using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace NeoTaskmgr.Core.Performance;

/// <summary>
/// Counts network bytes sent and received per process, using the kernel's TCP/IP events (ETW).
/// Requires administrator rights; <see cref="IsAvailable"/> is false otherwise.
/// </summary>
public sealed class NetworkActivityMonitor : IDisposable
{
    private const string SessionName = "NeoTaskmgr-NetworkActivity";

    private readonly TraceEventSession? _session;
    private readonly Lock _lock = new();
    private Dictionary<int, long> _bytesByPid = [];

    public NetworkActivityMonitor()
    {
        if (!Elevation.IsElevated)
        {
            return;
        }

        try
        {
            // Replaces a session left behind if the app previously crashed.
            _session = new TraceEventSession(SessionName) { StopOnDispose = true };
            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

            var kernel = _session.Source.Kernel;
            kernel.TcpIpSend += e => Add(e.ProcessID, e.size);
            kernel.TcpIpRecv += e => Add(e.ProcessID, e.size);
            kernel.TcpIpSendIPV6 += e => Add(e.ProcessID, e.size);
            kernel.TcpIpRecvIPV6 += e => Add(e.ProcessID, e.size);
            kernel.UdpIpSend += e => Add(e.ProcessID, e.size);
            kernel.UdpIpRecv += e => Add(e.ProcessID, e.size);
            kernel.UdpIpSendIPV6 += e => Add(e.ProcessID, e.size);
            kernel.UdpIpRecvIPV6 += e => Add(e.ProcessID, e.size);

            // Process() blocks until the session is disposed.
            new Thread(() => _session.Source.Process()) { IsBackground = true, Name = "Network activity" }.Start();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            Debug.WriteLine($"Network activity monitoring is unavailable: {e.Message}");
            _session?.Dispose();
            _session = null;
        }
    }

    public bool IsAvailable => _session is not null;

    /// <summary>Returns the bytes sent and received per process ID since the previous call, or null if unavailable.</summary>
    public IReadOnlyDictionary<int, long>? TakeBytes()
    {
        if (!IsAvailable)
        {
            return null;
        }

        lock (_lock)
        {
            var bytes = _bytesByPid;
            _bytesByPid = [];
            return bytes;
        }
    }

    public void Dispose() => _session?.Dispose();

    private void Add(int pid, int size)
    {
        lock (_lock)
        {
            _bytesByPid[pid] = _bytesByPid.GetValueOrDefault(pid) + size;
        }
    }
}
