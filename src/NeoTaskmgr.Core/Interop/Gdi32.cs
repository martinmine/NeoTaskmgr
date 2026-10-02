using System.Runtime.InteropServices;

namespace NeoTaskmgr.Core.Interop;

/// <summary>
/// The kernel-mode graphics adapter queries (D3DKMT) in gdi32.dll, used to list GPUs without COM.
/// These are declared in the Windows Driver Kit headers, which CsWin32 does not cover.
/// </summary>
internal static unsafe partial class Gdi32
{
    public const int KmtQaiTypeSegmentSizeInfo = 3;
    public const int KmtQaiTypeAdapterRegistryInfo = 8;
    public const int KmtQaiTypeAdapterType = 15;

    // D3DKMT_ADAPTERTYPE bit flags
    public const uint AdapterTypeRenderSupported = 1 << 0;
    public const uint AdapterTypeSoftwareDevice = 1 << 2;

    [LibraryImport("gdi32.dll")]
    public static partial int D3DKMTEnumAdapters2(D3DKMT_ENUMADAPTERS2* enumAdapters);

    [LibraryImport("gdi32.dll")]
    public static partial int D3DKMTQueryAdapterInfo(D3DKMT_QUERYADAPTERINFO* queryAdapterInfo);

    [LibraryImport("gdi32.dll")]
    public static partial int D3DKMTCloseAdapter(uint* adapter);
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3DKMT_ADAPTERINFO
{
    public uint Adapter;
    public uint LuidLowPart;
    public int LuidHighPart;
    public uint NumOfSources;
    public int PrecisePresentRegionsPreferred;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct D3DKMT_ENUMADAPTERS2
{
    public uint NumAdapters;
    public D3DKMT_ADAPTERINFO* Adapters;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct D3DKMT_QUERYADAPTERINFO
{
    public uint Adapter;
    public int Type;
    public void* PrivateDriverData;
    public uint PrivateDriverDataSize;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct D3DKMT_ADAPTERREGISTRYINFO
{
    public fixed char AdapterString[260];
    public fixed char BiosString[260];
    public fixed char DacType[260];
    public fixed char ChipType[260];
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3DKMT_SEGMENTSIZEINFO
{
    public ulong DedicatedVideoMemorySize;
    public ulong DedicatedSystemMemorySize;
    public ulong SharedSystemMemorySize;
}
