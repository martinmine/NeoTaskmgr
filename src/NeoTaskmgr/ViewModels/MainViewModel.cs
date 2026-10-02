using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.Core.Settings;
using NeoTaskmgr.Mvvm;
using NeoTaskmgr.ViewModels.Performance;

namespace NeoTaskmgr.ViewModels;

/// <summary>The view models of all pages; they outlive page navigation so state such as sorting is kept.</summary>
public sealed class MainViewModel
{
    public MainViewModel(
        SettingsViewModel settings,
        ProcessesViewModel processes,
        PerformanceViewModel performance,
        StartupAppsViewModel startupApps,
        DetailsViewModel details,
        ServicesViewModel services,
        Action refreshNow)
    {
        Settings = settings;
        Processes = processes;
        Performance = performance;
        StartupApps = startupApps;
        Details = details;
        Services = services;
        RefreshCommand = new RelayCommand(refreshNow);

        foreach (var table in Tables)
        {
            if (settings.Current.Tables?.GetValueOrDefault(table.Name) is { } layout)
            {
                table.ApplyLayout(layout);
            }
        }
    }

    public SettingsViewModel Settings { get; }
    public ProcessesViewModel Processes { get; }
    public PerformanceViewModel Performance { get; }

    /// <summary>Not updated by snapshots; the page reloads it when shown.</summary>
    public StartupAppsViewModel StartupApps { get; }

    public DetailsViewModel Details { get; }
    public ServicesViewModel Services { get; }

    /// <summary>Samples now, also when updates are paused.</summary>
    public RelayCommand RefreshCommand { get; }

    private IEnumerable<TableColumns> Tables => [Processes.Columns, StartupApps.Columns, Details.Columns, Services.Columns];

    /// <summary>Updates all pages from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot snapshot)
    {
        Processes.Apply(snapshot);
        Performance.Apply(snapshot);
        Details.Apply(snapshot);
        Services.Apply(snapshot.Services);
    }

    /// <summary>Remembers the window placement and the column layout of all tables for the next start.</summary>
    public void SaveSession(WindowPlacement? window) =>
        Settings.SaveSession(window, Tables.ToDictionary(t => t.Name, t => t.GetLayout()));
}
