namespace NeoTaskmgr.Core.Processes;

/// <summary>Turns cumulative CPU times into a percentage of total CPU capacity since the previous sample.</summary>
public sealed class CpuUsageTracker(int processorCount)
{
    private Dictionary<ProcessKey, TimeSpan> _previous = [];
    private TimeSpan _previousTimestamp;

    /// <param name="processes">The current processes.</param>
    /// <param name="timestamp">A monotonic timestamp of the sample.</param>
    /// <returns>CPU usage (0–100) per process, in the same order. Zero for processes seen for the first time.</returns>
    public double[] Update(IReadOnlyList<ProcessCounters> processes, TimeSpan timestamp)
    {
        var capacityTicks = (double)(timestamp - _previousTimestamp).Ticks * processorCount;
        var usage = new double[processes.Count];
        var current = new Dictionary<ProcessKey, TimeSpan>(processes.Count);

        for (var i = 0; i < processes.Count; i++)
        {
            var process = processes[i];
            current[process.Key] = process.CpuTime;

            if (capacityTicks > 0 && _previous.TryGetValue(process.Key, out var previousCpuTime))
            {
                usage[i] = Math.Clamp((process.CpuTime - previousCpuTime).Ticks * 100.0 / capacityTicks, 0, 100);
            }
        }

        _previous = current;
        _previousTimestamp = timestamp;
        return usage;
    }
}
