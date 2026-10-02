namespace NeoTaskmgr.Core.Startup;

/// <summary>Where a startup entry is registered.</summary>
public enum StartupSource
{
    CurrentUserRun,
    LocalMachineRun,
    LocalMachineRun32,
    CurrentUserStartupFolder,
    CommonStartupFolder,
}

/// <param name="Name">The registry value name, or the file name (without .lnk) in a Startup folder.</param>
/// <param name="Command">The command line, or the path of the file in the Startup folder.</param>
/// <param name="TargetPath">The program that runs, when it could be determined.</param>
/// <param name="Location">The registry key or folder the entry is in.</param>
/// <param name="ApprovalName">The value name under the StartupApproved key that stores the enabled state.</param>
public sealed record StartupEntry(
    string Name,
    string Command,
    string? TargetPath,
    StartupSource Source,
    string Location,
    string ApprovalName,
    bool IsEnabled,
    string? Description,
    string? Publisher)
{
    /// <summary>The name Task Manager shows: the program's description, or the entry name.</summary>
    public string DisplayName => Description ?? Name;

    public bool IsInRegistry => Source is StartupSource.CurrentUserRun or StartupSource.LocalMachineRun or StartupSource.LocalMachineRun32;
}

/// <summary>
/// The StartupApproved values Explorer and Task Manager use: 12 bytes, where an odd first byte means disabled and
/// bytes 4–11 hold the time it was disabled (a FILETIME). A missing value means enabled.
/// </summary>
public static class StartupApproval
{
    public static bool IsEnabled(byte[]? value) => value is not { Length: > 0 } || (value[0] & 1) == 0;

    public static byte[] Create(bool enabled, DateTime utcNow)
    {
        var value = new byte[12];
        value[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
        {
            BitConverter.TryWriteBytes(value.AsSpan(4), utcNow.ToFileTimeUtc());
        }

        return value;
    }
}
