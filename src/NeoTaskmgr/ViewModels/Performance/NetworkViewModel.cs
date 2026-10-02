using NeoTaskmgr.Core.Formatting;
using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.ViewModels.Performance;

/// <summary>A network adapter. <see cref="PerformanceDeviceViewModel.History"/> is the receive rate.</summary>
public sealed class NetworkViewModel : PerformanceDeviceViewModel
{
    private const double MinimumScale = 100_000; // 100 Kbps

    private readonly StatViewModel _send;
    private readonly StatViewModel _receive;
    private readonly StatViewModel _adapterName;
    private readonly StatViewModel _connectionType;
    private readonly StatViewModel _ipv4;
    private readonly StatViewModel _ipv6;
    private readonly StatViewModel _linkSpeed;
    private string _scale = string.Empty;

    public NetworkViewModel(string key) : base(key, PerformanceDeviceKind.Network)
    {
        _send = AddStat("StatSend", line: StatLine.Dashed);
        _receive = AddStat("StatReceive", newRow: true, line: StatLine.Solid);
        _adapterName = AddProperty("StatAdapterName");
        _connectionType = AddProperty("StatConnectionType");
        _ipv4 = AddProperty("StatIPv4");
        _ipv6 = AddProperty("StatIPv6");
        _linkSpeed = AddProperty("StatLinkSpeed");
    }

    public GraphHistory SendHistory { get; } = new();

    public override GraphHistory SecondaryHistory => SendHistory;

    /// <summary>The throughput at the top of the graph.</summary>
    public string Scale
    {
        get => _scale;
        private set => SetProperty(ref _scale, value);
    }

    public void Update(NetworkAdapterSample adapter)
    {
        History.Add(adapter.ReceiveBitsPerSecond);
        SendHistory.Add(adapter.SendBitsPerSecond);
        var scale = GraphScale.NiceMaximum(Math.Max(History.Peak, SendHistory.Peak), MinimumScale);
        History.Maximum = SendHistory.Maximum = scale;
        Scale = Format.BitsPerSecond(scale);

        var kind = Strings.Get(adapter.Kind == NetworkAdapterKind.WiFi ? "NetworkWiFi" : "NetworkEthernet");
        Title = kind;
        Subtitle = adapter.Name;
        Detail = Strings.Format("NetworkSendReceive", Format.BitsPerSecond(adapter.SendBitsPerSecond), Format.BitsPerSecond(adapter.ReceiveBitsPerSecond));
        Description = adapter.Description;

        _send.Value = Format.BitsPerSecond(adapter.SendBitsPerSecond);
        _receive.Value = Format.BitsPerSecond(adapter.ReceiveBitsPerSecond);
        _adapterName.Value = adapter.Name;
        _connectionType.Value = kind;
        _ipv4.Value = adapter.IPv4Address ?? Strings.Get("NotAvailable");
        _ipv6.Value = adapter.IPv6Address ?? Strings.Get("NotAvailable");
        _linkSpeed.Value = Format.BitsPerSecond(adapter.LinkSpeedBitsPerSecond);
    }
}
