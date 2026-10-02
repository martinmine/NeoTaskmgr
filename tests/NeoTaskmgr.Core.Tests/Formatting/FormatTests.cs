using System.Globalization;
using NeoTaskmgr.Core.Formatting;

namespace NeoTaskmgr.Core.Tests.Formatting;

public sealed class FormatTests
{
    public FormatTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0, "0 K")]
    [InlineData(1023, "0 K")]
    [InlineData(12_345 * 1024L, "12,345 K")]
    public void Kilobytes(long bytes, string expected) => Assert.Equal(expected, Format.Kilobytes(bytes));

    [Theory]
    [InlineData(0, "00")]
    [InlineData(7.4, "07")]
    [InlineData(99.6, "100")]
    public void CpuPercent(double percent, string expected) => Assert.Equal(expected, Format.CpuPercent(percent));

    [Theory]
    [InlineData(0, "0:00:00")]
    [InlineData(3723, "1:02:03")]
    [InlineData(123 * 3600, "123:00:00")]
    public void Duration(int seconds, string expected) => Assert.Equal(expected, Format.Duration(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(0, "0.0%")]
    [InlineData(12.345, "12.3%")]
    [InlineData(100, "100.0%")]
    public void Percent(double percent, string expected) => Assert.Equal(expected, Format.Percent(percent));

    [Theory]
    [InlineData(0, "0.0 MB")]
    [InlineData(1024 * 1024, "1.0 MB")]
    [InlineData(1536 * 1024, "1.5 MB")]
    [InlineData(1234L * 1024 * 1024, "1,234.0 MB")]
    public void Megabytes(long bytes, string expected) => Assert.Equal(expected, Format.Megabytes(bytes));

    [Theory]
    [InlineData(0, "0 MB")]
    [InlineData(200 * 1024, "0.2 MB")]
    public void UsageMegabytes(long bytes, string expected) => Assert.Equal(expected, Format.UsageMegabytes(bytes));

    [Theory]
    [InlineData(500, "500 bytes")]
    [InlineData(512 * 1024, "512 KB")]
    [InlineData(12L * 1024 * 1024, "12 MB")]
    [InlineData(16_965_120_000L, "15.8 GB")]
    [InlineData(2L * 1024 * 1024 * 1024 * 1024, "2.0 TB")]
    public void Size(long bytes, string expected) => Assert.Equal(expected, Format.Size(bytes));

    [Theory]
    [InlineData(0, "0 Kbps")]
    [InlineData(8_000, "8 Kbps")]
    [InlineData(1_200_000, "1.2 Mbps")]
    [InlineData(1_000_000_000, "1.0 Gbps")]
    public void BitsPerSecond(double bits, string expected) => Assert.Equal(expected, Format.BitsPerSecond(bits));

    [Theory]
    [InlineData(512 * 1024, "512 KB/s")]
    [InlineData(3 * 1024 * 1024, "3.0 MB/s")]
    public void BytesPerSecond(double bytes, string expected) => Assert.Equal(expected, Format.BytesPerSecond(bytes));

    [Fact]
    public void Megabits() => Assert.Equal("1.0 Mbps", Format.Megabits(125_000));

    [Fact]
    public void Gigahertz() => Assert.Equal("3.20 GHz", Format.Gigahertz(3200));

    [Fact]
    public void Uptime() => Assert.Equal("2:04:05:06", Format.Uptime(new TimeSpan(2, 4, 5, 6)));
}