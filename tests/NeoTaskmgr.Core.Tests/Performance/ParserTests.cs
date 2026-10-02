using System.Buffers.Binary;
using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.Core.Tests.Performance;

public sealed class ParserTests
{
    [Fact]
    public void ParseTopology_CountsSocketsCoresAndCaches()
    {
        var data = Concat(
            Record(relationship: 3, size: 16), // Package
            Record(relationship: 0, size: 16), // Core
            Record(relationship: 0, size: 16), // Core
            Cache(level: 1, size: 32 * 1024), // L1 data
            Cache(level: 1, size: 32 * 1024), // L1 instruction
            Cache(level: 2, size: 512 * 1024),
            Cache(level: 3, size: 8 * 1024 * 1024),
            Record(relationship: 4, size: 16)); // Group

        var topology = CpuReader.ParseTopology(data);

        Assert.Equal(new CpuTopology(Sockets: 1, Cores: 2, L1CacheBytes: 64 * 1024, L2CacheBytes: 512 * 1024, L3CacheBytes: 8 * 1024 * 1024), topology);
    }

    [Fact]
    public void ParseTopology_StopsAtRecordLongerThanData()
    {
        var truncated = Record(relationship: 0, size: 16);
        BinaryPrimitives.WriteInt32LittleEndian(truncated.AsSpan(4), 100); // Claims 100 bytes, but only 16 follow.

        Assert.Equal(new CpuTopology(1, 0, 0, 0, 0), CpuReader.ParseTopology(Concat(Record(3, 16), truncated)));
    }

    [Fact]
    public void ParseMemoryDevices_ReadsType17()
    {
        var table = Concat(
            Structure(type: 0, length: 0x18, strings: ["BIOS vendor"]),
            MemoryDevice(sizeMb: 16384, formFactor: 0x0D, speed: 4800, configuredSpeed: 4400),
            MemoryDevice(sizeMb: 0, formFactor: 0x0D, speed: 0, configuredSpeed: 0), // Empty slot
            MemoryDevice(sizeMb: 16384, formFactor: 0x0D, speed: 4800, configuredSpeed: 0),
            Structure(type: 127, length: 4, strings: []));

        var modules = SmbiosParser.ParseMemoryDevices(RawSmbios(table));

        Assert.Equal(new SmbiosParser.MemoryModules(SlotsUsed: 2, SlotsTotal: 3, SpeedMts: 4800, FormFactor: "SODIMM"), modules);
    }

    [Fact]
    public void ParseMemoryDevices_EmptyOrTruncatedData() =>
        Assert.Equal(default, SmbiosParser.ParseMemoryDevices([1, 2, 3]));

    [Theory]
    [InlineData("0 C:", 0, new[] { "C:" })]
    [InlineData("1 D: E:", 1, new[] { "D:", "E:" })]
    [InlineData("2", 2, new string[0])]
    public void ParseDiskInstance(string instance, int number, string[] volumes)
    {
        var disk = DiskReader.ParseInstance(instance);

        Assert.Equal(number, disk!.Value.Number);
        Assert.Equal(volumes, disk.Value.Volumes);
    }

    [Fact]
    public void ParseDiskInstance_Total_IsNull() => Assert.Null(DiskReader.ParseInstance("_Total"));

    [Fact]
    public void ParseGpuEngineInstance()
    {
        var engine = GpuReader.ParseEngineInstance("pid_1234_luid_0x00000001_0x0000D1E5_phys_0_eng_3_engtype_VideoDecode");

        Assert.Equal((1234, (1L << 32) | 0xD1E5, "0_3", "VideoDecode"), engine);
    }

    [Theory]
    [InlineData("_Total")]
    [InlineData("pid_x_luid_0x0_0x1_phys_0_eng_0_engtype_3D")]
    [InlineData("luid_0x0_0x1_phys_0")]
    public void ParseGpuEngineInstance_Invalid_IsNull(string instance) => Assert.Null(GpuReader.ParseEngineInstance(instance));

    [Fact]
    public void AggregateGpuEngines()
    {
        (string, double)[] values =
        [
            ("pid_1_luid_0x0_0x10_phys_0_eng_0_engtype_3D", 20),
            ("pid_2_luid_0x0_0x10_phys_0_eng_0_engtype_3D", 30), // Same engine: summed to 50.
            ("pid_2_luid_0x0_0x10_phys_0_eng_1_engtype_3D", 10), // Another 3D engine: the busiest one counts.
            ("pid_2_luid_0x0_0x10_phys_0_eng_2_engtype_Copy", 5),
            ("pid_3_luid_0x0_0x20_phys_0_eng_0_engtype_3D", 70), // Another adapter.
        ];

        var (enginesByAdapter, byProcess) = GpuReader.AggregateEngines(values);

        Assert.Equal(new Dictionary<string, double> { ["3D"] = 50, ["Copy"] = 5 }, enginesByAdapter[0x10]);
        Assert.Equal(new Dictionary<string, double> { ["3D"] = 70 }, enginesByAdapter[0x20]);
        Assert.Equal(new Dictionary<int, double> { [1] = 20, [2] = 30, [3] = 70 }, byProcess);
    }

    [Fact]
    public void SumGpuMemoryByAdapter() =>
        Assert.Equal(
            new Dictionary<long, long> { [0x10] = 300, [0x20] = 5 },
            GpuReader.SumByAdapter([("luid_0x0_0x10_phys_0", 100), ("luid_0x0_0x10_phys_1", 200), ("luid_0x0_0x20_phys_0", 5), ("_Total", 9)]));

    private static byte[] Record(int relationship, int size)
    {
        var record = new byte[size];
        BinaryPrimitives.WriteInt32LittleEndian(record, relationship);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(4), size);
        return record;
    }

    private static byte[] Cache(byte level, uint size)
    {
        var record = Record(relationship: 2, size: 48);
        record[8] = level;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(12), size);
        return record;
    }

    private static byte[] Structure(byte type, byte length, string[] strings)
    {
        var formatted = new byte[length];
        formatted[0] = type;
        formatted[1] = length;
        var stringSet = strings.Length == 0
            ? [0, 0]
            : strings.SelectMany(s => System.Text.Encoding.ASCII.GetBytes(s).Append((byte)0)).Append((byte)0).ToArray();
        return Concat(formatted, stringSet);
    }

    private static byte[] MemoryDevice(ushort sizeMb, byte formFactor, ushort speed, ushort configuredSpeed)
    {
        var device = Structure(type: 17, length: 0x28, strings: ["DIMM 0", "BANK 0"]);
        BinaryPrimitives.WriteUInt16LittleEndian(device.AsSpan(0x0C), sizeMb);
        device[0x0E] = formFactor;
        BinaryPrimitives.WriteUInt16LittleEndian(device.AsSpan(0x15), speed);
        BinaryPrimitives.WriteUInt16LittleEndian(device.AsSpan(0x20), configuredSpeed);
        return device;
    }

    private static byte[] RawSmbios(byte[] table)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), table.Length);
        return Concat(header, table);
    }

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
