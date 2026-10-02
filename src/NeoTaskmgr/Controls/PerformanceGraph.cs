using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;
using Windows.UI;

namespace NeoTaskmgr.Controls;

/// <summary>
/// A Task Manager style history graph: a filled line of the most recent values (oldest on the left) over a grid,
/// with an optional dashed second series.
/// </summary>
public sealed partial class PerformanceGraph : Grid
{
    public static readonly DependencyProperty ValuesProperty = Register(nameof(Values), typeof(IReadOnlyList<double>), null);
    public static readonly DependencyProperty SecondaryValuesProperty = Register(nameof(SecondaryValues), typeof(IReadOnlyList<double>), null);
    public static readonly DependencyProperty MaximumProperty = Register(nameof(Maximum), typeof(double), 100.0);
    public static readonly DependencyProperty ColorProperty = Register(nameof(Color), typeof(Color), Color.FromArgb(255, 17, 125, 187));
    public static readonly DependencyProperty ShowGridProperty = Register(nameof(ShowGrid), typeof(bool), true);

    private const int GridDivisions = 10;

    private readonly Path _grid = new() { StrokeThickness = 1 };
    private readonly Polygon _fill = new();
    private readonly Polyline _line = new() { StrokeThickness = 1.5 };
    private readonly Polyline _secondaryLine = new() { StrokeThickness = 1, StrokeDashArray = [3, 2] };
    private readonly Border _border = new() { BorderThickness = new Thickness(1) };

    public PerformanceGraph()
    {
        Children.Add(_grid);
        Children.Add(_fill);
        Children.Add(_secondaryLine);
        Children.Add(_line);
        Children.Add(_border);
        SizeChanged += (_, _) => Redraw();
        ApplyColor();
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>An optional second series, drawn as a dashed line (e.g. network send next to receive).</summary>
    public IReadOnlyList<double>? SecondaryValues
    {
        get => (IReadOnlyList<double>?)GetValue(SecondaryValuesProperty);
        set => SetValue(SecondaryValuesProperty, value);
    }

    /// <summary>The value at the top of the graph.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    private static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(PerformanceGraph), new PropertyMetadata(defaultValue, (d, e) =>
        {
            var graph = (PerformanceGraph)d;
            if (e.Property == ColorProperty)
            {
                graph.ApplyColor();
            }

            graph.Redraw();
        }));

    private void ApplyColor()
    {
        var color = Color;
        _line.Stroke = new SolidColorBrush(color);
        _secondaryLine.Stroke = _line.Stroke;
        _fill.Fill = new SolidColorBrush(WithAlpha(color, 0x30));
        _grid.Stroke = new SolidColorBrush(WithAlpha(color, 0x28));
        _border.BorderBrush = new SolidColorBrush(WithAlpha(color, 0xB0));
    }

    private void Redraw()
    {
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _line.Points = ToPoints(Values, width, height);
        _secondaryLine.Points = ToPoints(SecondaryValues, width, height);

        var fill = ToPoints(Values, width, height);
        if (fill.Count > 0)
        {
            fill.Add(new Point(width, height));
            fill.Add(new Point(0, height));
        }

        _fill.Points = fill;
        _grid.Data = ShowGrid ? CreateGrid(width, height) : null;
    }

    private PointCollection ToPoints(IReadOnlyList<double>? values, double width, double height)
    {
        var points = new PointCollection();
        if (values is not { Count: > 1 })
        {
            return points;
        }

        var maximum = Maximum > 0 ? Maximum : 1;
        var step = width / (values.Count - 1);
        for (var i = 0; i < values.Count; i++)
        {
            points.Add(new Point(i * step, height - Math.Clamp(values[i] / maximum, 0, 1) * height));
        }

        return points;
    }

    private static GeometryGroup CreateGrid(double width, double height)
    {
        var grid = new GeometryGroup();
        for (var i = 1; i < GridDivisions; i++)
        {
            var x = width * i / GridDivisions;
            var y = height * i / GridDivisions;
            grid.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, height) });
            grid.Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(width, y) });
        }

        return grid;
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
