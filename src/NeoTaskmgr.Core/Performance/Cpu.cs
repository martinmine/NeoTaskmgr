using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NeoTaskmgr.Core.Interop;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;

namespace NeoTaskmgr.Core.Performance;

/// <summary>CPU properties that do not change.</summary>
public sealed record CpuInfo(
    string Name,
    double BaseSpeedMhz,
    int Sockets,
    int Cores,
    int LogicalProcessors,
    long L1CacheBytes,
    long L2CacheBytes,
    long L3CacheBytes,
    bool IsVirtualizationEnabled);

public sealed record CpuSample(CpuInfo Info, double UsagePercent, IReadOnlyList<double> ProcessorUsagePercent, double SpeedMhz);

/// <summary>Processor sockets, cores and caches, as reported by GetLogicalProcessorInformationEx.</summary>
internal readonly record struct CpuTopology(int Sockets, int Cores, long L1CacheBytes, long L2CacheBytes, long L3CacheBytes);

/// <summary>Reads CPU usage per logical processor. Usage is computed since the previous call.</summary>
internal sealed unsafe class CpuReader
{
    private (long Idle, long Total)[] _previous = [];

    public static CpuInfo ReadInfo()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        var topology = ParseTopology(ReadLogicalProcessorInformation());
        return new CpuInfo(
            (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? string.Empty,
            key?.GetValue("~MHz") is int mhz ? mhz : 0,
            topology.Sockets,
            topology.Cores,
            SystemInfo.LogicalProcessorCount,
            topology.L1CacheBytes,
            topology.L2CacheBytes,
            topology.L3CacheBytes,
            PInvoke.IsProcessorFeaturePresent(PROCESSOR_FEATURE_ID.PF_VIRT_FIRMWARE_ENABLED));
    }

    /// <summary>Returns the overall usage and the usage per logical processor, in percent.</summary>
    public (double Total, double[] PerProcessor) ReadUsage()
    {
        var times = ReadProcessorTimes();
        var perProcessor = new double[times.Length];
        long idleDelta = 0, totalDelta = 0;
        for (var i = 0; i < times.Length && i < _previous.Length; i++)
        {
            var idle = times[i].Idle - _previous[i].Idle;
            var total = times[i].Total - _previous[i].Total;
            perProcessor[i] = total > 0 ? Math.Clamp(100.0 * (total - idle) / total, 0, 100) : 0;
            idleDelta += idle;
            totalDelta += total;
        }

        _previous = times;
        return (totalDelta > 0 ? Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0, 100) : 0, perProcessor);
    }

    /// <summary>Idle and total (kernel + user, which includes idle) time per logical processor.</summary>
    private static (long Idle, long Total)[] ReadProcessorTimes()
    {
        var count = SystemInfo.LogicalProcessorCount;
        var buffer = new SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[count];
        uint returned;
        fixed (SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION* data = buffer)
        {
            var status = Ntdll.NtQuerySystemInformation(
                Ntdll.SystemProcessorPerformanceInformationClass, data, (uint)(count * sizeof(SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION)), out returned);
            if (status < 0)
            {
                return [];
            }
        }

        var processors = (int)returned / sizeof(SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION);
        return buffer.Take(processors).Select(p => (p.IdleTime, p.KernelTime + p.UserTime)).ToArray();
    }

    private static byte[] ReadLogicalProcessorInformation()
    {
        uint length = 0;
        PInvoke.GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationAll, null, &length);
        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            return PInvoke.GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationAll, (SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*)data, &length)
                ? buffer
                : [];
        }
    }

    /// <summary>Parses the variable-length SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX records.</summary>
    internal static CpuTopology ParseTopology(ReadOnlySpan<byte> data)
    {
        const int relationProcessorCore = 0, relationCache = 2, relationProcessorPackage = 3;
        int sockets = 0, cores = 0;
        long l1 = 0, l2 = 0, l3 = 0;
        for (var offset = 0; offset + 8 <= data.Length;)
        {
            var relationship = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            var size = BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 4)..]);
            if (size <= 0 || offset + size > data.Length)
            {
                break;
            }

            switch (relationship)
            {
                case relationProcessorCore:
                    cores++;
                    break;
                case relationProcessorPackage:
                    sockets++;
                    break;
                case relationCache when size >= 20:
                    // CACHE_RELATIONSHIP: Level (byte) at 8, CacheSize (uint) at 12.
                    var cacheSize = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 12)..]);
                    switch (data[offset + 8])
                    {
                        case 1: l1 += cacheSize; break;
                        case 2: l2 += cacheSize; break;
                        case 3: l3 += cacheSize; break;
                    }

                    break;
            }

            offset += size;
        }

        return new CpuTopology(sockets, cores, l1, l2, l3);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
{
    public long IdleTime;
    public long KernelTime;
    public long UserTime;
    public long DpcTime;
    public long InterruptTime;
    public uint InterruptCount;
}
