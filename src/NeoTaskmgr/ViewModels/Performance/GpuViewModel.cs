using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Performance;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels.Performance;

public sealed class GpuViewModel : PerformanceDeviceViewModel
{
    // Task Manager's default engine graphs; other engine types fill in when these do not exist.
    private static readonly string[] PreferredEngines = ["3D", "Copy", "VideoEncode", "VideoDecode"];

    private readonly StatViewModel _utilization;
    private readonly StatViewModel _dedicatedMemory;
    private readonly StatViewModel _sharedMemory;
    private readonly StatViewModel _gpuMemory;

    public GpuViewModel(string key, int index, GpuInfo info) : base(key, PerformanceDeviceKind.Gpu)
    {
        Title = Strings.Format("PerformanceGpu", index);
        Subtitle = info.Name;
        Description = info.Name;
        DedicatedMemoryHistory = new GraphHistory(info.DedicatedMemoryBytes);
        SharedMemoryHistory = new GraphHistory(info.SharedMemoryBytes);
        _utilization = AddStat("StatUtilization");
        _dedicatedMemory = AddStat("StatDedicatedGpuMemory");
        _sharedMemory = AddStat("StatSharedGpuMemory");
        _gpuMemory = AddStat("StatGpuMemory");
    }

    /// <summary>Up to four engine graphs, chosen when the first sample arrives.</summary>
    public List<GpuEngineViewModel> Engines { get; } = [];

    public GraphHistory DedicatedMemoryHistory { get; }
    public GraphHistory SharedMemoryHistory { get; }

    public void Update(GpuSample gpu)
    {
        if (Engines.Count == 0)
        {
            Engines.AddRange(PreferredEngines.Where(gpu.EngineUtilization.ContainsKey)
                .Concat(gpu.EngineUtilization.Keys.Except(PreferredEngines).Order())
                .Take(4)
                .Select(name => new GpuEngineViewModel(name)));
            OnPropertyChanged(nameof(Engines));
        }

        History.Add(gpu.UtilizationPercent);
        foreach (var engine in Engines)
        {
            engine.Update(gpu.EngineUtilization.GetValueOrDefault(engine.Name));
        }

        DedicatedMemoryHistory.Add(gpu.DedicatedMemoryUsed);
        SharedMemoryHistory.Add(gpu.SharedMemoryUsed);

        var info = gpu.Info;
        Detail = Format.WholePercent(gpu.UtilizationPercent);
        _utilization.Value = Format.WholePercent(gpu.UtilizationPercent);
        _dedicatedMemory.Value = $"{Format.Size(gpu.DedicatedMemoryUsed)}/{Format.Size(info.DedicatedMemoryBytes)}";
        _sharedMemory.Value = $"{Format.Size(gpu.SharedMemoryUsed)}/{Format.Size(info.SharedMemoryBytes)}";
        _gpuMemory.Value = $"{Format.Size(gpu.DedicatedMemoryUsed + gpu.SharedMemoryUsed)}/{Format.Size(info.DedicatedMemoryBytes + info.SharedMemoryBytes)}";
    }
}

public sealed class GpuEngineViewModel(string name) : ObservableObject
{
    private string _utilization = string.Empty;

    public string Name { get; } = name;
    public GraphHistory History { get; } = new();

    public string Utilization
    {
        get => _utilization;
        private set => SetProperty(ref _utilization, value);
    }

    public void Update(double percent)
    {
        History.Add(percent);
        Utilization = Format.WholePercent(percent);
    }
}
