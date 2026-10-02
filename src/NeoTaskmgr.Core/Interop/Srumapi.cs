using System.Runtime.InteropServices;

namespace NeoTaskmgr.Core.Interop;

/// <summary>
/// srumapi.dll, the client of the System Resource Usage Monitor (SRUM) that Task Manager's App history reads. The API
/// is undocumented and not in the Windows SDK metadata; the declarations below were worked out from srumapi.dll and
/// taskmgr.exe. The service keeps the data of about the last 30 days and answers queries for any user's records only
/// for administrators.
/// </summary>
internal static unsafe partial class Srumapi
{
    /// <summary>Records of the current user.</summary>
    public const uint ScopeCurrentUser = 1;

    /// <summary>Records of all users; fails with ERROR_ACCESS_DENIED without administrator rights.</summary>
    public const uint ScopeAllUsers = 2;

    /// <summary>Returns the records with a timestamp between <paramref name="start"/> and <paramref name="end"/> (UTC).</summary>
    /// <returns>A Win32 error code.</returns>
    [LibraryImport("srumapi.dll")]
    public static partial uint SruQueryStats(SruProvider provider, SYSTEMTIME* start, SYSTEMTIME* end, uint scope, SRU_RECORD_SET** recordSet);

    [LibraryImport("srumapi.dll")]
    public static partial void SruFreeRecordSet(SRU_RECORD_SET* recordSet);
}

/// <summary>The SRUM data providers (tables) that App history uses.</summary>
internal enum SruProvider : uint
{
    /// <summary>Values: 0 interface LUID, 1 L2 profile ID, 2 L2 profile flags, 3 bytes sent, 4 bytes received.</summary>
    NetworkUsage = 0,

    /// <summary>Values: 0 foreground cycle time, 1 background cycle time, 2 face time, then context switches and I/O.</summary>
    ResourceUsage = 1,

    /// <summary>Values: 0 notification type, 1 payload size in bytes, 2 network type.</summary>
    PushNotifications = 2,
}

/// <summary>What <see cref="SRU_RECORD.AppName"/> identifies.</summary>
internal enum SruAppKind : uint
{
    /// <summary>A program: its NT path such as \Device\HarddiskVolume3\Windows\explorer.exe.</summary>
    Program = 0,

    /// <summary>A service name.</summary>
    Service = 1,

    /// <summary>A packaged app's package full name.</summary>
    Package = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEMTIME
{
    public ushort Year;
    public ushort Month;
    public ushort DayOfWeek;
    public ushort Day;
    public ushort Hour;
    public ushort Minute;
    public ushort Second;
    public ushort Milliseconds;

    public static SYSTEMTIME FromDateTime(DateTime utc) => new()
    {
        Year = (ushort)utc.Year,
        Month = (ushort)utc.Month,
        DayOfWeek = (ushort)utc.DayOfWeek,
        Day = (ushort)utc.Day,
        Hour = (ushort)utc.Hour,
        Minute = (ushort)utc.Minute,
        Second = (ushort)utc.Second,
        Milliseconds = (ushort)utc.Millisecond,
    };

    public readonly DateTime ToDateTime() => new(Year, Month, Day, Hour, Minute, Second, Milliseconds, DateTimeKind.Utc);
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SRU_RECORD_SET
{
    public uint Count;
    public SRU_RECORD* Records;
}

/// <summary>The usage of one app during one interval (a minute for the last hour, then about an hour).</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SRU_RECORD
{
    public SYSTEMTIME Timestamp;
    public SruAppKind AppKind;
    public char* AppName;
    public uint SidLength;
    public void* Sid;
    public ushort ValueCount;
    public SRU_VALUE* Values;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SRU_VALUE
{
    /// <summary>The column within the provider's table.</summary>
    public uint Id;

    /// <summary>0 and 1 for numbers; other types (such as 3, a blob) point to <see cref="Data"/>.</summary>
    public uint Type;

    public ulong Value;
    public void* Data;
}
