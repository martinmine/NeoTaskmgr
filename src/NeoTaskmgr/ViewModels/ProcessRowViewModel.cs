using System.Diagnostics;
using System.Globalization;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>One process in a table. Display properties are derived from <see cref="Info"/>.</summary>
public sealed class ProcessRowViewModel(ProcessInfo info) : ObservableObject
{
    public ProcessInfo Info { get; private set; } = info;

    public ProcessKey Key => Info.Key;
    public string Name => Info.Counters.Name;
    public string PidText => Info.Counters.Pid.ToString(CultureInfo.CurrentCulture);
    public string StatusText => Strings.Get(Info.Counters.IsSuspended ? "StatusSuspended" : "StatusRunning");
    public string UserName => Info.Details.UserName ?? string.Empty;
    public string SessionIdText => Info.Counters.SessionId.ToString(CultureInfo.CurrentCulture);
    public string CpuText => Format.CpuPercent(Info.CpuPercent);
    public string CpuTimeText => Format.Duration(Info.Counters.CpuTime);
    public string MemoryText => Format.Kilobytes(Info.Counters.PrivateWorkingSet);
    public string WorkingSetText => Format.Kilobytes(Info.Counters.WorkingSet);
    public string PeakWorkingSetText => Format.Kilobytes(Info.Counters.PeakWorkingSet);
    public string CommitSizeText => Format.Kilobytes(Info.Counters.CommitSize);
    public string HandlesText => Format.Number(Info.Counters.HandleCount);
    public string ThreadsText => Format.Number(Info.Counters.ThreadCount);
    public ProcessPriorityClass? Priority => ProcessPriority.FromBasePriority(Info.Counters.BasePriority);
    public string PriorityText => Priority is { } priority ? Strings.Get($"Priority{priority}") : string.Empty;
    public string Architecture => Info.Details.Architecture ?? string.Empty;
    public string ElevatedText => Info.Details.IsElevated is { } elevated ? Strings.Get(elevated ? "Yes" : "No") : string.Empty;
    public string Description => Info.Details.Description ?? string.Empty;
    public string ImagePath => Info.Details.ImagePath ?? string.Empty;
    public string CommandLine => Info.Details.CommandLine ?? string.Empty;

    /// <summary>Used by the list view as the row's accessible name.</summary>
    public override string ToString() => Name;

    public void Update(ProcessInfo info)
    {
        if (info != Info)
        {
            Info = info;
            OnPropertyChanged(string.Empty); // All properties derive from Info.
        }
    }
}
