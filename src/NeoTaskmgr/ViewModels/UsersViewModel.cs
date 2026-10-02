using System.Collections.ObjectModel;
using System.Globalization;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.Core.Users;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>The Users page: the signed-in users with the usage of their processes, expandable to the processes.</summary>
public sealed class UsersViewModel : ObservableObject
{
    private readonly ProcessCommandRunner _runner;
    private readonly TableColumn<IUsageRow> _cpuColumn;
    private readonly TableColumn<IUsageRow> _memoryColumn;
    private readonly TableColumn<IUsageRow> _diskColumn;
    private readonly TableColumn<IUsageRow> _networkColumn;
    private readonly TableColumn<IUsageRow> _gpuColumn;

    private Dictionary<int, UserRowViewModel> _userRows = []; // By session ID.
    private Dictionary<ProcessKey, ProcessGroupRowViewModel> _processRows = [];
    private string _search = string.Empty;
    private object? _selectedRow;
    private bool _isSyncingRows;

    public UsersViewModel(ProcessCommandRunner runner)
    {
        _runner = runner;

        // The order must match the cells in the row templates of UsersPage.xaml. The session columns only sort users.
        _cpuColumn = Number("Cpu", 80, r => r.CpuPercent);
        _memoryColumn = Number("Memory", 110, r => r.Memory);
        _diskColumn = Number("Disk", 90, r => r.DiskBytesPerSecond);
        _networkColumn = Number("Network", 90, r => r.NetworkBytesPerSecond ?? -1);
        _gpuColumn = Number("Gpu", 80, r => r.GpuPercent, isVisible: false);
        Columns = new TableColumns("Users",
        [
            Text("User", 280, r => r.Name, canHide: false),
            new TableColumn<IUsageRow>("Id", Strings.Get("ColumnId"), 60, (a, b) => SessionOf(a).CompareTo(SessionOf(b)), isNumeric: true, isVisible: false),
            Text("Session", 100, r => (r as UserRowViewModel)?.SessionName ?? string.Empty, isVisible: false),
            Text("ClientName", 120, r => (r as UserRowViewModel)?.ClientName ?? string.Empty, isVisible: false),
            Text("Status", 120, r => r.StatusSortText),
            _cpuColumn,
            _memoryColumn,
            _diskColumn,
            _networkColumn,
            _gpuColumn,
        ]);

        DisconnectCommand = new RelayCommand(() => _ = DisconnectAsync(), () => SelectedUser is { IsDisconnected: false });
        SignOutCommand = new RelayCommand(() => _ = SignOutAsync(), () => SelectedUser is not null);
        ManageUserAccountsCommand = new RelayCommand(() => _ = _runner.RunAsync(UserActions.ManageUserAccounts));
        RunNewTaskCommand = new RelayCommand(() => _ = _runner.RunNewTaskAsync());
        ToggleExpandedCommand = new RelayCommand(() => ToggleExpanded(SelectedUser!), () => SelectedUser is { CanExpand: true });
        EndTaskCommand = new RelayCommand(() => _ = _runner.RunForEachAsync([SelectedProcess!.Root.Key], ProcessActions.End), () => SelectedProcess is not null);
        GoToDetailsCommand = new RelayCommand(() => GoToDetailsRequested?.Invoke(SelectedProcess!.Root.Counters.Pid), () => SelectedProcess is not null);
        OpenFileLocationCommand = new RelayCommand(() => _ = _runner.OpenFileLocationAsync(SelectedProcess!.ImagePath!), () => SelectedProcess?.ImagePath is not null);
        SearchOnlineCommand = new RelayCommand(() => _ = _runner.SearchOnlineAsync(SelectedProcess!.Root), () => SelectedProcess is not null);
        PropertiesCommand = new RelayCommand(() => _ = _runner.ShowPropertiesAsync(SelectedProcess!.ImagePath!), () => SelectedProcess?.ImagePath is not null);

        Columns.SortChanged += (_, _) => Refresh();
        Columns.Sort(Columns[0]);
    }

    /// <summary>Asks the window to show the process (by PID) on the Details page.</summary>
    public event Action<int>? GoToDetailsRequested;

    public TableColumns Columns { get; }

    /// <summary>Users, each followed by its processes while expanded, in display order.</summary>
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

    public bool IsUserSelected => SelectedUser is not null;
    public bool IsProcessSelected => SelectedProcess is not null;
    public string ToggleExpandedText => Strings.Get(SelectedUser?.IsExpanded == true ? "Collapse" : "Expand");

    public RelayCommand DisconnectCommand { get; }
    public RelayCommand SignOutCommand { get; }
    public RelayCommand ManageUserAccountsCommand { get; }
    public RelayCommand RunNewTaskCommand { get; }
    public RelayCommand ToggleExpandedCommand { get; }
    public RelayCommand EndTaskCommand { get; }
    public RelayCommand GoToDetailsCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public RelayCommand SearchOnlineCommand { get; }
    public RelayCommand PropertiesCommand { get; }

    private UserRowViewModel? SelectedUser => SelectedRow as UserRowViewModel;
    private ProcessGroupRowViewModel? SelectedProcess => SelectedRow as ProcessGroupRowViewModel;

    /// <summary>Updates the rows from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot snapshot)
    {
        var totalMemory = snapshot.Performance.Memory.TotalPhysical;
        var servicesByPid = snapshot.Services.Where(s => s.Pid != 0).ToLookup(s => s.Pid);
        ProcessesViewModel.UpdateSummaries(snapshot.Performance, _cpuColumn, _memoryColumn, _diskColumn, _networkColumn, _gpuColumn);

        // Rebuilding the dictionaries drops rows of signed-out users and exited processes; existing rows keep their state.
        var processesBySession = UserProcesses.Group(snapshot.Users, snapshot.Processes.Processes);
        var userRows = new Dictionary<int, UserRowViewModel>();
        var processRows = new Dictionary<ProcessKey, ProcessGroupRowViewModel>();
        foreach (var session in snapshot.Users)
        {
            var processes = processesBySession[session.SessionId];
            var row = _userRows.GetValueOrDefault(session.SessionId) ?? new UserRowViewModel(session, ToggleExpanded);
            row.Update(session, processes, totalMemory);
            userRows[session.SessionId] = row;

            foreach (var process in processes)
            {
                var child = _processRows.GetValueOrDefault(process.Key) ?? new ProcessGroupRowViewModel(isChild: true, _ => { });
                child.Update([process], default, totalMemory, ProcessesViewModel.GetServiceHostName(process, servicesByPid));
                processRows[process.Key] = child;
            }
        }

        _userRows = userRows;
        _processRows = processRows;
        Refresh();
    }

    public void ApplySearch(string text)
    {
        _search = text.Trim();
        Refresh();
    }

    /// <summary>Expands or collapses the selected user (from the keyboard). Returns whether anything changed.</summary>
    public bool SetSelectedExpanded(bool isExpanded)
    {
        if (SelectedUser is not { CanExpand: true } row || row.IsExpanded == isExpanded)
        {
            return false;
        }

        ToggleExpanded(row);
        return true;
    }

    private void ToggleExpanded(UserRowViewModel row)
    {
        row.IsExpanded = !row.IsExpanded;
        Refresh();
    }

    private void Refresh()
    {
        var column = (TableColumn<IUsageRow>?)Columns.SortColumn;
        var direction = Columns.SortDescending ? -1 : 1;
        int Compare(IUsageRow a, IUsageRow b) => column is null ? 0 : column.Compare(a, b) * direction;

        var users = _userRows.Values.Where(u => MatchesSearch(u) || u.Processes.Any(MatchesSearch)).ToList();
        users.Sort((a, b) => Compare(a, b) is var result && result != 0 ? result : a.Session.SessionId.CompareTo(b.Session.SessionId));
        var rows = new List<object>();
        foreach (var user in users)
        {
            rows.Add(user);
            // While searching, a user that matches only through their processes shows the matching ones.
            var showMatchingChildren = _search.Length > 0 && !MatchesSearch(user);
            if (user.IsExpanded || showMatchingChildren)
            {
                var children = user.Processes.Where(p => !showMatchingChildren || MatchesSearch(p)).Select(p => _processRows[p.Key]).ToList();
                children.Sort((a, b) => Compare(a, b) is var result && result != 0 ? result : a.Root.Counters.Pid.CompareTo(b.Root.Counters.Pid));
                rows.AddRange(children);
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
            UpdateCommands(); // The selected row's state may have changed.
        }
    }

    private bool MatchesSearch(UserRowViewModel user) =>
        _search.Length == 0 ||
        user.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        user.Session.UserName.Contains(_search, StringComparison.CurrentCultureIgnoreCase);

    private bool MatchesSearch(ProcessInfo process) =>
        _search.Length == 0 ||
        process.DisplayName.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        process.Counters.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        process.Counters.Pid.ToString(CultureInfo.InvariantCulture).StartsWith(_search, StringComparison.Ordinal);

    private void UpdateCommands()
    {
        foreach (var command in new[]
                 {
                     DisconnectCommand, SignOutCommand, ToggleExpandedCommand, EndTaskCommand, GoToDetailsCommand, OpenFileLocationCommand,
                     SearchOnlineCommand, PropertiesCommand,
                 })
        {
            command.NotifyCanExecuteChanged();
        }

        OnPropertyChanged(nameof(IsUserSelected));
        OnPropertyChanged(nameof(IsProcessSelected));
        OnPropertyChanged(nameof(ToggleExpandedText));
    }

    private async Task DisconnectAsync()
    {
        if (SelectedUser is { } user && await _runner.Dialogs.ConfirmAsync(
                Strings.Format("DisconnectConfirmTitle", user.Name), Strings.Get("DisconnectConfirmMessage"), Strings.Get("DisconnectConfirmButton")))
        {
            await _runner.RunAsync(() => UserActions.Disconnect(user.Session.SessionId));
        }
    }

    private async Task SignOutAsync()
    {
        if (SelectedUser is { } user && await _runner.Dialogs.ConfirmAsync(
                Strings.Format("SignOutConfirmTitle", user.Name), Strings.Get("SignOutConfirmMessage"), Strings.Get("SignOutConfirmButton")))
        {
            await _runner.RunAsync(() => UserActions.SignOut(user.Session.SessionId));
        }
    }

    private static int SessionOf(IUsageRow row) => (row as UserRowViewModel)?.Session.SessionId ?? 0;

    private static TableColumn<IUsageRow> Text(string key, double width, Func<IUsageRow, string> value, bool isVisible = true, bool canHide = true) =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => string.Compare(value(a), value(b), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase),
            isVisible: isVisible, canHide: canHide);

    private static TableColumn<IUsageRow> Number<T>(string key, double width, Func<IUsageRow, T> value, bool isVisible = true)
        where T : IComparable<T> =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => value(a).CompareTo(value(b)), isNumeric: true, isVisible: isVisible);
}
