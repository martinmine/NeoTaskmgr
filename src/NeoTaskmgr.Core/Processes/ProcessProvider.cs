namespace NeoTaskmgr.Core.Processes;

/// <summary>Produces <see cref="ProcessSnapshot"/>s. Not thread-safe; call <see cref="Sample"/> from one thread at a time.</summary>
public sealed class ProcessProvider : IDisposable
{
    private const int IdleBasePriority = 4;

    private readonly SystemProcessReader _reader = new();
    private readonly ProcessDetailsReader _detailsReader = new();
    private readonly CpuUsageTracker _cpuUsage = new(SystemInfo.LogicalProcessorCount);
    private Dictionary<ProcessKey, ProcessDetails> _details = [];

    /// <param name="timestamp">A monotonic timestamp used to compute CPU usage since the previous sample.</param>
    public ProcessSnapshot Sample(TimeSpan timestamp)
    {
        var counters = _reader.Read();
        var cpuUsage = _cpuUsage.Update(counters, timestamp);
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
            processes[i] = new ProcessInfo(process, cpuUsage[i], processDetails)
            {
                Window = windows.GetValueOrDefault(process.Pid),
                // Efficiency mode always lowers the priority to idle, so only those processes need to be checked.
                IsEfficiencyMode = process.BasePriority == IdleBasePriority && ProcessActions.IsEfficiencyModeEnabled(process.Key),
            };
        }

        _details = details;
        return new ProcessSnapshot(processes);
    }

    public void Dispose() => _reader.Dispose();
}
