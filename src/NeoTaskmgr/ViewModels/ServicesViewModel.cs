using System.Collections.ObjectModel;
using System.Globalization;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Services;
using NeoTaskmgr.Core.Shell;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>The Services page: all Win32 services with their state, and actions to start and stop them.</summary>
public sealed class ServicesViewModel : ObservableObject
{
    private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(30);

    private readonly ProcessCommandRunner _runner;
    private Dictionary<string, ServiceRowViewModel> _rows = [];
    private string _search = string.Empty;
    private ServiceRowViewModel? _selectedRow;
    private bool _isSyncingRows;

    public ServicesViewModel(ProcessCommandRunner runner)
    {
        _runner = runner;

        // The order must match the cells in the row template of ServicesPage.xaml.
        Columns = new TableColumns("Services",
        [
            Text("Name", 220, r => r.Name, canHide: false),
            new TableColumn<ServiceRowViewModel>("Pid", Strings.Get("ColumnPid"), 70, (a, b) => a.Info.Pid.CompareTo(b.Info.Pid), isNumeric: true),
            Text("Description", 340, r => r.DisplayName),
            Text("Status", 110, r => r.StatusText),
            Text("Group", 180, r => r.Group),
        ]);

        StartCommand = new RelayCommand(() => _ = RunAsync(ServiceActions.Start), () => SelectedRow?.Info.State == ServiceState.Stopped);
        StopCommand = new RelayCommand(() => _ = RunAsync(ServiceActions.Stop), () => SelectedRow?.Info.State == ServiceState.Running);
        RestartCommand = new RelayCommand(() => _ = RunAsync(name => ServiceActions.Restart(name, RestartTimeout)),
            () => SelectedRow?.Info.State == ServiceState.Running);
        OpenServicesCommand = new RelayCommand(() => _ = _runner.RunAsync(ServiceActions.OpenServicesConsole));
        SearchOnlineCommand = new RelayCommand(
            () => _ = _runner.RunAsync(() => ShellActions.SearchOnline($"{SelectedRow!.Name} {SelectedRow.DisplayName}")), () => SelectedRow is not null);
        GoToDetailsCommand = new RelayCommand(() => GoToDetailsRequested?.Invoke(SelectedRow!.Info.Pid), () => SelectedRow?.Info.Pid > 0);

        Columns.SortChanged += (_, _) => Refresh();
        Columns.Sort(Columns[0]);
    }

    /// <summary>Asks the window to show the hosting process (by PID) on the Details page.</summary>
    public event Action<int>? GoToDetailsRequested;

    public TableColumns Columns { get; }

    public ObservableCollection<ServiceRowViewModel> Rows { get; } = [];

    public ServiceRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            // While rows are reordered the list view may briefly report no selection; keep the selected service.
            if (!_isSyncingRows && SetProperty(ref _selectedRow, value))
            {
                UpdateCommands();
            }
        }
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand OpenServicesCommand { get; }
    public RelayCommand SearchOnlineCommand { get; }
    public RelayCommand GoToDetailsCommand { get; }

    /// <summary>Updates the rows from the current services. Must be called on the UI thread.</summary>
    public void Apply(IReadOnlyList<ServiceInfo> services)
    {
        var rows = new Dictionary<string, ServiceRowViewModel>(services.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            var row = _rows.GetValueOrDefault(service.Name) ?? new ServiceRowViewModel(service);
            row.Update(service);
            rows[service.Name] = row;
        }

        _rows = rows;
        Refresh();
    }

    /// <summary>Filters by name, description or group; a number shows the services hosted by that process ID.</summary>
    public void ApplySearch(string text)
    {
        _search = text.Trim();
        Refresh();
    }

    private void Refresh()
    {
        var rows = _rows.Values.Where(MatchesSearch).ToList();
        if (Columns.SortColumn is TableColumn<ServiceRowViewModel> sortColumn)
        {
            var direction = Columns.SortDescending ? -1 : 1;
            rows.Sort((a, b) =>
            {
                var result = sortColumn.Compare(a, b) * direction;
                return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        _isSyncingRows = true;
        try
        {
            Rows.SyncWith(rows);
        }
        finally
        {
            _isSyncingRows = false;
        }

        if (_selectedRow is not null && !rows.Contains(_selectedRow))
        {
            SelectedRow = null;
        }
        else
        {
            OnPropertyChanged(nameof(SelectedRow)); // Restores the selection in the list view.
            UpdateCommands(); // The selected service's state may have changed.
        }
    }

    private bool MatchesSearch(ServiceRowViewModel row) =>
        _search.Length == 0 ||
        (int.TryParse(_search, NumberStyles.None, CultureInfo.CurrentCulture, out var pid)
            ? row.Info.Pid == pid
            : row.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
              row.DisplayName.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
              row.Group.Contains(_search, StringComparison.CurrentCultureIgnoreCase));

    private void UpdateCommands()
    {
        foreach (var command in new[] { StartCommand, StopCommand, RestartCommand, SearchOnlineCommand, GoToDetailsCommand })
        {
            command.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Runs a service action in the background (the service manager can be slow) for the selected service.</summary>
    private Task RunAsync(Action<string> action)
    {
        var name = SelectedRow!.Name;
        return _runner.RunInBackgroundAsync(() => action(name));
    }

    private static TableColumn<ServiceRowViewModel> Text(string key, double width, Func<ServiceRowViewModel, string> value, bool canHide = true) =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => string.Compare(value(a), value(b), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase),
            canHide: canHide);
}

public sealed class ServiceRowViewModel(ServiceInfo info) : ObservableObject
{
    public ServiceInfo Info { get; private set; } = info;

    public string Name => Info.Name;
    public string PidText => Info.Pid > 0 ? Info.Pid.ToString(CultureInfo.CurrentCulture) : string.Empty;
    public string DisplayName => Info.DisplayName;
    public string StatusText => Strings.Get($"ServiceState{Info.State}");
    public string Group => Info.Group ?? string.Empty;

    public void Update(ServiceInfo info)
    {
        if (info != Info)
        {
            Info = info;
            OnPropertyChanged(string.Empty); // All properties derive from Info.
        }
    }

    /// <summary>Used by the list view as the row's accessible name.</summary>
    public override string ToString() => Name;
}
