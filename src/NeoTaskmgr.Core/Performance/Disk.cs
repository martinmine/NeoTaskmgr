using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Ioctl;

namespace NeoTaskmgr.Core.Performance;

public enum DiskKind
{
    Unknown,
    Hdd,
    Ssd,
}

/// <summary>Disk properties that do not change. Model and capacity are null when the disk cannot be queried.</summary>
/// <param name="FormattedBytes">Total size of the formatted volumes.</param>
public sealed record DiskInfo(string? Model, DiskKind Kind, bool IsNvme, long? CapacityBytes, long FormattedBytes, bool IsSystemDisk);

public sealed record DiskSample
{
    public required int Number { get; init; }

    /// <summary>Drive letters of the volumes on the disk, such as "C:".</summary>
    public required IReadOnlyList<string> Volumes { get; init; }

    public required DiskInfo Info { get; init; }
    public double ActivePercent { get; init; }
    public double ReadBytesPerSecond { get; init; }
    public double WriteBytesPerSecond { get; init; }
    public double ResponseTimeMs { get; init; }
}

internal static class DiskReader
{
    private static readonly string SystemDrive = Path.GetPathRoot(Environment.SystemDirectory)!.TrimEnd('\\');

    /// <summary>
    /// Combines the PhysicalDisk performance counters (instances such as "0 C: D:") into one sample per disk.
    /// </summary>
    /// <param name="getInfo">Returns the (cached) info for a disk number and its volumes.</param>
    public static List<DiskSample> Read(
        IEnumerable<(string Instance, double Value)> idlePercent,
        IEnumerable<(string Instance, double Value)> readBytes,
        IEnumerable<(string Instance, double Value)> writeBytes,
        IEnumerable<(string Instance, double Value)> secondsPerTransfer,
        Func<string, int, string[], DiskInfo> getInfo)
    {
        var read = readBytes.ToDictionary(v => v.Instance, v => v.Value);
        var write = writeBytes.ToDictionary(v => v.Instance, v => v.Value);
        var response = secondsPerTransfer.ToDictionary(v => v.Instance, v => v.Value);

        var disks = new List<DiskSample>();
        foreach (var (instance, idle) in idlePercent)
        {
            if (ParseInstance(instance) is not { } disk)
            {
                continue;
            }

            disks.Add(new DiskSample
            {
                Number = disk.Number,
                Volumes = disk.Volumes,
                Info = getInfo(instance, disk.Number, disk.Volumes),
                ActivePercent = Math.Clamp(100 - idle, 0, 100),
                ReadBytesPerSecond = read.GetValueOrDefault(instance),
                WriteBytesPerSecond = write.GetValueOrDefault(instance),
                ResponseTimeMs = response.GetValueOrDefault(instance) * 1000,
            });
        }

        disks.Sort((a, b) => a.Number.CompareTo(b.Number));
        return disks;
    }

    /// <summary>Parses a PhysicalDisk instance name such as "0 C: D:"; null for "_Total".</summary>
    internal static (int Number, string[] Volumes)? ParseInstance(string instance)
    {
        var parts = instance.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (number, parts[1..])
            : null;
    }

    /// <summary>Queries the disk's model, type and sizes.</summary>
    public static unsafe DiskInfo ReadInfo(int number, string[] volumes)
    {
        var formattedBytes = volumes.Sum(GetVolumeSize);
        var isSystemDisk = volumes.Contains(SystemDrive, StringComparer.OrdinalIgnoreCase);

        // Access 0 is enough for these queries, so no administrator rights are needed.
        using var disk = PInvoke.CreateFile($@"\\.\PhysicalDrive{number}", 0, FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null, FILE_CREATION_DISPOSITION.OPEN_EXISTING, 0, null);
        if (disk.IsInvalid)
        {
            return new DiskInfo(null, DiskKind.Unknown, false, null, formattedBytes, isSystemDisk);
        }

        var (model, isNvme) = QueryDevice(disk);
        return new DiskInfo(model, QueryKind(disk), isNvme, QueryCapacity(disk), formattedBytes, isSystemDisk);
    }

    private static unsafe (string? Model, bool IsNvme) QueryDevice(SafeFileHandle disk)
    {
        Span<byte> buffer = stackalloc byte[1024];
        if (!Query(disk, STORAGE_PROPERTY_ID.StorageDeviceProperty, buffer))
        {
            return (null, false);
        }

        fixed (byte* data = buffer)
        {
            var descriptor = (STORAGE_DEVICE_DESCRIPTOR*)data;
            var vendor = ReadString(buffer, descriptor->VendorIdOffset);
            var product = ReadString(buffer, descriptor->ProductIdOffset);
            var model = string.Join(' ', new[] { vendor, product }.Where(s => !string.IsNullOrEmpty(s)));
            return (model.Length > 0 ? model : null, descriptor->BusType == STORAGE_BUS_TYPE.BusTypeNvme);
        }
    }

    private static unsafe DiskKind QueryKind(SafeFileHandle disk)
    {
        Span<byte> buffer = stackalloc byte[sizeof(DEVICE_SEEK_PENALTY_DESCRIPTOR)];
        if (!Query(disk, STORAGE_PROPERTY_ID.StorageDeviceSeekPenaltyProperty, buffer))
        {
            return DiskKind.Unknown;
        }

        return MemoryMarshal.Read<DEVICE_SEEK_PENALTY_DESCRIPTOR>(buffer).IncursSeekPenalty ? DiskKind.Hdd : DiskKind.Ssd;
    }

    private static unsafe long? QueryCapacity(SafeFileHandle disk)
    {
        Span<byte> buffer = stackalloc byte[256];
        return PInvoke.DeviceIoControl(disk, PInvoke.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, default, buffer, out _, null)
            ? MemoryMarshal.Read<DISK_GEOMETRY_EX>(buffer).DiskSize
            : null;
    }

    private static unsafe bool Query(SafeFileHandle disk, STORAGE_PROPERTY_ID property, Span<byte> output)
    {
        var query = new STORAGE_PROPERTY_QUERY { PropertyId = property, QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery };
        return PInvoke.DeviceIoControl(disk, PInvoke.IOCTL_STORAGE_QUERY_PROPERTY,
            MemoryMarshal.AsBytes(new ReadOnlySpan<STORAGE_PROPERTY_QUERY>(in query)), output, out _, null);
    }

    /// <summary>Reads a zero-terminated ASCII string at an offset in the descriptor (0 means none).</summary>
    private static string? ReadString(ReadOnlySpan<byte> buffer, uint offset)
    {
        if (offset == 0 || offset >= buffer.Length)
        {
            return null;
        }

        var text = buffer[(int)offset..];
        var end = text.IndexOf((byte)0);
        return System.Text.Encoding.ASCII.GetString(end < 0 ? text : text[..end]).Trim();
    }

    private static long GetVolumeSize(string driveLetter)
    {
        try
        {
            var drive = new DriveInfo(driveLetter);
            return drive.IsReady ? drive.TotalSize : 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 0;
        }
    }
}
