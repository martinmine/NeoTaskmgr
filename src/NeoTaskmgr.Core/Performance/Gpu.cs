using System.Globalization;
using NeoTaskmgr.Core.Interop;

namespace NeoTaskmgr.Core.Performance;

/// <summary>A graphics adapter, identified by its locally unique ID (LUID).</summary>
public sealed record GpuInfo(long Luid, string Name, long DedicatedMemoryBytes, long SharedMemoryBytes);

public sealed record GpuSample
{
    public required GpuInfo Info { get; init; }

    /// <summary>The utilization of the busiest engine, like Task Manager.</summary>
    public double UtilizationPercent { get; init; }

    /// <summary>Utilization per engine type, such as "3D", "Copy" or "VideoDecode".</summary>
    public required IReadOnlyDictionary<string, double> EngineUtilization { get; init; }

    public long DedicatedMemoryUsed { get; init; }
    public long SharedMemoryUsed { get; init; }
}

internal static class GpuReader
{
    /// <summary>Lists the hardware graphics adapters (excluding software renderers and compute-only devices such as NPUs).</summary>
    public static unsafe List<GpuInfo> ReadAdapters()
    {
        var gpus = new List<GpuInfo>();
        var enumAdapters = new D3DKMT_ENUMADAPTERS2();
        if (Gdi32.D3DKMTEnumAdapters2(&enumAdapters) < 0 || enumAdapters.NumAdapters == 0)
        {
            return gpus;
        }

        var adapters = new D3DKMT_ADAPTERINFO[enumAdapters.NumAdapters];
        fixed (D3DKMT_ADAPTERINFO* adapterInfo = adapters)
        {
            enumAdapters.Adapters = adapterInfo;
            if (Gdi32.D3DKMTEnumAdapters2(&enumAdapters) < 0)
            {
                return gpus;
            }
        }

        foreach (var adapter in adapters.Take((int)enumAdapters.NumAdapters))
        {
            var handle = adapter.Adapter;
            uint type = 0;
            D3DKMT_ADAPTERREGISTRYINFO registryInfo;
            D3DKMT_SEGMENTSIZEINFO segmentSizes;
            if (Query(handle, Gdi32.KmtQaiTypeAdapterType, &type, sizeof(uint)) &&
                (type & Gdi32.AdapterTypeRenderSupported) != 0 && (type & Gdi32.AdapterTypeSoftwareDevice) == 0 &&
                Query(handle, Gdi32.KmtQaiTypeAdapterRegistryInfo, &registryInfo, sizeof(D3DKMT_ADAPTERREGISTRYINFO)) &&
                Query(handle, Gdi32.KmtQaiTypeSegmentSizeInfo, &segmentSizes, sizeof(D3DKMT_SEGMENTSIZEINFO)))
            {
                var luid = ((long)adapter.LuidHighPart << 32) | adapter.LuidLowPart;
                var name = new string(registryInfo.AdapterString).TrimEnd('\0');
                gpus.Add(new GpuInfo(luid, name, (long)segmentSizes.DedicatedVideoMemorySize, (long)segmentSizes.SharedSystemMemorySize));
            }

            Gdi32.D3DKMTCloseAdapter(&handle);
        }

        return gpus;
    }

    private static unsafe bool Query(uint adapter, int type, void* data, int size)
    {
        var query = new D3DKMT_QUERYADAPTERINFO { Adapter = adapter, Type = type, PrivateDriverData = data, PrivateDriverDataSize = (uint)size };
        return Gdi32.D3DKMTQueryAdapterInfo(&query) >= 0;
    }
    /// <summary>
    /// Aggregates the "GPU Engine" counter, whose instances look like
    /// <c>pid_1234_luid_0x00000000_0x0000D1E5_phys_0_eng_3_engtype_3D</c> (one per process and engine).
    /// </summary>
    /// <returns>
    /// Per adapter, the utilization per engine type (the busiest engine of that type); and per process, the
    /// utilization of its busiest engine.
    /// </returns>
    internal static (Dictionary<long, Dictionary<string, double>> EnginesByAdapter, Dictionary<int, double> ByProcess) AggregateEngines(
        IEnumerable<(string Instance, double Value)> engineUtilization)
    {
        var perEngine = new Dictionary<(long Luid, string Engine), (string Type, double Value)>();
        var byProcess = new Dictionary<int, Dictionary<(long, string), double>>();
        foreach (var (instance, value) in engineUtilization)
        {
            if (ParseEngineInstance(instance) is not { } engine)
            {
                continue;
            }

            var key = (engine.Luid, engine.Engine);
            perEngine[key] = (engine.Type, perEngine.GetValueOrDefault(key).Value + value);
            if (!byProcess.TryGetValue(engine.Pid, out var processEngines))
            {
                byProcess[engine.Pid] = processEngines = [];
            }

            processEngines[key] = processEngines.GetValueOrDefault(key) + value;
        }

        var enginesByAdapter = new Dictionary<long, Dictionary<string, double>>();
        foreach (var ((luid, _), (type, value)) in perEngine)
        {
            if (!enginesByAdapter.TryGetValue(luid, out var types))
            {
                enginesByAdapter[luid] = types = [];
            }

            types[type] = Math.Min(100, Math.Max(types.GetValueOrDefault(type), value));
        }

        return (enginesByAdapter, byProcess.ToDictionary(p => p.Key, p => Math.Min(100, p.Value.Values.Max())));
    }

    /// <summary>Sums an adapter memory counter, whose instances look like <c>luid_0x00000000_0x0000D1E5_phys_0</c>, per adapter.</summary>
    internal static Dictionary<long, long> SumByAdapter(IEnumerable<(string Instance, double Value)> values)
    {
        var sums = new Dictionary<long, long>();
        foreach (var (instance, value) in values)
        {
            if (ParseLuid(instance) is { } luid)
            {
                sums[luid] = sums.GetValueOrDefault(luid) + (long)value;
            }
        }

        return sums;
    }

    internal static (int Pid, long Luid, string Engine, string Type)? ParseEngineInstance(string instance)
    {
        var parts = instance.Split('_');
        // pid _ 1234 _ luid _ 0xHIGH _ 0xLOW _ phys _ 0 _ eng _ 3 _ engtype _ <type, which may contain '_'>
        if (parts.Length < 11 || parts[0] != "pid" || parts[2] != "luid" || parts[7] != "eng" || parts[9] != "engtype" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ||
            ParseLuid(instance) is not { } luid)
        {
            return null;
        }

        return (pid, luid, $"{parts[6]}_{parts[8]}", string.Join('_', parts[10..]));
    }

    /// <summary>Finds "luid_0xHIGH_0xLOW" in a counter instance name.</summary>
    internal static long? ParseLuid(string instance)
    {
        var parts = instance.Split('_');
        var index = Array.IndexOf(parts, "luid");
        return index >= 0 && index + 2 < parts.Length &&
               TryParseHex(parts[index + 1], out var high) && TryParseHex(parts[index + 2], out var low)
            ? (high << 32) | low
            : null;
    }

    private static bool TryParseHex(string value, out long result) =>
        long.TryParse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value,
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
}
