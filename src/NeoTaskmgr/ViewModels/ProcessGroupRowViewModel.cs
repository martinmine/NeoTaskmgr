using System.Globalization;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>
/// A row of the Processes page: an app with its processes (shown with totals), or a single process.
/// Child rows show one process of an expanded app.
/// </summary>
public sealed class ProcessGroupRowViewModel : ObservableObject
{
    // The usage at which the heat map shading is at its darkest.
    private const double HotCpuPercent = 25;
    private const double HotMemoryFraction = 0.1;
    private const double HotDiskBytesPerSecond = 10 * 1024 * 1024;
    private const double HotNetworkBytesPerSecond = 1024 * 1024;
    private const double HotGpuPercent = 25;

    private bool _isExpanded;
    private long _totalMemory = 1;
    private string? _displayName;

    public ProcessGroupRowViewModel(bool isChild, Action<ProcessGroupRowViewModel> toggleExpanded)
    {
        IsChild = isChild;
        ToggleExpandedCommand = new RelayCommand(() => toggleExpanded(this));
    }

    public IReadOnlyList<ProcessInfo> Processes { get; private set; } = [];
    public ProcessCategory Category { get; private set; }
    public ProcessInfo Root => Processes[0];
    public bool IsChild { get; }
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

    public bool CanExpand => !IsChild && Processes.Count > 1;
    public string ExpandGlyph => IsExpanded ? "" : ""; // ChevronDown / ChevronRight
    public string ExpandText => Strings.Get(IsExpanded ? "Collapse" : "Expand");

    public string Name => CanExpand ? $"{DisplayName} ({Processes.Count})" : DisplayName;
    private string DisplayName => _displayName ?? Root.DisplayName;
    public string? ImagePath => Root.Details.ImagePath;
    public string PidText => CanExpand ? string.Empty : Root.Counters.Pid.ToString(CultureInfo.CurrentCulture);
    public string ProcessName => Root.Counters.Name;
    public string Publisher => Root.Details.Publisher ?? string.Empty;
    public string CommandLine => Root.Details.CommandLine ?? string.Empty;

    public ProcessWindow? Window => Processes.Select(p => p.Window).FirstOrDefault(w => w is not null);
    public bool IsSuspended => Processes.All(p => p.Counters.IsSuspended);
    public bool IsEfficiencyMode => Root.IsEfficiencyMode;
    public bool IsExplorer => Root.Counters.Name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);

    public string StatusText =>
        Window?.IsHung == true ? Strings.Get("StatusNotResponding") :
        IsSuspended ? Strings.Get("StatusSuspended") :
        IsEfficiencyMode ? Strings.Get("StatusEfficiencyMode") :
        string.Empty;

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
    public double? NetworkBytesPerSecond => Root.NetworkBytesPerSecond is null ? null : Processes.Sum(p => p.NetworkBytesPerSecond ?? 0);
    public string NetworkText => NetworkBytesPerSecond is { } bytes ? Format.Megabits(bytes) : string.Empty;
    public double NetworkHeat => (NetworkBytesPerSecond ?? 0) / HotNetworkBytesPerSecond;

    public double GpuPercent => Math.Min(100, Processes.Sum(p => p.GpuPercent));
    public string GpuText => Format.Percent(GpuPercent);
    public double GpuHeat => GpuPercent / HotGpuPercent;

    /// <summary>Processes in the order to end them: the ones started last first.</summary>
    public IEnumerable<ProcessKey> KeysForEnding => Processes.Reverse().Select(p => p.Key);

    /// <param name="displayName">A name to show instead of the process's own, e.g. for service hosts.</param>
    public void Update(IReadOnlyList<ProcessInfo> processes, ProcessCategory category, long totalMemory, string? displayName = null)
    {
        if (category != Category || totalMemory != _totalMemory || displayName != _displayName || !processes.SequenceEqual(Processes))
        {
            Processes = processes;
            Category = category;
            _totalMemory = Math.Max(1, totalMemory);
            _displayName = displayName;
            OnPropertyChanged(string.Empty); // All properties derive from the processes.
        }
    }

    /// <summary>Used by the list view as the row's accessible name.</summary>
    public override string ToString() => Name;
}

/// <summary>The header row of a section of the Processes page, e.g. "Apps (5)".</summary>
public sealed class SectionRowViewModel(ProcessCategory category) : ObservableObject
{
    private int _count;

    public ProcessCategory Category { get; } = category;

    public string Title => Strings.Format($"Section{Category}", _count);

    public void Update(int count) => SetProperty(ref _count, count, nameof(Title));

    public override string ToString() => Title;
}
