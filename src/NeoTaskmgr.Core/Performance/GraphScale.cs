namespace NeoTaskmgr.Core.Performance;

public static class GraphScale
{
    /// <summary>
    /// The smallest "nice" graph maximum (1, 2 or 5 times a power of ten) that fits <paramref name="value"/>,
    /// but at least <paramref name="minimum"/>. Used for graphs without a natural maximum, such as throughput.
    /// </summary>
    public static double NiceMaximum(double value, double minimum)
    {
        if (value <= minimum)
        {
            return minimum;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 5, 10 })
        {
            if (step * magnitude >= value)
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }
}
