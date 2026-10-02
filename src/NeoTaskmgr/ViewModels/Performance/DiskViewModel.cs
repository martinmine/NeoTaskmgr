using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.ViewModels.Performance;

public sealed class DiskViewModel : PerformanceDeviceViewModel
{
    private const double MinimumTransferScale = 1024 * 1024; // 1 MB/s

    private readonly StatViewModel _activeTime;
    private readonly StatViewModel _responseTime;
    private readonly StatViewModel _readSpeed;
    private readonly StatViewModel _writeSpeed;
    private readonly StatViewModel _capacity;
    private readonly StatViewModel _formatted;
    private readonly StatViewModel _systemDisk;
    private readonly StatViewModel _type;
    private string _transferScale = string.Empty;

    public DiskViewModel(string key) : base(key, PerformanceDeviceKind.Disk)
    {
        _activeTime = AddStat("StatActiveTime");
        _responseTime = AddStat("StatResponseTime");
        _readSpeed = AddStat("StatReadSpeed", newRow: true);
        _writeSpeed = AddStat("StatWriteSpeed");
        _capacity = AddProperty("StatCapacity");
        _formatted = AddProperty("StatFormatted");
        _systemDisk = AddProperty("StatSystemDisk");
        _type = AddProperty("StatType");
    }

    public GraphHistory ReadHistory { get; } = new();
    public GraphHistory WriteHistory { get; } = new();

    /// <summary>The transfer rate at the top of the transfer graph.</summary>
    public string TransferScale
    {
        get => _transferScale;
        private set => SetProperty(ref _transferScale, value);
    }

    public void Update(DiskSample disk)
    {
        History.Add(disk.ActivePercent);
        ReadHistory.Add(disk.ReadBytesPerSecond);
        WriteHistory.Add(disk.WriteBytesPerSecond);
        var scale = GraphScale.NiceMaximum(Math.Max(ReadHistory.Peak, WriteHistory.Peak), MinimumTransferScale);
        ReadHistory.Maximum = WriteHistory.Maximum = scale;
        TransferScale = Format.BytesPerSecond(scale);

        var info = disk.Info;
        var volumes = string.Join(' ', disk.Volumes);
        Title = volumes.Length > 0 ? Strings.Format("PerformanceDiskWithVolumes", disk.Number, volumes) : Strings.Format("PerformanceDisk", disk.Number);
        Subtitle = TypeText(info);
        Detail = Format.WholePercent(disk.ActivePercent);
        Description = info.Model ?? string.Empty;

        _activeTime.Value = Format.WholePercent(disk.ActivePercent);
        _responseTime.Value = Format.Milliseconds(disk.ResponseTimeMs);
        _readSpeed.Value = Format.BytesPerSecond(disk.ReadBytesPerSecond);
        _writeSpeed.Value = Format.BytesPerSecond(disk.WriteBytesPerSecond);
        _capacity.Value = info.CapacityBytes is { } capacity ? Format.Size(capacity) : Strings.Get("NotAvailable");
        _formatted.Value = Format.Size(info.FormattedBytes);
        _systemDisk.Value = Strings.Get(info.IsSystemDisk ? "Yes" : "No");
        _type.Value = TypeText(info);
    }

    private static string TypeText(DiskInfo info) => info.Kind switch
    {
        DiskKind.Ssd => info.IsNvme ? Strings.Get("DiskTypeNvmeSsd") : Strings.Get("DiskTypeSsd"),
        DiskKind.Hdd => Strings.Get("DiskTypeHdd"),
        _ => Strings.Get("NotAvailable"),
    };
}
