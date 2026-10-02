namespace NeoTaskmgr.Core.Processes;

/// <summary>Identifies a process instance. PIDs are reused, so the creation time is part of the identity.</summary>
public readonly record struct ProcessKey(int Pid, long CreateTime);

/// <summary>Values that change while a process runs, read in one batch for all processes.</summary>
public sealed record ProcessCounters
{
    public required int Pid { get; init; }
    public required int ParentPid { get; init; }

    /// <summary>Creation time as a FILETIME (100 ns ticks since 1601, UTC).</summary>
    public required long CreateTime { get; init; }

    public required string Name { get; init; }
    public bool IsSuspended { get; init; }
    public int SessionId { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public int BasePriority { get; init; }
    public long PrivateWorkingSet { get; init; }
    public long WorkingSet { get; init; }
    public long PeakWorkingSet { get; init; }
    public long CommitSize { get; init; }
    public TimeSpan CpuTime { get; init; }

    public ProcessKey Key => new(Pid, CreateTime);
}

/// <summary>Values that are fixed for the lifetime of a process; read once and cached. Null when access is denied.</summary>
public sealed record ProcessDetails
{
    public static ProcessDetails Empty { get; } = new();

    public string? ImagePath { get; init; }
    public string? UserName { get; init; }
    public string? Description { get; init; }
    public string? Architecture { get; init; }
    public string? CommandLine { get; init; }
    public bool? IsElevated { get; init; }
}

public sealed record ProcessInfo(ProcessCounters Counters, double CpuPercent, ProcessDetails Details)
{
    public ProcessKey Key => Counters.Key;
}

public sealed record ProcessSnapshot(IReadOnlyList<ProcessInfo> Processes);
