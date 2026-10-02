using NeoTaskmgr.Core.Processes;

namespace NeoTaskmgr.Core.Tests.Processes;

internal static class TestProcesses
{
    public static ProcessCounters Counters(int pid, int parentPid = 0, long createTime = 100, double cpuSeconds = 0) => new()
    {
        Pid = pid,
        ParentPid = parentPid,
        CreateTime = createTime,
        Name = $"process{pid}.exe",
        CpuTime = TimeSpan.FromSeconds(cpuSeconds),
    };
}
