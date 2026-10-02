using System.Runtime.InteropServices;

namespace NeoTaskmgr.Core.Interop;

/// <summary>
/// ntdll.dll declarations that CsWin32 cannot generate usefully: the Windows SDK only exposes these structs with
/// "Reserved" fields, so the full layouts (as used by Process Explorer / System Informer) are declared here.
/// </summary>
internal static unsafe partial class Ntdll
{
    public const int SystemProcessInformationClass = 5;
    public const int ProcessCommandLineInformationClass = 60;
    public const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    [LibraryImport("ntdll.dll")]
    public static partial int NtQuerySystemInformation(int systemInformationClass, void* systemInformation, uint systemInformationLength, out uint returnLength);

    [LibraryImport("ntdll.dll")]
    public static partial int NtQueryInformationProcess(SafeHandle processHandle, int processInformationClass, void* processInformation, uint processInformationLength, out uint returnLength);
}

[StructLayout(LayoutKind.Sequential)]
internal struct UNICODE_STRING
{
    public ushort Length;
    public ushort MaximumLength;
    public nint Buffer;

    public readonly unsafe string? ToStringOrNull() => Buffer == 0 ? null : new string((char*)Buffer, 0, Length / sizeof(char));
}

/// <summary>One entry of the SystemProcessInformation list; followed in memory by NumberOfThreads <see cref="SYSTEM_THREAD_INFORMATION"/>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_PROCESS_INFORMATION
{
    public uint NextEntryOffset;
    public uint NumberOfThreads;
    public long WorkingSetPrivateSize;
    public uint HardFaultCount;
    public uint NumberOfThreadsHighWatermark;
    public ulong CycleTime;
    public long CreateTime;
    public long UserTime;
    public long KernelTime;
    public UNICODE_STRING ImageName;
    public int BasePriority;
    public nint UniqueProcessId;
    public nint InheritedFromUniqueProcessId;
    public uint HandleCount;
    public uint SessionId;
    public nuint UniqueProcessKey;
    public nuint PeakVirtualSize;
    public nuint VirtualSize;
    public uint PageFaultCount;
    public nuint PeakWorkingSetSize;
    public nuint WorkingSetSize;
    public nuint QuotaPeakPagedPoolUsage;
    public nuint QuotaPagedPoolUsage;
    public nuint QuotaPeakNonPagedPoolUsage;
    public nuint QuotaNonPagedPoolUsage;
    public nuint PagefileUsage;
    public nuint PeakPagefileUsage;
    public nuint PrivatePageCount;
    public long ReadOperationCount;
    public long WriteOperationCount;
    public long OtherOperationCount;
    public long ReadTransferCount;
    public long WriteTransferCount;
    public long OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_THREAD_INFORMATION
{
    public const uint StateWaiting = 5;
    public const uint WaitReasonSuspended = 5;

    public long KernelTime;
    public long UserTime;
    public long CreateTime;
    public uint WaitTime;
    public nint StartAddress;
    public nint UniqueProcess;
    public nint UniqueThread;
    public int Priority;
    public int BasePriority;
    public uint ContextSwitches;
    public uint ThreadState;
    public uint WaitReason;
}
