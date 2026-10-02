namespace NeoTaskmgr.Core.AppHistory;

public enum AppHistoryKind
{
    Program,
    Service,
    Package,
}

/// <summary>The resource usage of one app, summed over the period of an <see cref="AppHistorySnapshot"/>.</summary>
/// <param name="Key">Identifies the app across reads; all versions of a program or package share it.</param>
/// <param name="Name">The program's file name, the service name, or the package name (not its display name).</param>
/// <param name="ImagePath">For programs: the most recently used executable, when its drive could be determined.</param>
/// <param name="PackageFamilyName">For packaged apps: the package family, to look up the display name and logo.</param>
/// <param name="NotificationBytes">The size of the push notifications (including tile updates) the app received.</param>
public sealed record AppHistoryEntry(
    string Key,
    AppHistoryKind Kind,
    string Name,
    string? ImagePath,
    string? PackageFamilyName,
    TimeSpan CpuTime,
    long BytesSent,
    long BytesReceived,
    long NotificationBytes)
{
    public long NetworkBytes => BytesSent + BytesReceived;
}

/// <param name="IncludesSystemAccounts">
/// False when the app is not running as administrator; then only the current user's usage can be read.
/// </param>
public sealed record AppHistorySnapshot(DateTime SinceUtc, bool IncludesSystemAccounts, IReadOnlyList<AppHistoryEntry> Entries);
