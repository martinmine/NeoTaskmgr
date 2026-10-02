using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace NeoTaskmgr.Controls;

/// <summary>
/// Lays out one row of a <see cref="DataTable"/>: child <c>i</c> is placed in column <c>i</c> using the shared
/// column widths, and is collapsed when the column is hidden. Rows find their columns from the enclosing table.
/// </summary>
public sealed partial class TableRowPanel : Panel
{
    // Implicit styles don't cross template boundaries, so cells inside list items only see styles declared within
    // the row template. This panel is the root of every row template, so it carries the cell text style.
    private static readonly Style CellTextStyle = new(typeof(TextBlock))
    {
        Setters =
        {
            new Setter(VerticalAlignmentProperty, VerticalAlignment.Center),
            new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis),
        },
    };

    private TableColumns? _columns;
    private bool _isLoaded;

    public TableRowPanel()
    {
        Resources[typeof(TextBlock)] = CellTextStyle;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Space between the content of adjacent cells.</summary>
    public double CellSpacing { get; set; } = 12;

    /// <summary>Set explicitly for the header row; item rows take the columns of the enclosing <see cref="DataTable"/>.</summary>
    public TableColumns? Columns
    {
        get => _columns;
        set
        {
            Detach();
            _columns = value;
            Attach();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // A newly realized row is measured before it is loaded. Find the columns now, so the row isn't laid out
        // empty for a frame and only filled in on Loaded, which makes the list blink while scrolling.
        if (_columns is null && FindTable()?.Columns is { } columns)
        {
            _columns = columns;
            ApplyVisibility();
            Attach();
        }

        if (_columns is null)
        {
            return default;
        }

        double width = 0, height = 0;
        for (var i = 0; i < Children.Count && i < _columns.Count; i++)
        {
            var column = _columns[i];
            if (column.IsVisible)
            {
                Children[i].Measure(new Size(Math.Max(0, column.Width - CellSpacing), availableSize.Height));
                height = Math.Max(height, Children[i].DesiredSize.Height);
                width += column.Width;
            }
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_columns is null)
        {
            return finalSize;
        }

        double x = 0;
        for (var i = 0; i < Children.Count && i < _columns.Count; i++)
        {
            var column = _columns[i];
            if (column.IsVisible)
            {
                Children[i].Arrange(new Rect(x, 0, Math.Max(0, column.Width - CellSpacing), finalSize.Height));
                x += column.Width;
            }
        }

        return finalSize;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        if (_columns is null)
        {
            _columns = FindTable()?.Columns;
        }

        Attach();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
        _isLoaded = false;
    }

    private void Attach()
    {
        if (_isLoaded && _columns is not null)
        {
            _columns.LayoutChanged += Columns_LayoutChanged;
            ApplyLayout();
        }
    }

    private void Detach()
    {
        if (_columns is not null)
        {
            _columns.LayoutChanged -= Columns_LayoutChanged;
        }
    }

    private void Columns_LayoutChanged(object? sender, EventArgs e) => ApplyLayout();

    private void ApplyLayout()
    {
        ApplyVisibility();
        InvalidateMeasure();
    }

    private void ApplyVisibility()
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var isVisible = _columns is not null && i < _columns.Count && _columns[i].IsVisible;
            Children[i].Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private DataTable? FindTable()
    {
        for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is DataTable table)
            {
                return table;
            }
        }

        return null;
    }
}
