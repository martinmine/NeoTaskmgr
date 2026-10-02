using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Services;

namespace NeoTaskmgr.Core.Services;

/// <summary>Lists the Win32 services. Not thread-safe; call <see cref="Sample"/> from one thread at a time.</summary>
public sealed unsafe class ServiceProvider : IDisposable
{
    private readonly CloseServiceHandleSafeHandle _manager =
        PInvoke.OpenSCManager(null, null, PInvoke.SC_MANAGER_CONNECT | PInvoke.SC_MANAGER_ENUMERATE_SERVICE);

    // A service's group comes from its configuration, which rarely changes; read it once per service.
    private readonly Dictionary<string, string?> _groups = new(StringComparer.OrdinalIgnoreCase);
    private byte[] _buffer = new byte[256 * 1024];

    public IReadOnlyList<ServiceInfo> Sample()
    {
        if (_manager.IsInvalid)
        {
            return [];
        }

        while (true)
        {
            if (PInvoke.EnumServicesStatusEx(_manager, SC_ENUM_TYPE.SC_ENUM_PROCESS_INFO, ENUM_SERVICE_TYPE.SERVICE_WIN32,
                    ENUM_SERVICE_STATE.SERVICE_STATE_ALL, _buffer, out var needed, out var count))
            {
                return Parse(count);
            }

            if (Marshal.GetLastPInvokeError() != 234) // ERROR_MORE_DATA: services were added, or the buffer is too small.
            {
                return [];
            }

            _buffer = new byte[Math.Max(needed + 16 * 1024, _buffer.Length * 2)];
        }
    }

    public void Dispose() => _manager.Dispose();

    private List<ServiceInfo> Parse(uint count)
    {
        var services = new List<ServiceInfo>((int)count);
        fixed (byte* buffer = _buffer)
        {
            var entries = (ENUM_SERVICE_STATUS_PROCESSW*)buffer;
            for (var i = 0; i < count; i++)
            {
                var name = entries[i].lpServiceName.ToString();
                var status = entries[i].ServiceStatusProcess;
                services.Add(new ServiceInfo(
                    name,
                    entries[i].lpDisplayName.ToString(),
                    (ServiceState)status.dwCurrentState,
                    (int)status.dwProcessId,
                    GetGroup(name)));
            }
        }

        return services;
    }

    private string? GetGroup(string name)
    {
        if (!_groups.TryGetValue(name, out var group))
        {
            _groups[name] = group = ServiceGroup.Parse(ReadBinaryPath(name));
        }

        return group;
    }

    private string? ReadBinaryPath(string name)
    {
        using var service = PInvoke.OpenService(_manager, name, PInvoke.SERVICE_QUERY_CONFIG);
        if (service.IsInvalid)
        {
            return null;
        }

        Span<byte> config = stackalloc byte[8192];
        if (!PInvoke.QueryServiceConfig(service, config, out _))
        {
            return null;
        }

        fixed (byte* data = config)
        {
            return ((QUERY_SERVICE_CONFIGW*)data)->lpBinaryPathName.ToString();
        }
    }
}
