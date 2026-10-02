using System.Globalization;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Users;
using NeoTaskmgr.Mvvm;
using static NeoTaskmgr.ViewModels.ProcessGroupRowViewModel;

namespace NeoTaskmgr.ViewModels;

/// <summary>A row of the Processes or Users page with usage columns, for sorting rows of either kind.</summary>
public interface IUsageRow
{
    string Name { get; }
    string StatusSortText { get; }
    double CpuPercent { get; }
    long Memory { get; }
    double DiskBytesPerSecond { get; }
    double? NetworkBytesPerSecond { get; }
    double GpuPercent { get; }
}

/// <summary>A signed-in user on the Users page, with the total usage of their processes. Expands to the processes.</summary>
public sealed class UserRowViewModel : ObservableObject, IUsageRow
{
    private bool _isExpanded;
    private long _totalMemory = 1;

    public UserRowViewModel(UserSession session, Action<UserRowViewModel> toggleExpanded)
    {
        Session = session;
        ToggleExpandedCommand = new RelayCommand(() => toggleExpanded(this));
    }

    public UserSession Session { get; private set; }
    public IReadOnlyList<ProcessInfo> Processes { get; private set; } = [];
    public RelayCommand ToggleExpandedCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
                OnPropertyChanged(nameof(ExpandText));
            }
        }
    }

    public bool CanExpand => Processes.Count > 0;
    public string ExpandGlyph => IsExpanded ? "" : ""; // ChevronDown / ChevronRight
    public string ExpandText => Strings.Get(IsExpanded ? "Collapse" : "Expand");

    public string Name => Session.DisplayName;
    public string? PicturePath => Session.PicturePath;
    public bool HasPicture => Session.PicturePath is not null;
    public string IdText => Session.SessionId.ToString(CultureInfo.CurrentCulture);
    public string SessionName => Session.SessionName;
    public string ClientName => Session.ClientName;
    public bool IsDisconnected => Session.State == UserSessionState.Disconnected;
    public string StatusText => IsDisconnected ? Strings.Get("StatusDisconnected") : string.Empty;
    public string StatusSortText => StatusText;

    public double CpuPercent => Processes.Sum(p => p.CpuPercent);
    public string CpuText => Format.Percent(CpuPercent);
    public double CpuHeat => CpuPercent / HotCpuPercent;

    public long Memory => Processes.Sum(p => p.Counters.PrivateWorkingSet);
    public string MemoryText => Format.Megabytes(Memory);
    public double MemoryHeat => Memory / (_totalMemory * HotMemoryFraction);

    public double DiskBytesPerSecond => Processes.Sum(p => p.DiskBytesPerSecond);
    public string DiskText => Format.MegabytesPerSecond(DiskBytesPerSecond);
    public double DiskHeat => DiskBytesPerSecond / HotDiskBytesPerSecond;

    /// <summary>Null when per-process network usage is unavailable (without administrator rights).</summary>
    public double? NetworkBytesPerSecond => Processes.Count > 0 && Processes[0].NetworkBytesPerSecond is null ? null : Processes.Sum(p => p.NetworkBytesPerSecond ?? 0);
    public string NetworkText => NetworkBytesPerSecond is { } bytes ? Format.Megabits(bytes) : string.Empty;
    public double NetworkHeat => (NetworkBytesPerSecond ?? 0) / HotNetworkBytesPerSecond;

    public double GpuPercent => Math.Min(100, Processes.Sum(p => p.GpuPercent));
    public string GpuText => Format.Percent(GpuPercent);
    public double GpuHeat => GpuPercent / HotGpuPercent;

    public void Update(UserSession session, IReadOnlyList<ProcessInfo> processes, long totalMemory)
    {
        if (session != Session || totalMemory != _totalMemory || !processes.SequenceEqual(Processes))
        {
            Session = session;
            Processes = processes;
            _totalMemory = Math.Max(1, totalMemory);
            OnPropertyChanged(string.Empty); // All properties derive from the session and processes.
        }
    }

    /// <summary>Used by the list view as the row's accessible name.</summary>
    public override string ToString() => Name;
}
