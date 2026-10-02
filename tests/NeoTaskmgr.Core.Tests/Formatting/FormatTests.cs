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
}
