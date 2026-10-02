using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
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

    /// <summary>Opens File Explorer with the file selected.</summary>
    public static void OpenFileLocation(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true })?.Dispose();

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

    private static long ToInt64(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        ((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    private static void ThrowLastError() => throw new Win32Exception(Marshal.GetLastPInvokeError());
}
