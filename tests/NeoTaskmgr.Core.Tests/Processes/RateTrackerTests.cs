using NeoTaskmgr.Core.Processes;
using static NeoTaskmgr.Core.Tests.Processes.TestProcesses;

namespace NeoTaskmgr.Core.Tests.Processes;

public sealed class RateTrackerTests
{
    private readonly RateTracker _tracker = new(p => (long)p.CpuTime.TotalSeconds);

    [Fact]
    public void FirstSample_ReportsZero() =>
        Assert.Equal([0.0], _tracker.Update([Counters(1, cpuSeconds: 10)], TimeSpan.FromSeconds(1)));

    [Fact]
    public void RateIsIncreasePerSecond()
    {
        _tracker.Update([Counters(1, cpuSeconds: 10), Counters(2, cpuSeconds: 0)], TimeSpan.FromSeconds(10));

        var rates = _tracker.Update([Counters(1, cpuSeconds: 12), Counters(2, cpuSeconds: 8)], TimeSpan.FromSeconds(12));

        Assert.Equal([1.0, 4.0], rates);
    }

    [Fact]
    public void NewProcess_ReportsZero()
    {
        _tracker.Update([Counters(1)], TimeSpan.FromSeconds(1));

        Assert.Equal([1.0, 0.0], _tracker.Update([Counters(1, cpuSeconds: 1), Counters(2, cpuSeconds: 1)], TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ReusedPid_IsTreatedAsNewProcess()
    {
        _tracker.Update([Counters(1, createTime: 100, cpuSeconds: 0)], TimeSpan.FromSeconds(1));

        Assert.Equal([0.0], _tracker.Update([Counters(1, createTime: 200, cpuSeconds: 5)], TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void DecreasingCounter_ReportsZero()
    {
        _tracker.Update([Counters(1, cpuSeconds: 5)], TimeSpan.FromSeconds(1));

        Assert.Equal([0.0], _tracker.Update([Counters(1, cpuSeconds: 3)], TimeSpan.FromSeconds(2)));
    }
}
