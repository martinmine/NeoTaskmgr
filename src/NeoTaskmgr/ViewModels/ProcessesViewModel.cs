using System.Collections.ObjectModel;
using System.Globalization;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.Core.Shell;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>The Processes page: apps, background processes and Windows processes, with apps expandable to their processes.</summary>
public sealed class ProcessesViewModel : ObservableObject
{
    private static readonly string WindowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private readonly ProcessCommandRunner _runner;
    private readonly TableColumn<ProcessGroupRowViewModel> _cpuColumn;
    private readonly TableColumn<ProcessGroupRowViewModel> _memoryColumn;
    private readonly SectionRowViewModel[] _sections = Enum.GetValues<ProcessCategory>().Select(c => new SectionRowViewModel(c)).ToArray();

    private Dictionary<ProcessKey, ProcessGroupRowViewModel> _groupRows = []; // By the group's root process.
    private Dictionary<ProcessKey, ProcessGroupRowViewModel> _childRows = []; // By process.
    private string _search = string.Empty;
    private object? _selectedRow;
    private bool _isSyncingRows;

    public ProcessesViewModel(ProcessCommandRunner runner)
    {
        _runner = runner;

        // The order must match the cells in the row template of ProcessesPage.xaml.
        _cpuColumn = Number("Cpu", 80, r => r.CpuPercent);
        _memoryColumn = Number("Memory", 110, r => r.Memory);
        Columns = new TableColumns(
        [
            Text("Name", 280, r => r.Name, canHide: false),
            Text("Status", 120, r => r.StatusText),
            Text("Publisher", 160, r => r.Publisher, isVisible: false),
            new TableColumn<ProcessGroupRowViewModel>("Pid", Strings.Get("ColumnPid"), 70, (a, b) => a.Root.Counters.Pid.CompareTo(b.Root.Counters.Pid),
                isNumeric: true, isVisible: false),
            Text("ProcessName", 150, r => r.ProcessName, isVisible: false),
            Text("CommandLine", 300, r => r.CommandLine, isVisible: false),
            _cpuColumn,
            _memoryColumn,
        ]);
        EndTaskCommand = new RelayCommand(() => _ = EndTaskAsync(), () => SelectedProcess is not null);
        RestartExplorerCommand = new RelayCommand(() => _ = RestartExplorerAsync(), () => SelectedProcess?.IsExplorer == true);
        ToggleEfficiencyModeCommand = new RelayCommand(
            () => _ = _runner.SetEfficiencyModeAsync(SelectedProcess!.Root.DisplayName, SelectedProcess.Processes.Select(p => p.Key), !SelectedProcess.IsEfficiencyMode),
            () => SelectedProcess is { Category: not ProcessCategory.Windows });
        SuspendCommand = new RelayCommand(
            () => _ = _runner.RunForEachAsync(SelectedProcess!.KeysForEnding, ProcessActions.Suspend), () => SelectedProcess is { IsSuspended: false });
        // A thread blocked in the kernel only shows as suspended once its wait ends, so resuming is always offered.
        // Resuming a process that is not suspended has no effect.
        ResumeCommand = new RelayCommand(
            () => _ = _runner.RunForEachAsync(SelectedProcess!.Processes.Select(p => p.Key), ProcessActions.Resume), () => SelectedProcess is not null);
        SwitchToCommand = new RelayCommand(() => _ = _runner.RunAsync(() => ShellActions.SwitchTo(SelectedProcess!.Window!.Handle)),
            () => SelectedProcess?.Window is not null);
        CreateDumpFileCommand = new RelayCommand(() => _ = _runner.CreateDumpFileAsync(SelectedProcess!.Root.Key, SelectedProcess.Root.Counters.Name),
            () => SelectedProcess is not null);
        GoToDetailsCommand = new RelayCommand(() => GoToDetailsRequested?.Invoke(SelectedProcess!.Root.Key), () => SelectedProcess is not null);
        OpenFileLocationCommand = new RelayCommand(() => _ = _runner.OpenFileLocationAsync(SelectedProcess!.ImagePath!), () => SelectedProcess?.ImagePath is not null);
        SearchOnlineCommand = new RelayCommand(() => _ = _runner.SearchOnlineAsync(SelectedProcess!.Root), () => SelectedProcess is not null);
        PropertiesCommand = new RelayCommand(() => _ = _runner.ShowPropertiesAsync(SelectedProcess!.ImagePath!), () => SelectedProcess?.ImagePath is not null);
        RunNewTaskCommand = new RelayCommand(() => _ = _runner.RunNewTaskAsync());

        Columns.SortChanged += (_, _) => Refresh();
        Columns.Sort(Columns[0]);
    }

    /// <summary>Asks the window to show the process on the Details page.</summary>
    public event Action<ProcessKey>? GoToDetailsRequested;

    public TableColumns Columns { get; }

    /// <summary>Section headers and process rows, in display order.</summary>
    public ObservableCollection<object> Rows { get; } = [];

    public object? SelectedRow
    {
        get => _selectedRow;
        set
        {
            // While rows are reordered the list view may briefly report no selection; keep the selected row.
            if (!_isSyncingRows && SetProperty(ref _selectedRow, value))
            {
                UpdateCommands();
            }
        }
    }

    public bool IsSelectedEfficiencyMode => SelectedProcess?.IsEfficiencyMode == true;
    public bool IsExplorerSelected => SelectedProcess?.IsExplorer == true;

    public RelayCommand EndTaskCommand { get; }
    public RelayCommand RestartExplorerCommand { get; }
    public RelayCommand ToggleEfficiencyModeCommand { get; }
    public RelayCommand SuspendCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand SwitchToCommand { get; }
    public RelayCommand CreateDumpFileCommand { get; }
    public RelayCommand GoToDetailsCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public RelayCommand SearchOnlineCommand { get; }
    public RelayCommand PropertiesCommand { get; }
    public RelayCommand RunNewTaskCommand { get; }

    private ProcessGroupRowViewModel? SelectedProcess => SelectedRow as ProcessGroupRowViewModel;

    /// <summary>Updates the rows from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot snapshot)
    {
        var processes = snapshot.Processes.Processes;
        var totalMemory = snapshot.Memory.TotalPhysical;
        var idleCpu = processes.FirstOrDefault(p => p.Counters.Pid == 0)?.CpuPercent ?? 0;
        _cpuColumn.Summary = Format.WholePercent(100 - idleCpu);
        _memoryColumn.Summary = Format.WholePercent(snapshot.Memory.UsedPercent);

        // Rebuilding the dictionaries drops rows of exited processes; existing rows are reused to keep their state.
        var groupRows = new Dictionary<ProcessKey, ProcessGroupRowViewModel>();
        var childRows = new Dictionary<ProcessKey, ProcessGroupRowViewModel>();
        foreach (var group in ProcessGrouper.Group(processes, WindowsDirectory))
        {
            var row = _groupRows.GetValueOrDefault(group.Root.Key) ?? new ProcessGroupRowViewModel(isChild: false, ToggleExpanded);
            row.Update(group.Processes, group.Category, totalMemory);
            groupRows[group.Root.Key] = row;

            if (group.Processes.Count > 1)
            {
                foreach (var process in group.Processes)
                {
                    var child = _childRows.GetValueOrDefault(process.Key) ?? new ProcessGroupRowViewModel(isChild: true, ToggleExpanded);
                    child.Update([process], group.Category, totalMemory);
                    childRows[process.Key] = child;
                }
            }
        }

        _groupRows = groupRows;
        _childRows = childRows;
        Refresh();
    }

    public void ApplySearch(string text)
    {
        _search = text.Trim();
        Refresh();
    }

    private void ToggleExpanded(ProcessGroupRowViewModel row)
    {
        row.IsExpanded = !row.IsExpanded;
        Refresh();
    }

    private void Refresh()
    {
        var comparison = CreateComparison();
        var rows = new List<object>();
        foreach (var section in _sections)
        {
            var groups = _groupRows.Values.Where(g => g.Category == section.Category && g.Processes.Any(MatchesSearch)).ToList();
            if (groups.Count == 0)
            {
                continue;
            }

            groups.Sort(comparison);
            section.Update(groups.Count);
            rows.Add(section);
            foreach (var group in groups)
            {
                rows.Add(group);
                // While searching, a group that matches only through its processes shows the matching ones.
                var showMatchingChildren = _search.Length > 0 && !MatchesSearch(group.Root);
                if (group.CanExpand && (group.IsExpanded || showMatchingChildren))
                {
                    var children = group.Processes.Where(p => !showMatchingChildren || MatchesSearch(p)).Select(p => _childRows[p.Key]).ToList();
                    children.Sort(comparison);
                    rows.AddRange(children);
                }
            }
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
            UpdateCommands(); // The selected process's state may have changed.
        }
    }

    private Comparison<ProcessGroupRowViewModel> CreateComparison()
    {
        var column = (TableColumn<ProcessGroupRowViewModel>?)Columns.SortColumn;
        var direction = Columns.SortDescending ? -1 : 1;
        return (a, b) =>
        {
            var result = column is null ? 0 : column.Compare(a, b) * direction;
            return result != 0 ? result : a.Root.Counters.Pid.CompareTo(b.Root.Counters.Pid);
        };
    }

    private bool MatchesSearch(ProcessInfo process) =>
        _search.Length == 0 ||
        process.DisplayName.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        process.Counters.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        (process.Details.Publisher?.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
        process.Counters.Pid.ToString(CultureInfo.InvariantCulture).StartsWith(_search, StringComparison.Ordinal);

    private void UpdateCommands()
    {
        foreach (var command in new[]
                 {
                     EndTaskCommand, RestartExplorerCommand, ToggleEfficiencyModeCommand, SuspendCommand, ResumeCommand, SwitchToCommand,
                     CreateDumpFileCommand, GoToDetailsCommand, OpenFileLocationCommand, SearchOnlineCommand, PropertiesCommand,
                 })
        {
            command.NotifyCanExecuteChanged();
        }

        OnPropertyChanged(nameof(IsSelectedEfficiencyMode));
        OnPropertyChanged(nameof(IsExplorerSelected));
    }

    private async Task EndTaskAsync()
    {
        if (SelectedProcess is not { } row)
        {
            return;
        }

        // Ending a Windows process can make the system unstable, so ask first.
        if (row.Category != ProcessCategory.Windows || await _runner.Dialogs.ConfirmAsync(
                Strings.Format("EndProcessConfirmTitle", row.Root.DisplayName), Strings.Get("EndProcessConfirmMessage"), Strings.Get("EndProcessConfirmButton")))
        {
            await _runner.RunForEachAsync(row.KeysForEnding, ProcessActions.End);
        }
    }

    private Task RestartExplorerAsync()
    {
        var explorers = SelectedProcess!.Processes.Where(p => p.Counters.Name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToList();
        return _runner.RunInBackgroundAsync(() => ProcessActions.RestartExplorer(explorers));
    }

    private static TableColumn<ProcessGroupRowViewModel> Text(string key, double width, Func<ProcessGroupRowViewModel, string> value, bool isVisible = true, bool canHide = true) =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => string.Compare(value(a), value(b), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase),
            isVisible: isVisible, canHide: canHide);

    private static TableColumn<ProcessGroupRowViewModel> Number<T>(string key, double width, Func<ProcessGroupRowViewModel, T> value)
        where T : IComparable<T> =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => value(a).CompareTo(value(b)), isNumeric: true);
}
