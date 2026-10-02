using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.ViewModels.Performance;

namespace NeoTaskmgr.ViewModels;

/// <summary>The view models of all pages; they outlive page navigation so state such as sorting is kept.</summary>
public sealed class MainViewModel(
    SettingsViewModel settings,
    ProcessesViewModel processes,
    PerformanceViewModel performance,
    DetailsViewModel details,
    ServicesViewModel services)
{
    public SettingsViewModel Settings { get; } = settings;
    public ProcessesViewModel Processes { get; } = processes;
    public PerformanceViewModel Performance { get; } = performance;
    public DetailsViewModel Details { get; } = details;
    public ServicesViewModel Services { get; } = services;

    /// <summary>Updates all pages from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot snapshot)
    {
        Processes.Apply(snapshot);
        Performance.Apply(snapshot);
        Details.Apply(snapshot);
        Services.Apply(snapshot.Services);
    }
}
