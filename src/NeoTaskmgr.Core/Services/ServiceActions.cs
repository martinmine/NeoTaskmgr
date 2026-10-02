using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Services;

namespace NeoTaskmgr.Core.Services;

/// <summary>Starts and stops services. Requires administrator rights; failures throw <see cref="Win32Exception"/>.</summary>
public static class ServiceActions
{
    private const int ErrorServiceAlreadyRunning = 1056;
    private const int ErrorServiceNotActive = 1062;

    public static void Start(string name)
    {
        using var service = Open(name, PInvoke.SERVICE_START);
        if (!PInvoke.StartService(service, default) && Marshal.GetLastPInvokeError() != ErrorServiceAlreadyRunning)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    public static void Stop(string name)
    {
        using var service = Open(name, PInvoke.SERVICE_STOP);
        if (!PInvoke.ControlService(service, PInvoke.SERVICE_CONTROL_STOP, out _) && Marshal.GetLastPInvokeError() != ErrorServiceNotActive)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Stops the service, waits until it has stopped (up to <paramref name="timeout"/>), and starts it again. Blocks.</summary>
    public static void Restart(string name, TimeSpan timeout)
    {
        Stop(name);
        using var service = Open(name, PInvoke.SERVICE_QUERY_STATUS);
        var stopwatch = Stopwatch.StartNew();
        while (GetState(service) != SERVICE_STATUS_CURRENT_STATE.SERVICE_STOPPED)
        {
            if (stopwatch.Elapsed > timeout)
            {
                throw new Win32Exception(1053); // ERROR_SERVICE_REQUEST_TIMEOUT
            }

            Thread.Sleep(250);
        }

        Start(name);
    }

    /// <summary>Opens the Services console.</summary>
    public static void OpenServicesConsole() =>
        Process.Start(new ProcessStartInfo("services.msc") { UseShellExecute = true })?.Dispose();

    private static CloseServiceHandleSafeHandle Open(string name, uint access)
    {
        using var manager = PInvoke.OpenSCManager(null, null, PInvoke.SC_MANAGER_CONNECT);
        var service = manager.IsInvalid ? manager : PInvoke.OpenService(manager, name, access);
        if (service.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            service.Dispose();
            throw new Win32Exception(error);
        }

        return service;
    }

    private static unsafe SERVICE_STATUS_CURRENT_STATE GetState(CloseServiceHandleSafeHandle service)
    {
        Span<byte> buffer = stackalloc byte[sizeof(SERVICE_STATUS_PROCESS)];
        if (!PInvoke.QueryServiceStatusEx(service, SC_STATUS_TYPE.SC_STATUS_PROCESS_INFO, buffer, out _))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return MemoryMarshal.Read<SERVICE_STATUS_PROCESS>(buffer).dwCurrentState;
    }
}
