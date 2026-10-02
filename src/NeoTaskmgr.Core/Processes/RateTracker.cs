namespace NeoTaskmgr.Core.Processes;

/// <summary>Turns a cumulative per-process counter (such as CPU time or bytes read) into a rate per second.</summary>
public sealed class RateTracker(Func<ProcessCounters, long> counter)
{
    private Dictionary<ProcessKey, long> _previous = [];
    private TimeSpan _previousTimestamp;

    /// <param name="processes">The current processes.</param>
    /// <param name="timestamp">A monotonic timestamp of the sample.</param>
    /// <returns>The increase per second since the previous call, per process in the same order. Zero for new processes.</returns>
    public double[] Update(IReadOnlyList<ProcessCounters> processes, TimeSpan timestamp)
    {
        var seconds = (timestamp - _previousTimestamp).TotalSeconds;
        var rates = new double[processes.Count];
        var current = new Dictionary<ProcessKey, long>(processes.Count);

        for (var i = 0; i < processes.Count; i++)
        {
            var value = counter(processes[i]);
            current[processes[i].Key] = value;
            if (seconds > 0 && _previous.TryGetValue(processes[i].Key, out var previous))
            {
                rates[i] = Math.Max(0, value - previous) / seconds;
            }
        }

        _previous = current;
        _previousTimestamp = timestamp;
        return rates;
    }
}
