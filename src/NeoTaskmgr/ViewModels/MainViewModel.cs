using NeoTaskmgr.Core.Sampling;

namespace NeoTaskmgr.ViewModels;

/// <summary>The view models of all pages; they outlive page navigation so state such as sorting is kept.</summary>
public sealed class MainViewModel(SettingsViewModel settings, ProcessesViewModel processes, DetailsViewModel details)
{
    public SettingsViewModel Settings { get; } = settings;
    public ProcessesViewModel Processes { get; } = processes;
    public DetailsViewModel Details { get; } = details;

    /// <summary>Updates all pages from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot snapshot)
    {
        Processes.Apply(snapshot);
        Details.Apply(snapshot.Processes);
    }
}
