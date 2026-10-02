using NeoTaskmgr.Core.Settings;

namespace NeoTaskmgr.Core.Tests.Settings;

public sealed class UpdateSpeedExtensionsTests
{
    [Theory]
    [InlineData(UpdateSpeed.High, 500)]
    [InlineData(UpdateSpeed.Normal, 1000)]
    [InlineData(UpdateSpeed.Low, 4000)]
    public void ToInterval_ReturnsSamplingInterval(UpdateSpeed speed, int expectedMilliseconds) =>
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), speed.ToInterval());

    [Fact]
    public void ToInterval_Paused_ReturnsNull() => Assert.Null(UpdateSpeed.Paused.ToInterval());
}
