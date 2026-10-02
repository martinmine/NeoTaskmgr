using System.Runtime.InteropServices;

namespace NeoTaskmgr.Core.Interop;

/// <summary>
/// dbghelp.dll declarations. CsWin32 only generates MiniDumpWriteDump for a specific CPU architecture (its optional
/// structs differ per platform); they are not used here, so they are declared as plain pointers.
/// </summary>
internal static partial class Dbghelp
{
    // MINIDUMP_TYPE flags
    public const uint MiniDumpWithHandleData = 0x4;
    public const uint MiniDumpWithFullMemory = 0x2;
    public const uint MiniDumpWithUnloadedModules = 0x20;
    public const uint MiniDumpWithFullMemoryInfo = 0x800;
    public const uint MiniDumpWithThreadInfo = 0x1000;

    [LibraryImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MiniDumpWriteDump(
        SafeHandle process, uint processId, SafeHandle file, uint dumpType, nint exceptionParam, nint userStreamParam, nint callbackParam);
}
