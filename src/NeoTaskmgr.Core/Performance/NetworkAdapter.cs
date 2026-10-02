using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NeoTaskmgr.Core.Performance;

public enum NetworkAdapterKind
{
    Ethernet,
    WiFi,
}

public sealed record NetworkAdapterSample
{
    public required string Id { get; init; }

    /// <summary>The connection name, such as "Wi-Fi" or "Ethernet 2".</summary>
    public required string Name { get; init; }

    /// <summary>The adapter's description, usually its model.</summary>
    public required string Description { get; init; }

    public NetworkAdapterKind Kind { get; init; }
    public double SendBitsPerSecond { get; init; }
    public double ReceiveBitsPerSecond { get; init; }
    public long LinkSpeedBitsPerSecond { get; init; }
    public string? IPv4Address { get; init; }
    public string? IPv6Address { get; init; }
}

/// <summary>Reads the throughput of connected network adapters since the previous call.</summary>
internal sealed class NetworkAdapterReader
{
    private Dictionary<string, (long Sent, long Received)> _previous = [];
    private TimeSpan _previousTimestamp;

    public List<NetworkAdapterSample> Read(TimeSpan timestamp)
    {
        var seconds = (timestamp - _previousTimestamp).TotalSeconds;
        var current = new Dictionary<string, (long, long)>();
        var adapters = new List<NetworkAdapterSample>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || GetKind(adapter.NetworkInterfaceType) is not { } kind || adapter.Speed <= 0)
            {
                continue;
            }

            // Hidden filter drivers and WAN miniports also report as "up" Ethernet; real connections have an address.
            var addresses = adapter.GetIPProperties().UnicastAddresses.Select(a => a.Address).ToList();
            if (addresses.Count == 0)
            {
                continue;
            }

            var statistics = adapter.GetIPStatistics();
            var (sent, received) = (statistics.BytesSent, statistics.BytesReceived);
            current[adapter.Id] = (sent, received);
            var hasPrevious = _previous.TryGetValue(adapter.Id, out var previous) && seconds > 0;

            adapters.Add(new NetworkAdapterSample
            {
                Id = adapter.Id,
                Name = adapter.Name,
                Description = adapter.Description,
                Kind = kind,
                SendBitsPerSecond = hasPrevious ? Math.Max(0, sent - previous.Sent) * 8 / seconds : 0,
                ReceiveBitsPerSecond = hasPrevious ? Math.Max(0, received - previous.Received) * 8 / seconds : 0,
                LinkSpeedBitsPerSecond = Math.Max(0, adapter.Speed),
                IPv4Address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString(),
                IPv6Address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)?.ToString(),
            });
        }

        _previous = current;
        _previousTimestamp = timestamp;
        adapters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return adapters;
    }

    private static NetworkAdapterKind? GetKind(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => NetworkAdapterKind.WiFi,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ethernet3Megabit or NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.GigabitEthernet => NetworkAdapterKind.Ethernet,
        _ => null, // Loopback, tunnels, etc.
    };
}
