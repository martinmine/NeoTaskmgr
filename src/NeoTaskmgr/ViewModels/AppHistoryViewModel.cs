using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core.AppHistory;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Shell;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>
/// The App history page: CPU time, network traffic and notifications per app over the last 30 days, from the usage
/// data Windows records. The data only changes about once a minute, so it is read when the page opens, not sampled.
/// </summary>
public sealed class AppHistoryViewModel : ObservableObject
{
    /// <summary>The shade of the smallest usage, so it differs clearly from none.</summary>
    private const double MinHeat = 0.4;

    private readonly AppHistoryProvider _provider;
    private readonly SettingsViewModel _settings;
    private readonly ProcessCommandRunner _runner;
    private Dictionary<string, AppHistoryRowViewModel> _rows = [];
    private string _search = string.Empty;
    private string _summary = string.Empty;
    private AppHistoryRowViewModel? _selectedRow;
    private bool _isSyncingRows;
    private int _reloadVersion;

    public AppHistoryViewModel(AppHistoryProvider provider, SettingsViewModel settings, ProcessCommandRunner runner)
    {
        _provider = provider;
        _settings = settings;
        _runner = runner;

        // The order must match the cells in the row template of AppHistoryPage.xaml.
        Columns = new TableColumns("AppHistory",
        [
            new TableColumn<AppHistoryRowViewModel>("Name", Strings.Get("ColumnName"), 280,
                (a, b) => string.Compare(a.Name, b.Name, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase), canHide: false),
            Number("CpuTime", 100, r => r.Entry.CpuTime),
            Number("Network", 100, r => r.Entry.NetworkBytes),
            Number("Notifications", 110, r => r.Entry.NotificationBytes),
            Number("Downloads", 100, r => r.Entry.BytesReceived, isVisible: false),
            Number("Uploads", 100, r => r.Entry.BytesSent, isVisible: false),
        ]);

        DeleteUsageHistoryCommand = new RelayCommand(DeleteUsageHistory);
        OpenFileLocationCommand = new RelayCommand(() => _ = _runner.OpenFileLocationAsync(SelectedRow!.ImagePath!), HasFile);
        SearchOnlineCommand = new RelayCommand(
            () => _ = _runner.RunAsync(() => ShellActions.SearchOnline(SelectedRow!.Name)), () => SelectedRow is not null);
        PropertiesCommand = new RelayCommand(() => _ = _runner.ShowPropertiesAsync(SelectedRow!.ImagePath!), HasFile);

        Columns.SortChanged += (_, _) => Refresh();
        Columns.Sort(Columns[0]);
    }

    public TableColumns Columns { get; }

    public ObservableCollection<AppHistoryRowViewModel> Rows { get; } = [];

    /// <summary>The period and accounts the usage covers, or why it could not be read.</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public AppHistoryRowViewModel? SelectedRow
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

    public RelayCommand DeleteUsageHistoryCommand { get; }
    public RelayCommand OpenFileLocationCommand { get; }
    public RelayCommand SearchOnlineCommand { get; }
    public RelayCommand PropertiesCommand { get; }

    /// <summary>Reads the usage again. Must be called on the UI thread.</summary>
    public async Task ReloadAsync()
    {
        var version = ++_reloadVersion;
        var since = _settings.Current.AppHistoryDeletedUtc ?? DateTime.MinValue;
        AppHistorySnapshot snapshot;
        Dictionary<string, (string Name, string? ImagePath)> details;
        try
        {
            // Reading a month of records and looking up package names takes a moment; keep the UI responsive.
            (snapshot, details) = await Task.Run(() =>
            {
                var snapshot = _provider.Read(since);
                var details = snapshot.Entries.ToDictionary(e => e.Key, e => (
                    e.PackageFamilyName is { } family ? AppPackages.GetDisplayName(family) ?? e.Name : e.Name,
                    e.ImagePath is { } path && File.Exists(path) ? path : null));
                return (snapshot, details);
            });
        }
        catch (Win32Exception e)
        {
            Summary = Strings.Format("AppHistoryUnavailable", e.Message);
            _rows = [];
            Refresh();
            return;
        }

        if (version != _reloadVersion)
        {
            return; // A newer reload started meanwhile, for example after deleting the history.
        }

        var rows = new Dictionary<string, AppHistoryRowViewModel>();
        foreach (var entry in snapshot.Entries)
        {
            var row = _rows.GetValueOrDefault(entry.Key) ?? new AppHistoryRowViewModel(entry);
            var (name, imagePath) = details[entry.Key];
            row.Update(entry, name, imagePath);
            rows[entry.Key] = row;
        }

        UpdateHeat(rows.Values);
        _rows = rows;
        var date = snapshot.SinceUtc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
        Summary = Strings.Format(snapshot.IncludesSystemAccounts ? "AppHistorySummary" : "AppHistorySummaryCurrentUser", date);
        Refresh();
    }

    public void ApplySearch(string text)
    {
        _search = text.Trim();
        Refresh();
    }

    private void DeleteUsageHistory()
    {
        _settings.DeleteAppHistory();
        _ = ReloadAsync();
    }

    /// <summary>
    /// Shades the cells like Task Manager: any usage stands out from none, and larger values are darker. A log scale
    /// keeps small but real usage visible next to apps that used a thousand times more.
    /// </summary>
    private static void UpdateHeat(ICollection<AppHistoryRowViewModel> rows)
    {
        var maxCpu = rows.Select(r => r.Entry.CpuTime.TotalSeconds).DefaultIfEmpty().Max();
        var maxNetwork = rows.Select(r => (double)r.Entry.NetworkBytes).DefaultIfEmpty().Max();
        var maxNotifications = rows.Select(r => (double)r.Entry.NotificationBytes).DefaultIfEmpty().Max();
        var maxDownloads = rows.Select(r => (double)r.Entry.BytesReceived).DefaultIfEmpty().Max();
        var maxUploads = rows.Select(r => (double)r.Entry.BytesSent).DefaultIfEmpty().Max();
        foreach (var row in rows)
        {
            row.SetHeat(
                Heat(row.Entry.CpuTime.TotalSeconds, maxCpu),
                Heat(row.Entry.NetworkBytes, maxNetwork),
                Heat(row.Entry.NotificationBytes, maxNotifications),
                Heat(row.Entry.BytesReceived, maxDownloads),
                Heat(row.Entry.BytesSent, maxUploads));
        }
    }

    private static double Heat(double value, double max) =>
        value <= 0 ? 0 : MinHeat + ((1 - MinHeat) * Math.Log(1 + value) / Math.Log(1 + max));

    private void Refresh()
    {
        var rows = _rows.Values.Where(MatchesSearch).ToList();
        if (Columns.SortColumn is TableColumn<AppHistoryRowViewModel> sortColumn)
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
            UpdateCommands(); // The selected program's file may have been removed.
        }
    }

    private bool MatchesSearch(AppHistoryRowViewModel row) =>
        _search.Length == 0 || row.Name.Contains(_search, StringComparison.CurrentCultureIgnoreCase);

    private bool HasFile() => SelectedRow?.ImagePath is not null;

    private void UpdateCommands()
    {
        foreach (var command in new[] { OpenFileLocationCommand, SearchOnlineCommand, PropertiesCommand })
        {
            command.NotifyCanExecuteChanged();
        }
    }

    private static TableColumn<AppHistoryRowViewModel> Number<T>(
        string key, double width, Func<AppHistoryRowViewModel, T> value, bool isVisible = true)
        where T : IComparable<T> =>
        new(key, Strings.Get($"Column{key}"), width, (a, b) => value(a).CompareTo(value(b)), isNumeric: true, isVisible: isVisible);
}

public sealed class AppHistoryRowViewModel(AppHistoryEntry entry) : ObservableObject
{
    private string _name = entry.Name;
    private string? _imagePath;
    private double _cpuHeat;
    private double _networkHeat;
    private double _notificationsHeat;
    private double _downloadsHeat;
    private double _uploadsHeat;

    public AppHistoryEntry Entry { get; private set; } = entry;

    /// <summary>The display name of packaged apps; the file name of programs.</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>The program's file, when it still exists; packaged apps and services have none.</summary>
    public string? ImagePath
    {
        get => _imagePath;
        private set => SetProperty(ref _imagePath, value);
    }

    public string? PackageFamilyName => Entry.PackageFamilyName;

    public string CpuTimeText => Format.Duration(Entry.CpuTime);
    public string NetworkText => Format.UsageMegabytes(Entry.NetworkBytes);
    public string NotificationsText => Format.UsageMegabytes(Entry.NotificationBytes);
    public string DownloadsText => Format.UsageMegabytes(Entry.BytesReceived);
    public string UploadsText => Format.UsageMegabytes(Entry.BytesSent);

    public double CpuHeat => _cpuHeat;
    public double NetworkHeat => _networkHeat;
    public double NotificationsHeat => _notificationsHeat;
    public double DownloadsHeat => _downloadsHeat;
    public double UploadsHeat => _uploadsHeat;

    public void Update(AppHistoryEntry entry, string name, string? imagePath)
    {
        Name = name;
        ImagePath = imagePath;
        if (entry != Entry)
        {
            Entry = entry;
            OnPropertyChanged(string.Empty); // The texts derive from the entry.
        }
    }

    public void SetHeat(double cpu, double network, double notifications, double downloads, double uploads)
    {
        SetProperty(ref _cpuHeat, cpu, nameof(CpuHeat));
        SetProperty(ref _networkHeat, network, nameof(NetworkHeat));
        SetProperty(ref _notificationsHeat, notifications, nameof(NotificationsHeat));
        SetProperty(ref _downloadsHeat, downloads, nameof(DownloadsHeat));
        SetProperty(ref _uploadsHeat, uploads, nameof(UploadsHeat));
    }

    /// <summary>Used by the list view as the row's accessible name.</summary>
    public override string ToString() => Name;
}
