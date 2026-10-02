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

    private bool _isExpanded;
    private long _totalMemory = 1;

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

    public string Name => CanExpand ? $"{Root.DisplayName} ({Processes.Count})" : Root.DisplayName;
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

    /// <summary>Processes in the order to end them: the ones started last first.</summary>
    public IEnumerable<ProcessKey> KeysForEnding => Processes.Reverse().Select(p => p.Key);

    public void Update(IReadOnlyList<ProcessInfo> processes, ProcessCategory category, long totalMemory)
    {
        if (category != Category || totalMemory != _totalMemory || !processes.SequenceEqual(Processes))
        {
            Processes = processes;
            Category = category;
            _totalMemory = Math.Max(1, totalMemory);
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
