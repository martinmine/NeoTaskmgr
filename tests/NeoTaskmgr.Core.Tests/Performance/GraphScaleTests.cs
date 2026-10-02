using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.Core.Tests.Performance;

public sealed class GraphScaleTests
{
    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(50, 100, 100)]
    [InlineData(101, 100, 200)]
    [InlineData(250, 100, 500)]
    [InlineData(500, 100, 500)]
    [InlineData(501, 100, 1000)]
    [InlineData(1_500_000, 100, 2_000_000)]
    [InlineData(0.3, 0.1, 0.5)]
    public void NiceMaximum(double value, double minimum, double expected) =>
        Assert.Equal(expected, GraphScale.NiceMaximum(value, minimum), precision: 9);
}
