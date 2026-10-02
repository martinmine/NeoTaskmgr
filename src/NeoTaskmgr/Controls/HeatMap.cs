using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace NeoTaskmgr.Controls;

/// <summary>
/// Cell shading like Task Manager's: the user's accent color, faint for low values and strong for high values. The
/// colors are partly transparent so they work on both light and dark backgrounds. In high contrast mode there is no
/// shading, so text keeps the system's contrast colors. For use from x:Bind on the UI thread.
/// </summary>
public static class HeatMap
{
    private const int Steps = 10;
    private const byte MinAlpha = 0x18;
    private const byte MaxAlpha = 0xA0;
    private static readonly SolidColorBrush[] Brushes =
        Enumerable.Range(0, Steps + 1).Select(_ => new SolidColorBrush(Colors.Transparent)).ToArray();
    private static readonly SolidColorBrush None = new(Colors.Transparent);
    private static bool _isHighContrast;

    /// <param name="level">0 (cold) to 1 (hot); values outside the range are clamped.</param>
    public static Brush Brush(double level) =>
        _isHighContrast ? None : Brushes[(int)Math.Round(Math.Clamp(level, 0, 1) * Steps)];

    /// <summary>
    /// Reads the accent color and whether high contrast is on. Called by the window at startup, when its theme changes
    /// (which includes switching high contrast) and when the system colors change; the change event of
    /// <see cref="AccessibilitySettings"/> needs a CoreWindow, which desktop apps do not have. The brushes are updated
    /// in place, so cells already showing them pick up the new color.
    /// </summary>
    public static void Update(UISettings uiSettings, ElementTheme theme)
    {
        _isHighContrast = new AccessibilitySettings().HighContrast;

        // A darker shade keeps the light text of the dark theme readable, and a lighter one the dark text of the light theme.
        var accent = uiSettings.GetColorValue(theme == ElementTheme.Dark ? UIColorType.AccentDark1 : UIColorType.AccentLight2);
        for (var step = 0; step <= Steps; step++)
        {
            var t = step / (double)Steps;
            Brushes[step].Color = Color.FromArgb((byte)(MinAlpha + t * (MaxAlpha - MinAlpha)), accent.R, accent.G, accent.B);
        }
    }
}
