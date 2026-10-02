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
public sealed record AppSettings(
    AppPage StartPage = AppPage.Processes,
    UpdateSpeed UpdateSpeed = UpdateSpeed.Normal,
    bool AlwaysOnTop = false,
    AppTheme Theme = AppTheme.System);
