using NeoTaskmgr.Core.Processes;
using static NeoTaskmgr.Core.Tests.Processes.TestProcesses;

namespace NeoTaskmgr.Core.Tests.Processes;

public sealed class CpuUsageTrackerTests
{
    [Fact]
    public void FirstSample_ReportsZero()
    {
        var tracker = new CpuUsageTracker(processorCount: 4);

        var usage = tracker.Update([Counters(1, cpuSeconds: 10)], TimeSpan.FromSeconds(1));

        Assert.Equal([0.0], usage);
    }

    [Fact]
    public void UsageIsCpuTimeDeltaOverTotalCapacity()
    {
        var tracker = new CpuUsageTracker(processorCount: 4);
        tracker.Update([Counters(1, cpuSeconds: 10), Counters(2, cpuSeconds: 0)], TimeSpan.FromSeconds(10));

        // 2 s elapsed on 4 processors = 8 s capacity. Process 1 used 2 s (25%), process 2 used 8 s (100%).
        var usage = tracker.Update([Counters(1, cpuSeconds: 12), Counters(2, cpuSeconds: 8)], TimeSpan.FromSeconds(12));

        Assert.Equal([25.0, 100.0], usage);
    }

    [Fact]
    public void NewProcess_ReportsZero()
    {
        var tracker = new CpuUsageTracker(processorCount: 1);
        tracker.Update([Counters(1)], TimeSpan.FromSeconds(1));

        var usage = tracker.Update([Counters(1, cpuSeconds: 0.5), Counters(2, cpuSeconds: 0.5)], TimeSpan.FromSeconds(2));

        Assert.Equal([50.0, 0.0], usage);
    }

    [Fact]
    public void ReusedPid_IsTreatedAsNewProcess()
    {
        var tracker = new CpuUsageTracker(processorCount: 1);
        tracker.Update([Counters(1, createTime: 100, cpuSeconds: 0)], TimeSpan.FromSeconds(1));

        var usage = tracker.Update([Counters(1, createTime: 200, cpuSeconds: 0.5)], TimeSpan.FromSeconds(2));

        Assert.Equal([0.0], usage);
    }

    [Fact]
    public void UsageIsClampedTo100()
    {
        var tracker = new CpuUsageTracker(processorCount: 1);
        tracker.Update([Counters(1)], TimeSpan.FromSeconds(1));

        var usage = tracker.Update([Counters(1, cpuSeconds: 5)], TimeSpan.FromSeconds(2));

        Assert.Equal([100.0], usage);
    }
}
