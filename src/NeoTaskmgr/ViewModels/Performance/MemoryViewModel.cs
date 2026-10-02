using System.Globalization;
using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.ViewModels.Performance;

public sealed class MemoryViewModel : PerformanceDeviceViewModel
{
    private readonly StatViewModel _inUse;
    private readonly StatViewModel _available;
    private readonly StatViewModel _committed;
    private readonly StatViewModel _cached;
    private readonly StatViewModel _pagedPool;
    private readonly StatViewModel _nonPagedPool;
    private readonly StatViewModel _hardwareReserved;
    private MemoryComposition _composition = new(1, 0, 0, 0);

    public MemoryViewModel(MemoryInfo info) : base("memory", PerformanceDeviceKind.Memory)
    {
        Title = Strings.Get("PerformanceMemory");
        Description = Format.Size(info.InstalledBytes);
        _inUse = AddStat("StatInUse");
        _available = AddStat("StatAvailable");
        _committed = AddStat("StatCommitted", newRow: true);
        _cached = AddStat("StatCached");
        _pagedPool = AddStat("StatPagedPool", newRow: true);
        _nonPagedPool = AddStat("StatNonPagedPool");
        AddProperty("StatSpeed").Value = info.SpeedMts is { } speed ? $"{speed.ToString(CultureInfo.CurrentCulture)} MT/s" : Strings.Get("NotAvailable");
        AddProperty("StatSlotsUsed").Value = info.SlotsTotal > 0 ? Strings.Format("SlotsUsedValue", info.SlotsUsed, info.SlotsTotal) : Strings.Get("NotAvailable");
        AddProperty("StatFormFactor").Value = info.FormFactor ?? Strings.Get("NotAvailable");
        _hardwareReserved = AddProperty("StatHardwareReserved");
    }

    /// <summary>Relative sizes of the parts of physical memory, for the composition bar.</summary>
    public MemoryComposition Composition
    {
        get => _composition;
        private set => SetProperty(ref _composition, value);
    }

    public void Update(MemorySample memory)
    {
        History.Add(memory.UsedPercent);
        Subtitle = $"{Format.Size(memory.UsedPhysical)}/{Format.Size(memory.TotalPhysical)} ({Format.WholePercent(memory.UsedPercent)})";
        _inUse.Value = Format.Size(memory.UsedPhysical);
        _available.Value = Format.Size(memory.AvailablePhysical);
        _committed.Value = $"{Format.Size(memory.Committed)}/{Format.Size(memory.CommitLimit)}";
        _cached.Value = Format.Size(memory.Cached);
        _pagedPool.Value = Format.Size(memory.PagedPool);
        _nonPagedPool.Value = Format.Size(memory.NonPagedPool);
        _hardwareReserved.Value = Format.Size(memory.HardwareReserved);

        // "In use" excludes the modified pages, which are shown separately.
        Composition = new MemoryComposition(Math.Max(0, memory.UsedPhysical - memory.Modified), memory.Modified, memory.Standby, memory.Free);
    }
}

/// <summary>Physical memory split into the parts Task Manager's composition bar shows.</summary>
public sealed record MemoryComposition(long InUse, long Modified, long Standby, long Free);
