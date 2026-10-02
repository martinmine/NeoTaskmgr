using System.ComponentModel;
using System.Security.Principal;
using Microsoft.Win32;
using NeoTaskmgr.Core.Interop;
using Windows.Win32;

namespace NeoTaskmgr.Core.AppHistory;

/// <summary>
/// Reads App history: the CPU time, network traffic and notifications of each app, as recorded by the System Resource
/// Usage Monitor (SRUM) – the same data Task Manager shows. Like Task Manager it covers the current user and the system
/// accounts, which needs administrator rights; without them only the current user's usage is read.
/// </summary>
public sealed class AppHistoryProvider
{
    /// <summary>How far back SRUM keeps usage, and so the longest period App history covers.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>The usage since <paramref name="sinceUtc"/>, or since the start of the retention period if that is later.</summary>
    /// <exception cref="Win32Exception">SRUM could not be queried, for example because its service is stopped.</exception>
    public AppHistorySnapshot Read(DateTime sinceUtc)
    {
        var now = DateTime.UtcNow;
        var start = sinceUtc > now - Retention ? sinceUtc : now - Retention;

        var scope = Elevation.IsElevated ? Srumapi.ScopeAllUsers : Srumapi.ScopeCurrentUser;
        var records = new List<AppUsageRecord>();
        if (!TryRead(start, now, scope, records))
        {
            scope = Srumapi.ScopeCurrentUser; // All users failed (for example denied by policy); the user's own usage still helps.
            records.Clear();
            ReadAll(start, now, scope, records);
        }

        using var identity = WindowsIdentity.GetCurrent();
        var currentUser = identity.User?.Value;
        var paths = DevicePathConverter.Create();
        var entries = AppHistoryAggregator.Aggregate(records, ReadCyclesPerSecond(), sid => IsIncluded(sid, currentUser), paths.ToDosPath);
        return new AppHistorySnapshot(start, scope == Srumapi.ScopeAllUsers, entries);
    }

    /// <summary>Task Manager's App history shows the current user and the built-in service accounts, not other users.</summary>
    internal static bool IsIncluded(string? sid, string? currentUser) =>
        sid is null or "S-1-5-18" or "S-1-5-19" or "S-1-5-20" || string.Equals(sid, currentUser, StringComparison.OrdinalIgnoreCase);

    private static bool TryRead(DateTime start, DateTime end, uint scope, List<AppUsageRecord> records)
    {
        try
        {
            ReadAll(start, end, scope, records);
            return true;
        }
        catch (Win32Exception) when (scope != Srumapi.ScopeCurrentUser)
        {
            return false;
        }
    }

    private static void ReadAll(DateTime start, DateTime end, uint scope, List<AppUsageRecord> records)
    {
        SrumReader.Read(SruProvider.ResourceUsage, start, end, scope, records);
        SrumReader.Read(SruProvider.NetworkUsage, start, end, scope, records);
        SrumReader.Read(SruProvider.PushNotifications, start, end, scope, records);
    }

    /// <summary>
    /// SRUM measures CPU usage in processor cycles; they are converted to time at the processor's base speed, which is
    /// the rate of the cycle counter.
    /// </summary>
    private static double ReadCyclesPerSecond()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return key?.GetValue("~MHz") is int mhz and > 0 ? mhz * 1e6 : 1e9;
    }
}

/// <summary>The usage of one app during one SRUM interval, reduced to the values App history shows.</summary>
internal readonly record struct AppUsageRecord(
    DateTime TimestampUtc,
    SruAppKind Kind,
    string AppName,
    string? UserSid,
    ulong CycleTime = 0,
    ulong BytesSent = 0,
    ulong BytesReceived = 0,
    ulong NotificationBytes = 0);

/// <summary>Sums SRUM records into one <see cref="AppHistoryEntry"/> per app.</summary>
internal static class AppHistoryAggregator
{
    public static List<AppHistoryEntry> Aggregate(
        IEnumerable<AppUsageRecord> records, double cyclesPerSecond, Func<string?, bool> includeUser, Func<string, string?> toDosPath)
    {
        var apps = new Dictionary<string, App>();
        foreach (var record in records)
        {
            if (record.AppName.Length == 0 || !includeUser(record.UserSid))
            {
                continue;
            }

            var key = GetKey(record.Kind, record.AppName);
            if (!apps.TryGetValue(key, out var app))
            {
                apps[key] = app = new App(record.Kind, GetName(record.Kind, record.AppName));
            }

            app.Add(record);
        }

        return apps.Select(pair => pair.Value.ToEntry(pair.Key, cyclesPerSecond, toDosPath)).ToList();
    }

    /// <summary>
    /// Programs are keyed by path, but without version folders such as "app-1.2.3" or "2.1.247" and hash folders such
    /// as "04c0d99f4f", so an app that updates itself into a new folder stays one row – like in Task Manager. Packaged
    /// apps are keyed by package family, which does not change with the version.
    /// </summary>
    internal static string GetKey(SruAppKind kind, string appName) => kind switch
    {
        SruAppKind.Package => "package:" + GetPackageFamilyName(appName).ToUpperInvariant(),
        SruAppKind.Service => "service:" + appName.ToUpperInvariant(),
        _ => "program:" + GetProgramKey(appName.ToUpperInvariant().Split('\\')),
    };

    private static string GetProgramKey(string[] parts) =>
        string.Join('\\', parts.Where((part, i) => i == parts.Length - 1 || !IsVersionFolder(part)));

    /// <summary>A package full name is Name_Version_Architecture_ResourceId_PublisherId; the family is Name_PublisherId.</summary>
    internal static string GetPackageFamilyName(string packageFullName)
    {
        var parts = packageFullName.Split('_');
        return parts.Length == 5 ? $"{parts[0]}_{parts[4]}" : packageFullName;
    }

    private static string GetName(SruAppKind kind, string appName) => kind switch
    {
        SruAppKind.Package => appName.Split('_')[0],
        SruAppKind.Service => appName,
        _ => appName[(appName.LastIndexOf('\\') + 1)..],
    };

    private static bool IsVersionFolder(string folder) =>
        folder.Any(char.IsAsciiDigit) && (folder.Contains('.') || (folder.Length >= 8 && folder.All(char.IsAsciiHexDigit)));

    private sealed class App(SruAppKind kind, string name)
    {
        private string _name = name;
        private string? _latestPath;
        private DateTime _latestPathTime = DateTime.MinValue;
        private ulong _cycles;
        private ulong _sent;
        private ulong _received;
        private ulong _notifications;

        public void Add(AppUsageRecord record)
        {
            _cycles += record.CycleTime;
            _sent += record.BytesSent;
            _received += record.BytesReceived;
            _notifications += record.NotificationBytes;

            // Some tables store paths in lower case; prefer the original casing for the name.
            var name = GetName(kind, record.AppName);
            if (_name == _name.ToLowerInvariant() && name != name.ToLowerInvariant())
            {
                _name = name;
            }

            if (kind == SruAppKind.Program && record.TimestampUtc >= _latestPathTime)
            {
                _latestPath = record.AppName;
                _latestPathTime = record.TimestampUtc;
            }
        }

        public AppHistoryEntry ToEntry(string key, double cyclesPerSecond, Func<string, string?> toDosPath) => new(
            key,
            kind switch
            {
                SruAppKind.Package => AppHistoryKind.Package,
                SruAppKind.Service => AppHistoryKind.Service,
                _ => AppHistoryKind.Program,
            },
            _name,
            _latestPath is null ? null : toDosPath(_latestPath),
            kind == SruAppKind.Package ? key["package:".Length..] : null,
            TimeSpan.FromSeconds(Math.Round(_cycles / cyclesPerSecond)),
            (long)_sent,
            (long)_received,
            (long)_notifications);
    }
}

/// <summary>Copies SRUM records out of the native record set.</summary>
internal static unsafe class SrumReader
{
    public static void Read(SruProvider provider, DateTime start, DateTime end, uint scope, List<AppUsageRecord> records)
    {
        var startTime = SYSTEMTIME.FromDateTime(start);
        var endTime = SYSTEMTIME.FromDateTime(end);
        SRU_RECORD_SET* set = null;
        var error = Srumapi.SruQueryStats(provider, &startTime, &endTime, scope, &set);
        if (error != 0)
        {
            throw new Win32Exception((int)error);
        }

        if (set is null)
        {
            return; // No usage in the period.
        }

        try
        {
            for (var i = 0; i < set->Count; i++)
            {
                var record = &set->Records[i];
                if (record->AppName is not null)
                {
                    records.Add(Convert(provider, record));
                }
            }
        }
        finally
        {
            Srumapi.SruFreeRecordSet(set);
        }
    }

    private static AppUsageRecord Convert(SruProvider provider, SRU_RECORD* record)
    {
        var result = new AppUsageRecord(
            record->Timestamp.ToDateTime(),
            record->AppKind,
            new string(record->AppName),
            record->Sid is null || record->SidLength == 0 ? null : new SecurityIdentifier((nint)record->Sid).Value);

        return provider switch
        {
            SruProvider.ResourceUsage => result with { CycleTime = GetValue(record, 0) + GetValue(record, 1) },
            SruProvider.NetworkUsage => result with { BytesSent = GetValue(record, 3), BytesReceived = GetValue(record, 4) },
            _ => result with { NotificationBytes = GetValue(record, 1) },
        };
    }

    private static ulong GetValue(SRU_RECORD* record, uint id)
    {
        for (var i = 0; i < record->ValueCount; i++)
        {
            var value = &record->Values[i];
            if (value->Id == id)
            {
                return value->Type <= 1 ? value->Value : 0;
            }
        }

        return 0;
    }
}

/// <summary>Converts NT paths such as \Device\HarddiskVolume3\Windows to drive-letter paths such as C:\Windows.</summary>
internal sealed class DevicePathConverter(IReadOnlyDictionary<string, string> devices)
{
    /// <summary>Maps the devices of the drive letters that exist now.</summary>
    public static DevicePathConverter Create()
    {
        var devices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Span<char> target = stackalloc char[1024];
        foreach (var drive in Environment.GetLogicalDrives())
        {
            var letter = drive[..2]; // "C:"
            var length = PInvoke.QueryDosDevice(letter, target);
            if (length > 0)
            {
                devices.TryAdd(target[..target.IndexOf('\0')].ToString(), letter);
            }
        }

        return new DevicePathConverter(devices);
    }

    /// <summary>Returns null when the path is not on a drive (for example on a removed disk).</summary>
    public string? ToDosPath(string ntPath)
    {
        if (!ntPath.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var end = ntPath.IndexOf('\\', @"\Device\".Length);
        return end > 0 && devices.TryGetValue(ntPath[..end], out var drive) ? drive + ntPath[end..] : null;
    }
}
