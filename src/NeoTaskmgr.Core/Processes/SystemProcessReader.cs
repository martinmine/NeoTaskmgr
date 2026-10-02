using System.Runtime.InteropServices;
using NeoTaskmgr.Core.Interop;

namespace NeoTaskmgr.Core.Processes;

/// <summary>
/// Reads counters for all processes with a single NtQuerySystemInformation call, which is far cheaper than
/// querying each process individually.
/// </summary>
internal sealed unsafe class SystemProcessReader : IDisposable
{
    private void* _buffer;
    private uint _bufferSize = 1024 * 1024;

    public List<ProcessCounters> Read()
    {
        Query();

        var processes = new List<ProcessCounters>(512);
        var entry = (byte*)_buffer;
        while (true)
        {
            var process = (SYSTEM_PROCESS_INFORMATION*)entry;
            processes.Add(ToCounters(process));

            if (process->NextEntryOffset == 0)
            {
                return processes;
            }

            entry += process->NextEntryOffset;
        }
    }

    public void Dispose()
    {
        NativeMemory.Free(_buffer);
        _buffer = null;
    }

    private void Query()
    {
        while (true)
        {
            if (_buffer == null)
            {
                _buffer = NativeMemory.Alloc(_bufferSize);
            }

            var status = Ntdll.NtQuerySystemInformation(Ntdll.SystemProcessInformationClass, _buffer, _bufferSize, out var needed);
            if (status != Ntdll.StatusInfoLengthMismatch)
            {
                if (status < 0)
                {
                    throw new InvalidOperationException($"NtQuerySystemInformation failed with status 0x{status:X8}.");
                }

                return;
            }

            // Processes may start between calls, so grow with some headroom.
            NativeMemory.Free(_buffer);
            _buffer = null;
            _bufferSize = Math.Max(_bufferSize * 2, needed + 64 * 1024);
        }
    }

    private static ProcessCounters ToCounters(SYSTEM_PROCESS_INFORMATION* process)
    {
        var pid = (int)process->UniqueProcessId;
        return new ProcessCounters
        {
            Pid = pid,
            ParentPid = (int)process->InheritedFromUniqueProcessId,
            CreateTime = process->CreateTime,
            Name = process->ImageName.ToStringOrNull() ?? (pid == 0 ? "System Idle Process" : string.Empty),
            IsSuspended = IsSuspended(process),
            SessionId = (int)process->SessionId,
            ThreadCount = (int)process->NumberOfThreads,
            HandleCount = (int)process->HandleCount,
            BasePriority = process->BasePriority,
            PrivateWorkingSet = process->WorkingSetPrivateSize,
            WorkingSet = (long)process->WorkingSetSize,
            PeakWorkingSet = (long)process->PeakWorkingSetSize,
            CommitSize = (long)process->PagefileUsage,
            CpuTime = TimeSpan.FromTicks(process->KernelTime + process->UserTime),
        };
    }

    /// <summary>A process is suspended when all of its threads are waiting with the "Suspended" reason.</summary>
    private static bool IsSuspended(SYSTEM_PROCESS_INFORMATION* process)
    {
        if (process->NumberOfThreads == 0 || process->UniqueProcessId == 0)
        {
            return false;
        }

        var threads = (SYSTEM_THREAD_INFORMATION*)(process + 1);
        for (var i = 0; i < process->NumberOfThreads; i++)
        {
            if (threads[i].ThreadState != SYSTEM_THREAD_INFORMATION.StateWaiting ||
                threads[i].WaitReason != SYSTEM_THREAD_INFORMATION.WaitReasonSuspended)
            {
                return false;
            }
        }

        return true;
    }
}
