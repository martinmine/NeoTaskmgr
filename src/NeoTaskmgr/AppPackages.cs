using System.Collections.Concurrent;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace NeoTaskmgr;

/// <summary>The installed packaged apps of the current user, looked up by package family name. Thread-safe.</summary>
public static class AppPackages
{
    private static readonly PackageManager Manager = new();
    private static readonly ConcurrentDictionary<string, AppListEntry?> Entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The name shown in Start, such as "Calculator"; null when the package is no longer installed.</summary>
    public static string? GetDisplayName(string familyName)
    {
        try
        {
            return GetEntry(familyName)?.DisplayInfo.DisplayName is { Length: > 0 } name ? name : null;
        }
        catch (Exception)
        {
            return null; // Packages being updated or removed can fail to describe themselves.
        }
    }

    /// <summary>The app's first entry in Start, which has its name and logo.</summary>
    public static AppListEntry? GetEntry(string familyName) => Entries.GetOrAdd(familyName, static family =>
    {
        try
        {
            // An empty user means the current user; this also works when running as administrator.
            return Manager.FindPackagesForUser(string.Empty, family).FirstOrDefault() is Package package
                ? package.GetAppListEntries().FirstOrDefault()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    });
}
