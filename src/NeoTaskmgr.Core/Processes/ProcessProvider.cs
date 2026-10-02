namespace NeoTaskmgr.Core.Processes;

/// <summary>Produces <see cref="ProcessSnapshot"/>s. Not thread-safe; call <see cref="Sample"/> from one thread at a time.</summary>
public sealed class ProcessProvider : IDisposable
{
    private const int IdleBasePriority = 4;

    private readonly SystemProcessReader _reader = new();
    private readonly ProcessDetailsReader _detailsReader = new();
    private readonly RateTracker _cpuTime = new(p => p.CpuTime.Ticks);
    private readonly RateTracker _ioBytes = new(p => p.IoBytes);
    private Dictionary<ProcessKey, ProcessDetails> _details = [];
    private TimeSpan _previousTimestamp;

    /// <param name="timestamp">A monotonic timestamp used to compute rates since the previous sample.</param>
    /// <param name="gpuByPid">GPU utilization per process ID.</param>
    /// <param name="networkBytesByPid">Network bytes per process ID since the previous sample, or null if unknown.</param>
    public ProcessSnapshot Sample(
        TimeSpan timestamp, IReadOnlyDictionary<int, double>? gpuByPid = null, IReadOnlyDictionary<int, long>? networkBytesByPid = null)
    {
        var counters = _reader.Read();
        var cpuTicksPerSecond = _cpuTime.Update(counters, timestamp);
        var ioBytesPerSecond = _ioBytes.Update(counters, timestamp);
        var seconds = (timestamp - _previousTimestamp).TotalSeconds;
        _previousTimestamp = timestamp;
        var windows = AppWindowReader.Read();

        // Details are read once per process; rebuilding the dictionary drops exited processes.
        var details = new Dictionary<ProcessKey, ProcessDetails>(counters.Count);
        var processes = new ProcessInfo[counters.Count];
        for (var i = 0; i < counters.Count; i++)
        {
            var process = counters[i];
            if (!_details.TryGetValue(process.Key, out var processDetails))
            {
                processDetails = _detailsReader.Read(process);
            }

            details[process.Key] = processDetails;
            var cpuPercent = cpuTicksPerSecond[i] / TimeSpan.TicksPerSecond / SystemInfo.LogicalProcessorCount * 100;
            processes[i] = new ProcessInfo(process, Math.Clamp(cpuPercent, 0, 100), processDetails)
            {
                Window = windows.GetValueOrDefault(process.Pid),
                // Efficiency mode always lowers the priority to idle, so only those processes need to be checked.
                IsEfficiencyMode = process.BasePriority == IdleBasePriority && ProcessActions.IsEfficiencyModeEnabled(process.Key),
                DiskBytesPerSecond = ioBytesPerSecond[i],
                NetworkBytesPerSecond = networkBytesByPid is null ? null : seconds > 0 ? networkBytesByPid.GetValueOrDefault(process.Pid) / seconds : 0,
                GpuPercent = gpuByPid?.GetValueOrDefault(process.Pid) ?? 0,
            };
        }

        _details = details;
        return new ProcessSnapshot(processes);
    }

    public void Dispose() => _reader.Dispose();
}
