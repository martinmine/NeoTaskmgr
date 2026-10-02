namespace NeoTaskmgr.ViewModels;

/// <summary>The view models of all pages; they outlive page navigation so state such as sorting is kept.</summary>
public sealed class MainViewModel(SettingsViewModel settings, DetailsViewModel details)
{
    public SettingsViewModel Settings { get; } = settings;
    public DetailsViewModel Details { get; } = details;
}
