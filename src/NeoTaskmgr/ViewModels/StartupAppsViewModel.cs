using System.Collections.ObjectModel;
using System.Globalization;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Shell;
using NeoTaskmgr.Core.Startup;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>
/// The Startup apps page: programs started at sign-in, which can be enabled or disabled. The list rarely changes,
/// so it is read when the page opens and after changes, not on every sample.
/// </summary>
public sealed class StartupAppsViewModel : ObservableObject
{
    private readonly StartupProvider _provider;
    private readonly ProcessCommandRunner _runner;
    private Dictionary<(StartupSource, string), StartupRowViewModel> _rows = [];
    private string _search = string.Empty;
    private StartupRowViewModel? _selectedRow;
    private bool _isSyncingRows;

    public StartupAppsViewModel(StartupProvider provider, ProcessCommandRunner runner)
    {
        _provider = provider;
        _runner = runner;

        // The order must match the cells in the row template of StartupAppsPage.xaml.
        Columns = new TableColumns(
        [
            Text("Name", 280, r => r.Name, canHide: false),
            Text("Publisher", 200, r => r.Publisher),
            Text("Status", 100, r => r.StatusText),
            Text("StartupImpact", 130, r => r.ImpactText),
            Text("StartupType", 120, r => r.TypeText, isVisible: false),
            Text("CommandLine", 400, r => r.Command, isVisible: false),
            Text("Location", 400, r => r.Location, isVisible: false),
        ]);

        EnableCommand = new RelayCommand(() => _ = SetEnabledAsync(true), () => SelectedRow is { Entry.IsEnabled: false });
        DisableCommand = new RelayCommand(() => _ = SetEnabledAsync(false), () => SelectedRow is { Entry.IsEnabled: true });
        OpenFileLocationCommand = new RelayCommand(() => _ = _runner.OpenFileLocationAsync(SelectedRow!.Entry.TargetPath!), HasTarget);
        SearchOnlineCommand = new RelayCommand(
            () => _ = _runner.RunAsync(() => ShellActions.SearchOnline(SelectedRow!.Name)), () => SelectedRow is not null);
        PropertiesCommand = new RelayCommand(() => _ = _runner.ShowPropertiesAsync(SelectedRow!.Entry.TargetPath!), HasTarget);

        Columns.SortChanged += (_, _) => Refresh();
        Columns.Sort(Columns[0]);
    }

    public TableColumns Columns { get; }

    public ObservableCollection<StartupRowViewModel> Rows { get; } = [];

    public StartupRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            // While rows are reordered the list view may briefly report no selection; keep the selected entry.
            if (!_isSyncingRows && SetProperty(ref _selectedRow, value))
            {
                UpdateCommands();
            }
        }
    }

    public RelayCommand EnableCommand { get; }
    public RelayCommand DisableCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public RelayCommand SearchOnlineCommand { get; }
    public RelayCommand PropertiesCommand { get; }

    /// <summary>Reads the startup entries again. Must be called on the UI thread.</summary>
    public async Task ReloadAsync()
    {
        // Reading version info of the programs takes a moment; keep the UI responsive.
        var entries = await Task.Run(_provider.Read);
        var rows = new Dictionary<(StartupSource, string), StartupRowViewModel>();
        foreach (var entry in entries)
        {
            var key = (entry.Source, entry.ApprovalName);
            var row = _rows.GetValueOrDefault(key) ?? new StartupRowViewModel(entry);
            row.Update(entry);
            rows[key] = row;
        }

        _rows = rows;
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
        if (Columns.SortColumn is TableColumn<StartupRowViewModel> sortColumn)
        {
            var direction = Columns.SortDescending ? -1 : 1;
            rows.Sort((a, b) =>
            {
                var result = sortColumn.Compare(a, b) * direction;
                return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
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
            UpdateCommands(); // The selected entry may have been enabled or disabled.
        }
    }

    private bool MatchesSearch(StartupRowViewModel row) =>
        _search.Length == 0 ||
        row.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        row.Publisher.Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
        row.Command.Contains(_search, StringComparison.CurrentCultureIgnoreCase);

    private bool HasTarget() => SelectedRow?.Entry.TargetPath is not null;

    private void UpdateCommands()
    {
        foreach (var command in new[] { EnableCommand, DisableCommand, OpenFileLocationCommand, SearchOnlineCommand, PropertiesCommand })
        {
            command.NotifyCanExecuteChanged();
        }
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        var entry = SelectedRow!.Entry;
        await _runner.RunAsync(() => _provider.SetEnabled(entry, enabled));
        await ReloadAsync();
    }

    private static TableColumn<StartupRowViewModel> Text(
        string key, double width, Func<StartupRowViewModel, string> value, bool isVisible = true, bool canHide = true) =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => string.Compare(value(a), value(b), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase),
            isVisible: isVisible, canHide: canHide);
}

public sealed class StartupRowViewModel(StartupEntry entry) : ObservableObject
{
    public StartupEntry Entry { get; private set; } = entry;

    public string Name => Entry.DisplayName;
    public string? TargetPath => Entry.TargetPath;
    public string Publisher => Entry.Publisher ?? string.Empty;
    public string StatusText => Strings.Get(Entry.IsEnabled ? "Enabled" : "Disabled");

    /// <summary>Windows computes the impact from its own diagnostics, which apps cannot read.</summary>
    public string ImpactText => Strings.Get("StartupImpactNotMeasured");

    public string TypeText => Strings.Get(Entry.IsInRegistry ? "StartupTypeRegistry" : "StartupTypeFolder");
    public string Command => Entry.Command;
    public string Location => Entry.Location;

    public void Update(StartupEntry entry)
    {
        if (entry != Entry)
        {
            Entry = entry;
            OnPropertyChanged(string.Empty); // All properties derive from the entry.
        }
    }

    /// <summary>Used by the list view as the row's accessible name.</summary>
    public override string ToString() => Name;
}
