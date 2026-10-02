using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace NeoTaskmgr.Core.Performance;

/// <summary>Physical memory usage of the system.</summary>
public sealed record MemoryStatus(long TotalPhysical, long AvailablePhysical)
{
    public long UsedPhysical => TotalPhysical - AvailablePhysical;

    public double UsedPercent => TotalPhysical == 0 ? 0 : UsedPhysical * 100.0 / TotalPhysical;

    public static unsafe MemoryStatus Read()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        if (!PInvoke.GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new MemoryStatus((long)status.ullTotalPhys, (long)status.ullAvailPhys);
    }
}
