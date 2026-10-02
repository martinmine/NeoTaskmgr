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

    /// <summary>Bytes read and written by all I/O of the process (files, devices and network).</summary>
    public long IoBytes { get; init; }

    public ProcessKey Key => new(Pid, CreateTime);
}

/// <summary>Values that are fixed for the lifetime of a process; read once and cached. Null when access is denied.</summary>
public sealed record ProcessDetails
{
    public static ProcessDetails Empty { get; } = new();

    public string? ImagePath { get; init; }
    public string? UserName { get; init; }
    public string? Description { get; init; }
    public string? Publisher { get; init; }
    public string? Architecture { get; init; }
    public string? CommandLine { get; init; }
    public bool? IsElevated { get; init; }
}

/// <summary>The main top-level window of a process, which makes it an app.</summary>
public sealed record ProcessWindow(nint Handle, bool IsHung);

public sealed record ProcessInfo(ProcessCounters Counters, double CpuPercent, ProcessDetails Details)
{
    public ProcessKey Key => Counters.Key;

    /// <summary>The process's main window, or null if it has no visible top-level window.</summary>
    public ProcessWindow? Window { get; init; }

    /// <summary>Windows 11 efficiency mode (EcoQoS power throttling with idle priority).</summary>
    public bool IsEfficiencyMode { get; init; }

    /// <summary>
    /// Read and write rate of the process's I/O. Unlike Task Manager (which traces disk activity), this also counts
    /// I/O to devices and the network.
    /// </summary>
    public double DiskBytesPerSecond { get; init; }

    /// <summary>Network bytes sent and received per second; null when unavailable (requires administrator rights).</summary>
    public double? NetworkBytesPerSecond { get; init; }

    /// <summary>Utilization of the busiest GPU engine used by the process.</summary>
    public double GpuPercent { get; init; }

    /// <summary>The name Task Manager shows: the file description, or the image name if there is none.</summary>
    public string DisplayName => Details.Description ?? Counters.Name;
}

public sealed record ProcessSnapshot(IReadOnlyList<ProcessInfo> Processes);
