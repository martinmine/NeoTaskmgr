namespace NeoTaskmgr.Core.Settings;

public static class UpdateSpeedExtensions
{
    /// <summary>The sampling interval for the speed, or <c>null</c> when updates are paused.</summary>
    public static TimeSpan? ToInterval(this UpdateSpeed speed) => speed switch
    {
        UpdateSpeed.High => TimeSpan.FromMilliseconds(500),
        UpdateSpeed.Normal => TimeSpan.FromSeconds(1),
        UpdateSpeed.Low => TimeSpan.FromSeconds(4),
        UpdateSpeed.Paused => null,
        _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, null),
    };
}
