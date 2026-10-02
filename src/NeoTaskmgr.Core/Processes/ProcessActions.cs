using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NeoTaskmgr.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace NeoTaskmgr.Core.Processes;

/// <summary>
/// Actions on processes. Each action verifies the process's creation time so a reused PID is never touched.
/// Actions on a process that has already exited do nothing. Failures throw <see cref="Win32Exception"/>.
/// </summary>
public static class ProcessActions
{
    private const uint StillActive = 259;

    public static void End(ProcessKey process)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE);
        if (handle is not null && !PInvoke.TerminateProcess(handle, 1))
        {
            ThrowLastError();
        }
    }

    /// <summary>Ends the process and all of its descendants, children first.</summary>
    public static void EndTree(ProcessKey root, IReadOnlyList<ProcessCounters> processes)
    {
        Exception? firstError = null;
        foreach (var process in ProcessTree.GetDescendants(processes, root).Select(p => p.Key).Append(root))
        {
            try
            {
                End(process);
            }
            catch (Win32Exception e)
            {
                firstError ??= e;
            }
        }

        if (firstError is not null)
        {
            throw firstError;
        }
    }

    public static void SetPriority(ProcessKey process, ProcessPriorityClass priority)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_SET_INFORMATION);
        if (handle is not null && !PInvoke.SetPriorityClass(handle, (PROCESS_CREATION_FLAGS)priority))
        {
            ThrowLastError();
        }
    }

    /// <summary>Returns the process's affinity mask and the mask of processors available in the system, or null if it exited.</summary>
    public static (ulong ProcessMask, ulong SystemMask)? GetAffinity(ProcessKey process)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION);
        if (handle is null)
        {
            return null;
        }

        if (!PInvoke.GetProcessAffinityMask(handle, out var processMask, out var systemMask))
        {
            ThrowLastError();
        }

        return (processMask, systemMask);
    }

    public static void SetAffinity(ProcessKey process, ulong mask)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_SET_INFORMATION);
        if (handle is not null && !PInvoke.SetProcessAffinityMask(handle, (nuint)mask))
        {
            ThrowLastError();
        }
    }

    /// <summary>
    /// Turns Windows 11 efficiency mode on or off, like Task Manager: EcoQoS power throttling plus idle priority
    /// (turning it off restores normal priority).
    /// </summary>
    public static void SetEfficiencyMode(ProcessKey process, bool enabled)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_SET_INFORMATION);
        if (handle is null)
        {
            return;
        }

        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = enabled ? PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
        };
        var priority = enabled ? PROCESS_CREATION_FLAGS.IDLE_PRIORITY_CLASS : PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS;
        if (!PInvoke.SetProcessInformation(handle, PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, MemoryMarshal.AsBytes(new ReadOnlySpan<PROCESS_POWER_THROTTLING_STATE>(in state))) ||
            !PInvoke.SetPriorityClass(handle, priority))
        {
            ThrowLastError();
        }
    }

    /// <summary>Whether efficiency mode (EcoQoS) is on. Returns false if the process cannot be queried.</summary>
    public static bool IsEfficiencyModeEnabled(ProcessKey process)
    {
        try
        {
            using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION);
            var state = new PROCESS_POWER_THROTTLING_STATE { Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION };
            return handle is not null &&
                   PInvoke.GetProcessInformation(handle, PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, MemoryMarshal.AsBytes(new Span<PROCESS_POWER_THROTTLING_STATE>(ref state))) &&
                   (state.StateMask & PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED) != 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public static void Suspend(ProcessKey process) => SuspendOrResume(process, suspend: true);

    public static void Resume(ProcessKey process) => SuspendOrResume(process, suspend: false);

    /// <summary>Writes a full memory dump to a new file in <paramref name="directory"/> and returns its path, or null if the process exited.</summary>
    public static string? CreateDumpFile(ProcessKey process, string processName, string directory)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ);
        if (handle is null)
        {
            return null;
        }

        var path = GetUniquePath(directory, Path.GetFileNameWithoutExtension(processName), ".DMP");
        using (var file = File.Create(path))
        {
            const uint dumpType = Dbghelp.MiniDumpWithFullMemory | Dbghelp.MiniDumpWithHandleData | Dbghelp.MiniDumpWithThreadInfo |
                Dbghelp.MiniDumpWithUnloadedModules | Dbghelp.MiniDumpWithFullMemoryInfo;
            if (Dbghelp.MiniDumpWriteDump(handle, (uint)process.Pid, file.SafeFileHandle, dumpType, 0, 0, 0))
            {
                return path;
            }
        }

        var error = Marshal.GetLastPInvokeError();
        File.Delete(path);
        throw new Win32Exception(error);
    }

    /// <summary>
    /// Ends the given Explorer processes and makes sure the shell comes back. Windows normally restarts it by itself;
    /// it is only started here if that did not happen. Blocks for up to a few seconds.
    /// </summary>
    public static void RestartExplorer(IEnumerable<ProcessKey> explorerProcesses)
    {
        foreach (var process in explorerProcesses)
        {
            End(process);
        }

        var sessionId = Process.GetCurrentProcess().SessionId;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            Thread.Sleep(100);
            var explorers = Process.GetProcessesByName("explorer");
            var isRunning = explorers.Any(p => p.SessionId == sessionId);
            Array.ForEach(explorers, p => p.Dispose());
            if (isRunning)
            {
                return;
            }
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose();
    }

    /// <summary>Opens the process, or returns null if it no longer exists.</summary>
    private static SafeFileHandle? Open(ProcessKey process, PROCESS_ACCESS_RIGHTS access)
    {
        var handle = PInvoke.OpenProcess_SafeHandle(access | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Pid);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error == (int)WIN32_ERROR.ERROR_INVALID_PARAMETER)
            {
                return null; // No process with this PID.
            }

            throw new Win32Exception(error);
        }

        var isOtherProcess = PInvoke.GetProcessTimes(handle, out var creationTime, out _, out _, out _) && ToInt64(creationTime) != process.CreateTime;
        // An exited process can still be opened while others hold a handle to it.
        var hasExited = PInvoke.GetExitCodeProcess(handle, out var exitCode) && exitCode != StillActive;
        if (isOtherProcess || hasExited)
        {
            handle.Dispose();
            return null;
        }

        return handle;
    }

    private static void SuspendOrResume(ProcessKey process, bool suspend)
    {
        using var handle = Open(process, PROCESS_ACCESS_RIGHTS.PROCESS_SUSPEND_RESUME);
        if (handle is null)
        {
            return;
        }

        var status = suspend ? Ntdll.NtSuspendProcess(handle) : Ntdll.NtResumeProcess(handle);
        if (status < 0)
        {
            throw new Win32Exception((int)Ntdll.RtlNtStatusToDosError(status));
        }
    }

    private static string GetUniquePath(string directory, string name, string extension)
    {
        var path = Path.Combine(directory, name + extension);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(directory, $"{name} ({i}){extension}");
        }

        return path;
    }

    private static long ToInt64(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        ((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    private static void ThrowLastError() => throw new Win32Exception(Marshal.GetLastPInvokeError());
}
