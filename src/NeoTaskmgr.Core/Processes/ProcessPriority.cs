using System.Diagnostics;

namespace NeoTaskmgr.Core.Processes;

public static class ProcessPriority
{
    /// <summary>Maps a process base priority (as reported by the kernel) to its priority class.</summary>
    public static ProcessPriorityClass? FromBasePriority(int basePriority) => basePriority switch
    {
        4 => ProcessPriorityClass.Idle,
        6 => ProcessPriorityClass.BelowNormal,
        8 => ProcessPriorityClass.Normal,
        10 => ProcessPriorityClass.AboveNormal,
        13 => ProcessPriorityClass.High,
        24 => ProcessPriorityClass.RealTime,
        _ => null,
    };
}
