using NeoTaskmgr.Core.Processes;
using static NeoTaskmgr.Core.Tests.Processes.TestProcesses;

namespace NeoTaskmgr.Core.Tests.Processes;

public sealed class ProcessTreeTests
{
    [Fact]
    public void ReturnsAllDescendants_DeepestFirst()
    {
        ProcessCounters[] processes =
        [
            Counters(1, parentPid: 0, createTime: 100),
            Counters(2, parentPid: 1, createTime: 200),
            Counters(3, parentPid: 2, createTime: 300),
            Counters(4, parentPid: 1, createTime: 400),
            Counters(5, parentPid: 99, createTime: 500),
        ];

        var descendants = ProcessTree.GetDescendants(processes, processes[0].Key).Select(p => p.Pid).ToList();

        Assert.Equal([3, 2, 4], descendants);
    }

    [Fact]
    public void IgnoresChildrenCreatedBeforeParent()
    {
        // Process 2 claims parent PID 1, but was created before the current process 1, so the PID was reused.
        ProcessCounters[] processes =
        [
            Counters(1, createTime: 500),
            Counters(2, parentPid: 1, createTime: 100),
        ];

        Assert.Empty(ProcessTree.GetDescendants(processes, processes[0].Key));
    }

    [Fact]
    public void ExcludesRootEvenIfItIsItsOwnParent()
    {
        // The idle process (PID 0) reports itself as its parent.
        ProcessCounters[] processes = [Counters(0, parentPid: 0, createTime: 0)];

        Assert.Empty(ProcessTree.GetDescendants(processes, processes[0].Key));
    }
}
