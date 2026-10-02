using System.Collections.ObjectModel;
using System.Globalization;
using NeoTaskmgr.Core.Collections;
using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels.Performance;

/// <summary>The Performance page: a list of devices (CPU, memory, disks, network adapters, GPUs) with graphs and details.</summary>
public sealed class PerformanceViewModel : ObservableObject
{
    private Dictionary<string, PerformanceDeviceViewModel> _devices = [];
    private PerformanceDeviceViewModel? _selectedDevice;

    public PerformanceViewModel(ProcessCommandRunner runner)
    {
        RunNewTaskCommand = new RelayCommand(() => _ = runner.RunNewTaskAsync());
    }

    public ObservableCollection<PerformanceDeviceViewModel> Devices { get; } = [];

    public RelayCommand RunNewTaskCommand { get; }

    public PerformanceDeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            // The list view clears its selection while devices are added or removed; keep the selected device.
            if (value is not null)
            {
                SetProperty(ref _selectedDevice, value);
            }
        }
    }

    /// <summary>Updates the devices from a new snapshot. Must be called on the UI thread.</summary>
    public void Apply(SystemSnapshot snapshot)
    {
        var performance = snapshot.Performance;
        var processes = snapshot.Processes.Processes;
        var devices = new Dictionary<string, PerformanceDeviceViewModel>();
        var order = new List<PerformanceDeviceViewModel>();

        // Reuses the view model of a known device (keeping its graph history), or creates one.
        T Get<T>(string key, Func<string, T> create)
            where T : PerformanceDeviceViewModel
        {
            var device = _devices.GetValueOrDefault(key) as T ?? create(key);
            devices[key] = device;
            order.Add(device);
            return device;
        }

        var cpu = Get("cpu", _ => new CpuViewModel(performance.Cpu.Info));
        cpu.Update(performance.Cpu, processes.Count(p => p.Counters.Pid != 0), processes.Sum(p => p.Counters.ThreadCount), processes.Sum(p => p.Counters.HandleCount));
        Get("memory", _ => new MemoryViewModel(performance.Memory.Info)).Update(performance.Memory);

        foreach (var disk in performance.Disks)
        {
            Get($"disk:{disk.Number.ToString(CultureInfo.InvariantCulture)}", key => new DiskViewModel(key)).Update(disk);
        }

        foreach (var adapter in performance.NetworkAdapters)
        {
            Get($"network:{adapter.Id}", key => new NetworkViewModel(key)).Update(adapter);
        }

        for (var i = 0; i < performance.Gpus.Count; i++)
        {
            var (gpu, index) = (performance.Gpus[i], i);
            Get($"gpu:{gpu.Info.Luid.ToString(CultureInfo.InvariantCulture)}", key => new GpuViewModel(key, index, gpu.Info)).Update(gpu);
        }

        _devices = devices;
        Devices.SyncWith(order);
        if (_selectedDevice is null || !devices.ContainsValue(_selectedDevice))
        {
            SetProperty(ref _selectedDevice, cpu, nameof(SelectedDevice));
        }
        else
        {
            OnPropertyChanged(nameof(SelectedDevice)); // Restores the selection in the list view.
        }
    }
}
