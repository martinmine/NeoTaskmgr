using Windows.Win32;

namespace NeoTaskmgr.Core;

public static class SystemInfo
{
    /// <summary>Number of logical processors in all processor groups (not limited by this process's affinity).</summary>
    public static int LogicalProcessorCount { get; } = (int)PInvoke.GetActiveProcessorCount(PInvoke.ALL_PROCESSOR_GROUPS);
}
