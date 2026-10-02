using Windows.Win32.System.Performance;

namespace NeoTaskmgr.Core.Performance;

/// <summary>System-wide performance in one sampling tick.</summary>
public sealed record PerformanceSnapshot(
    CpuSample Cpu,
    MemorySample Memory,
    IReadOnlyList<DiskSample> Disks,
    IReadOnlyList<NetworkAdapterSample> NetworkAdapters,
    IReadOnlyList<GpuSample> Gpus);

/// <summary>Produces <see cref="PerformanceSnapshot"/>s. Not thread-safe; call <see cref="Sample"/> from one thread at a time.</summary>
public sealed class PerformanceProvider : IDisposable
{
    private readonly PerformanceCounterQuery _query = new();
    private readonly CpuReader _cpu = new();
    private readonly NetworkAdapterReader _networkAdapters = new();
    private readonly CpuInfo _cpuInfo = CpuReader.ReadInfo();
    private readonly MemoryInfo _memoryInfo = MemoryReader.ReadInfo();
    private readonly IReadOnlyList<GpuInfo> _gpus = GpuReader.ReadAdapters();
    private readonly Dictionary<string, DiskInfo> _diskInfo = [];

    private readonly PDH_HCOUNTER? _processorPerformance;
    private readonly PDH_HCOUNTER? _modifiedBytes;
    private readonly PDH_HCOUNTER?[] _standbyBytes;
    private readonly PDH_HCOUNTER? _freeBytes;
    private readonly PDH_HCOUNTER? _diskIdle;
    private readonly PDH_HCOUNTER? _diskRead;
    private readonly PDH_HCOUNTER? _diskWrite;
    private readonly PDH_HCOUNTER? _diskSecondsPerTransfer;
    private readonly PDH_HCOUNTER? _gpuEngineUtilization;
    private readonly PDH_HCOUNTER? _gpuDedicatedUsage;
    private readonly PDH_HCOUNTER? _gpuSharedUsage;

    public PerformanceProvider()
    {
        _processorPerformance = _query.TryAdd(@"\Processor Information(_Total)\% Processor Performance");
        _modifiedBytes = _query.TryAdd(@"\Memory\Modified Page List Bytes");
        _standbyBytes =
        [
            _query.TryAdd(@"\Memory\Standby Cache Core Bytes"),
            _query.TryAdd(@"\Memory\Standby Cache Normal Priority Bytes"),
            _query.TryAdd(@"\Memory\Standby Cache Reserve Bytes"),
        ];
        _freeBytes = _query.TryAdd(@"\Memory\Free & Zero Page List Bytes");
        _diskIdle = _query.TryAdd(@"\PhysicalDisk(*)\% Idle Time");
        _diskRead = _query.TryAdd(@"\PhysicalDisk(*)\Disk Read Bytes/sec");
        _diskWrite = _query.TryAdd(@"\PhysicalDisk(*)\Disk Write Bytes/sec");
        _diskSecondsPerTransfer = _query.TryAdd(@"\PhysicalDisk(*)\Avg. Disk sec/Transfer");
        _gpuEngineUtilization = _query.TryAdd(@"\GPU Engine(*)\Utilization Percentage");
        _gpuDedicatedUsage = _query.TryAdd(@"\GPU Adapter Memory(*)\Dedicated Usage");
        _gpuSharedUsage = _query.TryAdd(@"\GPU Adapter Memory(*)\Shared Usage");
    }

    /// <param name="timestamp">A monotonic timestamp used to compute rates since the previous sample.</param>
    /// <returns>The snapshot, and the GPU utilization per process ID.</returns>
    public (PerformanceSnapshot Snapshot, IReadOnlyDictionary<int, double> GpuByPid) Sample(TimeSpan timestamp)
    {
        _query.Collect();

        var (cpuTotal, cpuPerProcessor) = _cpu.ReadUsage();
        var cpu = new CpuSample(_cpuInfo, cpuTotal, cpuPerProcessor, _cpuInfo.BaseSpeedMhz * _query.GetValue(_processorPerformance) / 100);

        var memory = MemoryReader.Read(
            _memoryInfo,
            (long)_query.GetValue(_modifiedBytes),
            (long)_standbyBytes.Sum(_query.GetValue),
            (long)_query.GetValue(_freeBytes));

        var disks = DiskReader.Read(
            _query.GetValues(_diskIdle), _query.GetValues(_diskRead), _query.GetValues(_diskWrite), _query.GetValues(_diskSecondsPerTransfer), GetDiskInfo);

        var (enginesByAdapter, gpuByPid) = GpuReader.AggregateEngines(_query.GetValues(_gpuEngineUtilization));
        var dedicated = GpuReader.SumByAdapter(_query.GetValues(_gpuDedicatedUsage));
        var shared = GpuReader.SumByAdapter(_query.GetValues(_gpuSharedUsage));
        var gpus = _gpus.Select(gpu =>
        {
            var engines = enginesByAdapter.GetValueOrDefault(gpu.Luid) ?? [];
            return new GpuSample
            {
                Info = gpu,
                EngineUtilization = engines,
                UtilizationPercent = engines.Count == 0 ? 0 : engines.Values.Max(),
                DedicatedMemoryUsed = dedicated.GetValueOrDefault(gpu.Luid),
                SharedMemoryUsed = shared.GetValueOrDefault(gpu.Luid),
            };
        }).ToList();

        var snapshot = new PerformanceSnapshot(cpu, memory, disks, _networkAdapters.Read(timestamp), gpus);
        return (snapshot, gpuByPid);
    }

    public void Dispose() => _query.Dispose();

    private DiskInfo GetDiskInfo(string instance, int number, string[] volumes)
    {
        if (!_diskInfo.TryGetValue(instance, out var info))
        {
            _diskInfo[instance] = info = DiskReader.ReadInfo(number, volumes);
        }

        return info;
    }
}
