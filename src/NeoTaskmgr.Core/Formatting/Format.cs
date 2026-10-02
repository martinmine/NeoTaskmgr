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

    public static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
