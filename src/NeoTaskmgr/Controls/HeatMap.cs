using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NeoTaskmgr.Controls;

/// <summary>
/// Cell shading like Task Manager's: pale yellow for low values to orange for high values. The colors are partly
/// transparent so they work on both light and dark backgrounds. For use from x:Bind on the UI thread.
/// </summary>
public static class HeatMap
{
    private const int Steps = 10;
    private static readonly SolidColorBrush[] Brushes = Enumerable.Range(0, Steps + 1).Select(CreateBrush).ToArray();

    /// <param name="level">0 (cold) to 1 (hot); values outside the range are clamped.</param>
    public static Brush Brush(double level) => Brushes[(int)Math.Round(Math.Clamp(level, 0, 1) * Steps)];

    private static SolidColorBrush CreateBrush(int step)
    {
        var t = step / (double)Steps;
        return new SolidColorBrush(Color.FromArgb(
            (byte)(0x30 + t * 0x90),
            0xFF,
            (byte)(0xE0 - t * 0x58),
            (byte)(0x70 - t * 0x58)));
    }
}
