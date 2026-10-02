using System.Collections.ObjectModel;
using System.Globalization;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.ViewModels.Performance;

public sealed class CpuViewModel : PerformanceDeviceViewModel
{
    private readonly StatViewModel _utilization;
    private readonly StatViewModel _speed;
    private readonly StatViewModel _processes;
    private readonly StatViewModel _threads;
    private readonly StatViewModel _handles;
    private readonly StatViewModel _uptime;
    private bool _showLogicalProcessors;

    public CpuViewModel(CpuInfo info) : base("cpu", PerformanceDeviceKind.Cpu)
    {
        Title = Strings.Get("PerformanceCpu");
        Description = info.Name;
        _utilization = AddStat("StatUtilization");
        _speed = AddStat("StatSpeed");
        _processes = AddStat("StatProcesses");
        _threads = AddStat("StatThreads");
        _handles = AddStat("StatHandles");
        _uptime = AddStat("StatUptime");
        AddStat("StatBaseSpeed").Value = Format.Gigahertz(info.BaseSpeedMhz);
        AddStat("StatSockets").Value = info.Sockets.ToString(CultureInfo.CurrentCulture);
        AddStat("StatCores").Value = info.Cores.ToString(CultureInfo.CurrentCulture);
        AddStat("StatLogicalProcessors").Value = info.LogicalProcessors.ToString(CultureInfo.CurrentCulture);
        AddStat("StatVirtualization").Value = Strings.Get(info.IsVirtualizationEnabled ? "Enabled" : "Disabled");
        AddStat("StatL1Cache").Value = Format.Size(info.L1CacheBytes);
        AddStat("StatL2Cache").Value = Format.Size(info.L2CacheBytes);
        AddStat("StatL3Cache").Value = Format.Size(info.L3CacheBytes);

        for (var i = 0; i < info.LogicalProcessors; i++)
        {
            ProcessorHistories.Add(new GraphHistory());
        }
    }

    public ObservableCollection<GraphHistory> ProcessorHistories { get; } = [];

    /// <summary>Shows a graph per logical processor instead of the overall utilization.</summary>
    public bool ShowLogicalProcessors
    {
        get => _showLogicalProcessors;
        set => SetProperty(ref _showLogicalProcessors, value);
    }

    public void Update(CpuSample cpu, int processes, int threads, int handles)
    {
        History.Add(cpu.UsagePercent);
        for (var i = 0; i < ProcessorHistories.Count && i < cpu.ProcessorUsagePercent.Count; i++)
        {
            ProcessorHistories[i].Add(cpu.ProcessorUsagePercent[i]);
        }

        Subtitle = $"{Format.WholePercent(cpu.UsagePercent)}  {Format.Gigahertz(cpu.SpeedMhz)}";
        _utilization.Value = Format.WholePercent(cpu.UsagePercent);
        _speed.Value = Format.Gigahertz(cpu.SpeedMhz);
        _processes.Value = Format.Number(processes);
        _threads.Value = Format.Number(threads);
        _handles.Value = Format.Number(handles);
        _uptime.Value = Format.Uptime(TimeSpan.FromMilliseconds(Environment.TickCount64));
    }
}
