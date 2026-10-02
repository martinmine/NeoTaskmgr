using System.Globalization;

namespace NeoTaskmgr.Core.Formatting;

/// <summary>Value formatting matching Task Manager's conventions, using the current culture.</summary>
public static class Format
{
    /// <summary>Bytes as whole kilobytes, e.g. "12,345 K".</summary>
    public static string Kilobytes(long bytes) => string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024:N0} K");

    /// <summary>CPU usage as a two-digit whole percentage, e.g. "07".</summary>
    public static string CpuPercent(double percent) => Math.Round(percent).ToString("00", CultureInfo.CurrentCulture);

    /// <summary>A duration as hours:minutes:seconds, e.g. "1:02:03" or "123:00:00".</summary>
    public static string Duration(TimeSpan duration) =>
        string.Create(CultureInfo.CurrentCulture, $"{(long)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}");

    /// <summary>A percentage with one decimal, e.g. "12.3%".</summary>
    public static string Percent(double percent) => string.Create(CultureInfo.CurrentCulture, $"{percent:0.0}%");

    /// <summary>A whole percentage, e.g. "45%".</summary>
    public static string WholePercent(double percent) => string.Create(CultureInfo.CurrentCulture, $"{Math.Round(percent):0}%");

    /// <summary>Bytes as megabytes with one decimal, e.g. "123.4 MB".</summary>
    public static string Megabytes(long bytes) => string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024.0 * 1024.0):N1} MB");

    /// <summary>Total usage in megabytes like Task Manager's App history: "0 MB" for none, else e.g. "0.2 MB".</summary>
    public static string UsageMegabytes(long bytes) => bytes == 0 ? "0 MB" : Megabytes(bytes);

    /// <summary>A size with a suitable unit and one decimal for GB and up, e.g. "512 KB", "12 MB", "15.8 GB".</summary>
    public static string Size(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString(unit >= 3 ? "0.0" : "0", CultureInfo.CurrentCulture)} {units[unit]}";
    }

    /// <summary>A rate in megabytes per second, e.g. "0.1 MB/s".</summary>
    public static string MegabytesPerSecond(double bytesPerSecond) =>
        string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond / (1024 * 1024):0.0} MB/s");

    /// <summary>A rate in kilobytes or megabytes per second, e.g. "512 KB/s" or "12.3 MB/s".</summary>
    public static string BytesPerSecond(double bytesPerSecond) => bytesPerSecond < 1024 * 1024
        ? string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond / 1024:0} KB/s")
        : string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond / (1024 * 1024):0.0} MB/s");

    /// <summary>A rate in megabits per second, e.g. "0.1 Mbps".</summary>
    public static string Megabits(double bytesPerSecond) =>
        string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond * 8 / 1_000_000:0.0} Mbps");

    /// <summary>A network speed with a suitable unit, e.g. "8 Kbps", "1.2 Mbps", "1.0 Gbps".</summary>
    public static string BitsPerSecond(double bitsPerSecond) => bitsPerSecond switch
    {
        >= 1_000_000_000 => string.Create(CultureInfo.CurrentCulture, $"{bitsPerSecond / 1_000_000_000:0.0} Gbps"),
        >= 1_000_000 => string.Create(CultureInfo.CurrentCulture, $"{bitsPerSecond / 1_000_000:0.0} Mbps"),
        _ => string.Create(CultureInfo.CurrentCulture, $"{bitsPerSecond / 1000:0} Kbps"),
    };

    /// <summary>A clock speed in GHz, e.g. "3.20 GHz".</summary>
    public static string Gigahertz(double megahertz) => string.Create(CultureInfo.CurrentCulture, $"{megahertz / 1000:0.00} GHz");

    /// <summary>An uptime as days:hours:minutes:seconds, e.g. "2:04:05:06".</summary>
    public static string Uptime(TimeSpan uptime) =>
        string.Create(CultureInfo.CurrentCulture, $"{uptime.Days}:{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}");

    public static string Milliseconds(double milliseconds) => string.Create(CultureInfo.CurrentCulture, $"{milliseconds:0.0} ms");

    public static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
