using System.Security.Principal;

namespace NeoTaskmgr.Core;

public static class Elevation
{
    /// <summary>True when the current process runs with administrator rights.</summary>
    public static bool IsElevated { get; } = IsCurrentProcessElevated();

    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
