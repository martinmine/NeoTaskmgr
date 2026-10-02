using System.Text.RegularExpressions;

namespace NeoTaskmgr.Core.Services;

/// <summary>The state of a service, with the values of the Win32 SERVICE_* states.</summary>
public enum ServiceState
{
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7,
}

/// <param name="Pid">The hosting process, or 0 when the service is not running.</param>
/// <param name="Group">The service host group (the svchost "-k" parameter), or null for services with their own executable.</param>
public sealed record ServiceInfo(string Name, string DisplayName, ServiceState State, int Pid, string? Group);

public static partial class ServiceGroup
{
    /// <summary>Extracts the group from a service command line such as <c>C:\Windows\system32\svchost.exe -k netsvcs -p</c>.</summary>
    public static string? Parse(string? binaryPath) =>
        binaryPath is not null && GroupArgument().Match(binaryPath) is { Success: true } match ? match.Groups[1].Value : null;

    [GeneratedRegex(@"(?:^|\s)-k\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex GroupArgument();
}
