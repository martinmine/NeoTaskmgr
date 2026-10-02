using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.ProcessStatus;
using Windows.Win32.System.SystemInformation;

namespace NeoTaskmgr.Core.Performance;

/// <summary>Memory properties that do not change. Hardware details are null when the firmware does not report them.</summary>
public sealed record MemoryInfo(long InstalledBytes, int? SpeedMts, int SlotsUsed, int SlotsTotal, string? FormFactor);

public sealed record MemorySample
{
    public required MemoryInfo Info { get; init; }
    public long TotalPhysical { get; init; }
    public long AvailablePhysical { get; init; }
    public long Committed { get; init; }
    public long CommitLimit { get; init; }
    public long PagedPool { get; init; }
    public long NonPagedPool { get; init; }

    /// <summary>Changed pages waiting to be written to disk before they can be reused.</summary>
    public long Modified { get; init; }

    /// <summary>Cached pages that can be reused immediately.</summary>
    public long Standby { get; init; }

    public long Free { get; init; }

    public long UsedPhysical => TotalPhysical - AvailablePhysical;
    public double UsedPercent => TotalPhysical == 0 ? 0 : UsedPhysical * 100.0 / TotalPhysical;
    public long Cached => Standby + Modified;

    /// <summary>Memory reserved by hardware and firmware, so not usable by Windows.</summary>
    public long HardwareReserved => Math.Max(0, Info.InstalledBytes - TotalPhysical);
}

internal static unsafe class MemoryReader
{
    public static MemoryInfo ReadInfo()
    {
        PInvoke.GetPhysicallyInstalledSystemMemory(out var installedKilobytes);
        var modules = SmbiosParser.ParseMemoryDevices(ReadSmbios());
        return new MemoryInfo((long)installedKilobytes * 1024, modules.SpeedMts, modules.SlotsUsed, modules.SlotsTotal, modules.FormFactor);
    }

    /// <param name="modified">The modified page list size, from a performance counter.</param>
    /// <param name="standby">The standby lists' size, from performance counters.</param>
    /// <param name="free">The free and zero page lists' size, from a performance counter.</param>
    public static MemorySample Read(MemoryInfo info, long modified, long standby, long free)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        var performance = new PERFORMANCE_INFORMATION { cb = (uint)sizeof(PERFORMANCE_INFORMATION) };
        if (!PInvoke.GlobalMemoryStatusEx(ref status) || !PInvoke.GetPerformanceInfo(ref performance, performance.cb))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var pageSize = (long)performance.PageSize;
        return new MemorySample
        {
            Info = info,
            TotalPhysical = (long)status.ullTotalPhys,
            AvailablePhysical = (long)status.ullAvailPhys,
            Committed = (long)performance.CommitTotal * pageSize,
            CommitLimit = (long)performance.CommitLimit * pageSize,
            PagedPool = (long)performance.KernelPaged * pageSize,
            NonPagedPool = (long)performance.KernelNonpaged * pageSize,
            Modified = modified,
            Standby = standby,
            Free = free,
        };
    }

    private static byte[] ReadSmbios()
    {
        const FIRMWARE_TABLE_PROVIDER rsmb = (FIRMWARE_TABLE_PROVIDER)0x52534D42; // 'RSMB'
        var size = PInvoke.GetSystemFirmwareTable(rsmb, 0, default);
        var buffer = new byte[size];
        return size > 0 && PInvoke.GetSystemFirmwareTable(rsmb, 0, buffer) == size ? buffer : [];
    }
}

/// <summary>Reads memory module details from the SMBIOS firmware table (type 17, "Memory Device").</summary>
internal static class SmbiosParser
{
    internal readonly record struct MemoryModules(int SlotsUsed, int SlotsTotal, int? SpeedMts, string? FormFactor);

    /// <param name="rawSmbiosData">A RawSMBIOSData structure: an 8-byte header followed by the SMBIOS structure table.</param>
    public static MemoryModules ParseMemoryDevices(ReadOnlySpan<byte> rawSmbiosData)
    {
        if (rawSmbiosData.Length < 8)
        {
            return default;
        }

        var tableLength = BinaryPrimitives.ReadInt32LittleEndian(rawSmbiosData[4..]);
        var table = rawSmbiosData[8..][..Math.Min(tableLength, rawSmbiosData.Length - 8)];
        int slots = 0, used = 0;
        int? speed = null;
        string? formFactor = null;

        for (var offset = 0; offset + 4 <= table.Length;)
        {
            var type = table[offset];
            var length = table[offset + 1];
            if (type == 127 || length < 4 || offset + length > table.Length)
            {
                break; // End of table, or a malformed structure.
            }

            var structure = table.Slice(offset, length);
            if (type == 17 && length >= 0x17)
            {
                slots++;
                var size = BinaryPrimitives.ReadUInt16LittleEndian(structure[0x0C..]);
                if (size is not 0 and not 0xFFFF)
                {
                    used++;
                    formFactor ??= FormFactorName(structure[0x0E]);
                    // Prefer the configured speed (SMBIOS 2.7+) over the module's maximum speed.
                    var moduleSpeed = length >= 0x22 ? BinaryPrimitives.ReadUInt16LittleEndian(structure[0x20..]) : (ushort)0;
                    if (moduleSpeed == 0)
                    {
                        moduleSpeed = BinaryPrimitives.ReadUInt16LittleEndian(structure[0x15..]);
                    }

                    if (moduleSpeed != 0)
                    {
                        speed = Math.Max(speed ?? 0, moduleSpeed);
                    }
                }
            }

            // The formatted area is followed by a set of strings, terminated by two zero bytes.
            var strings = offset + length;
            while (strings + 1 < table.Length && (table[strings] != 0 || table[strings + 1] != 0))
            {
                strings++;
            }

            offset = strings + 2;
        }

        return new MemoryModules(used, slots, speed, formFactor);
    }

    private static string? FormFactorName(byte formFactor) => formFactor switch
    {
        0x09 => "DIMM",
        0x0B => "Row of chips",
        0x0D => "SODIMM",
        0x0F => "FB-DIMM",
        0x10 => "Die",
        _ => null,
    };
}
