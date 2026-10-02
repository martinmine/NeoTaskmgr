namespace NeoTaskmgr.Core.Settings;

/// <summary>The top-level pages of the app, in navigation order.</summary>
public enum AppPage
{
    Processes,
    Performance,
    StartupApps,
    Details,
    Services,
}

/// <summary>How often data is refreshed, matching Task Manager's "Real time update speed".</summary>
public enum UpdateSpeed
{
    High,
    Normal,
    Low,
    Paused,
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>User preferences, persisted by <see cref="SettingsStore"/>.</summary>
/// <remarks>
/// Defaults are constructor parameters (not property initializers) so the JSON deserializer
/// also applies them to values missing from the settings file.
/// </remarks>
/// <param name="MinimizeOnUse">Minimize this window after switching to another app from it.</param>
/// <param name="Window">The window's last position and size, or null to use the default.</param>
/// <param name="Tables">The column layout of each table, by table name.</param>
public sealed record AppSettings(
    AppPage StartPage = AppPage.Processes,
    UpdateSpeed UpdateSpeed = UpdateSpeed.Normal,
    bool AlwaysOnTop = false,
    AppTheme Theme = AppTheme.System,
    bool MinimizeOnUse = false,
    WindowPlacement? Window = null,
    IReadOnlyDictionary<string, TableLayout>? Tables = null);

/// <summary>A window's restored (not maximized) bounds in physical pixels, and whether it was maximized.</summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool IsMaximized);

/// <summary>The columns of a table as the user arranged them, and its sort order.</summary>
public sealed record TableLayout(IReadOnlyList<ColumnLayout> Columns, string? SortColumn, bool SortDescending);

public sealed record ColumnLayout(string Key, double Width, bool IsVisible);
