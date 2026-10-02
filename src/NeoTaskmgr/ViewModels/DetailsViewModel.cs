using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>The Details page: a flat, sortable and searchable list of all processes.</summary>
public sealed class DetailsViewModel : ObservableObject
{
    private readonly ProcessCommandRunner _runner;
    private readonly Dictionary<ProcessKey, ProcessRowViewModel> _rows = [];
    private ProcessSnapshot _snapshot = new([]);
    private HashSet<int> _servicePids = [];
    private string _search = string.Empty;
    private ProcessRowViewModel? _selectedRow;
    private bool _isSyncingRows;

    public DetailsViewModel(ProcessCommandRunner runner)
    {
        _runner = runner;

        // The order must match the cells in the row template of DetailsPage.xaml.
        Columns = new TableColumns("Details",
        [
            Text("Name", 190, r => r.Name, canHide: false),
            Number("Pid", 60, r => r.Key.Pid),
            Text("Status", 80, r => r.StatusText),
            Text("UserName", 95, r => r.UserName),
            Number("SessionId", 80, r => r.Info.Counters.SessionId, isVisible: false),
            Number("Cpu", 45, r => r.Info.CpuPercent),
            Number("CpuTime", 90, r => r.Info.Counters.CpuTime, isVisible: false),
            Number("MemoryPrivateWorkingSet", 105, r => r.Info.Counters.PrivateWorkingSet),
            Number("WorkingSet", 110, r => r.Info.Counters.WorkingSet, isVisible: false),
            Number("PeakWorkingSet", 130, r => r.Info.Counters.PeakWorkingSet, isVisible: false),
            Number("CommitSize", 110, r => r.Info.Counters.CommitSize, isVisible: false),
            Number("Handles", 80, r => r.Info.Counters.HandleCount, isVisible: false),
            Number("Threads", 80, r => r.Info.Counters.ThreadCount, isVisible: false),
            new TableColumn<ProcessRowViewModel>("BasePriority", Strings.Get("ColumnBasePriority"), 110,
                (a, b) => a.Info.Counters.BasePriority.CompareTo(b.Info.Counters.BasePriority), isVisible: false),
            Text("Architecture", 92, r => r.Architecture),
            Text("Elevated", 80, r => r.ElevatedText, isVisible: false),
            Text("Description", 238, r => r.Description),
            Text("ImagePath", 300, r => r.ImagePath, isVisible: false),
            Text("CommandLine", 400, r => r.CommandLine, isVisible: false),
            Number("Disk", 90, r => r.Info.DiskBytesPerSecond, isVisible: false),
            Number("Network", 90, r => r.Info.NetworkBytesPerSecond ?? -1, isVisible: false),
            Number("Gpu", 70, r => r.Info.GpuPercent, isVisible: false),
        ]);
        EndTaskCommand = new RelayCommand(() => _ = EndAsync(tree: false), HasSelection);
        EndProcessTreeCommand = new RelayCommand(() => _ = EndAsync(tree: true), HasSelection);
        ToggleEfficiencyModeCommand = new RelayCommand(
            () => _ = _runner.SetEfficiencyModeAsync(SelectedRow!.Name, [SelectedRow.Key], !SelectedRow.Info.IsEfficiencyMode), HasSelection);
        SetPriorityCommand = new RelayCommand<string>(p => _ = SetPriorityAsync(Enum.Parse<ProcessPriorityClass>(p!)), _ => HasSelection());
        SetAffinityCommand = new RelayCommand(() => _ = SetAffinityAsync(), HasSelection);
        CreateDumpFileCommand = new RelayCommand(() => _ = _runner.CreateDumpFileAsync(SelectedRow!.Key, SelectedRow.Name), HasSelection);
        OpenFileLocationCommand = new RelayCommand(() => _ = _runner.OpenFileLocationAsync(SelectedRow!.ImagePath), HasImagePath);
        SearchOnlineCommand = new RelayCommand(() => _ = _runner.SearchOnlineAsync(SelectedRow!.Info), HasSelection);
        PropertiesCommand = new RelayCommand(() => _ = _runner.ShowPropertiesAsync(SelectedRow!.ImagePath), HasImagePath);
        RunNewTaskCommand = new RelayCommand(() => _ = _runner.RunNewTaskAsync());
        GoToServicesCommand = new RelayCommand(() => GoToServicesRequested?.Invoke(SelectedRow!.Key.Pid),
            () => SelectedRow is not null && _servicePids.Contains(SelectedRow.Key.Pid));

        Columns.SortChanged += (_, _) => Refresh();
        Columns.Sort(Columns[0]);
    }

    public TableColumns Columns { get; }

    /// <summary>The rows matching the search, in sort order.</summary>
    public ObservableCollection<ProcessRowViewModel> Rows { get; } = [];

    public ProcessRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            // While rows are reordered the list view may briefly report no selection; keep the selected process.
            if (!_isSyncingRows && SetProperty(ref _selectedRow, value))
            {
                UpdateCommands();
            }
        }
    }

    public bool IsSelectedEfficiencyMode => SelectedRow?.Info.IsEfficiencyMode == true;

    public RelayCommand EndTaskCommand { get; }
    public RelayCommand EndProcessTreeCommand { get; }
    public RelayCommand ToggleEfficiencyModeCommand { get; }
    public RelayCommand<string> SetPriorityCommand { get; }
    public RelayCommand SetAffinityCommand { get; }
    public RelayCommand CreateDumpFileCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public RelayCommand SearchOnlineCommand { get; }
    public RelayCommand PropertiesCommand { get; }
    public RelayCommand RunNewTaskCommand { get; }
    public RelayCommand GoToServicesCommand { get; }

    /// <summary>Asks the window to show the services hosted by the process (by PID).</summary>
    public event Action<int>? GoToServicesRequested;

    /// <summary>Selects the process (clearing any search that hides it), e.g. for "Go to details".</summary>
    public void Select(int pid)
    {
        if (_rows.Values.FirstOrDefault(r => r.Key.Pid == pid) is { } row)
        {
            ApplySearch(string.Empty);
            SelectedRow = row;
        }
    }

    /// <summary>Updates the rows from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot systemSnapshot)
    {
        var snapshot = systemSnapshot.Processes;
        _snapshot = snapshot;
        _servicePids = systemSnapshot.Services.Where(s => s.Pid != 0).Select(s => s.Pid).ToHashSet();
        var alive = new HashSet<ProcessKey>(snapshot.Processes.Count);
        foreach (var process in snapshot.Processes)
        {
            alive.Add(process.Key);
            if (_rows.TryGetValue(process.Key, out var row))
            {
                row.Update(process);
            }
            else
            {
                _rows[process.Key] = new ProcessRowViewModel(process);
            }
        }

        foreach (var exited in _rows.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            _rows.Remove(exited);
        }

        Refresh();
    }

    public void ApplySearch(string text)
    {
        _search = text.Trim();
        Refresh();
    }

    private void Refresh()
    {
        var rows = _rows.Values.Where(MatchesSearch).ToList();
        if (Columns.SortColumn is TableColumn<ProcessRowViewModel> sortColumn)
        {
            var direction = Columns.SortDescending ? -1 : 1;
            rows.Sort((a, b) =>
            {
                var result = sortColumn.Compare(a, b) * direction;
                return result != 0 ? result : a.Key.Pid.CompareTo(b.Key.Pid);
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
            OnPropertyChanged(nameof(IsSelectedEfficiencyMode));
        }
    }

    private void UpdateCommands()
    {
        foreach (var command in new[]
                 {
                     EndTaskCommand, EndProcessTreeCommand, ToggleEfficiencyModeCommand, SetAffinityCommand, CreateDumpFileCommand,
                     OpenFileLocationCommand, SearchOnlineCommand, PropertiesCommand, GoToServicesCommand,
                 })
        {
            command.NotifyCanExecuteChanged();
        }

        SetPriorityCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSelectedEfficiencyMode));
    }

    private bool MatchesSearch(ProcessRowViewModel row) =>
        _search.Length == 0 ||
        row.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        row.Description.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        row.PidText.StartsWith(_search, StringComparison.Ordinal);

    private bool HasSelection() => SelectedRow is not null;

    private bool HasImagePath() => SelectedRow?.Info.Details.ImagePath is not null;

    private async Task EndAsync(bool tree)
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var prefix = tree ? "EndProcessTree" : "EndProcess";
        if (await _runner.Dialogs.ConfirmAsync(Strings.Format($"{prefix}ConfirmTitle", row.Name), Strings.Get($"{prefix}ConfirmMessage"), Strings.Get($"{prefix}ConfirmButton")))
        {
            var processes = _snapshot.Processes.Select(p => p.Counters).ToList();
            await _runner.RunAsync(() =>
            {
                if (tree)
                {
                    ProcessActions.EndTree(row.Key, processes);
                }
                else
                {
                    ProcessActions.End(row.Key);
                }
            });
        }
    }

    private async Task SetPriorityAsync(ProcessPriorityClass priority)
    {
        if (SelectedRow is { } row && row.Priority != priority &&
            await _runner.Dialogs.ConfirmAsync(Strings.Format("PriorityConfirmTitle", row.Name), Strings.Get("PriorityConfirmMessage"), Strings.Get("PriorityConfirmButton")))
        {
            await _runner.RunAsync(() => ProcessActions.SetPriority(row.Key, priority));
        }
    }

    private async Task SetAffinityAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        (ulong ProcessMask, ulong SystemMask)? affinity = null;
        await _runner.RunAsync(() => affinity = ProcessActions.GetAffinity(row.Key));
        if (affinity is { } current && await _runner.Dialogs.PickAffinityAsync(row.Name, current.ProcessMask, current.SystemMask) is { } mask)
        {
            await _runner.RunAsync(() => ProcessActions.SetAffinity(row.Key, mask));
        }
    }

    private static TableColumn<ProcessRowViewModel> Text(string key, double width, Func<ProcessRowViewModel, string> value, bool isVisible = true, bool canHide = true) =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => string.Compare(value(a), value(b), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase),
            isVisible: isVisible, canHide: canHide);

    private static TableColumn<ProcessRowViewModel> Number<T>(string key, double width, Func<ProcessRowViewModel, T> value, bool isVisible = true)
        where T : IComparable<T> =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => value(a).CompareTo(value(b)), isNumeric: true, isVisible: isVisible);
}
