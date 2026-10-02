using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace NeoTaskmgr.Controls;

/// <summary>The drag handle at the right edge of a column header that resizes the column.</summary>
public sealed partial class ColumnGripper : Grid
{
    private readonly TableColumn _column;
    private double _dragStartX;
    private double _dragStartWidth;
    private bool _isDragging;

    public ColumnGripper(TableColumn column)
    {
        _column = column;
        Width = 8;
        HorizontalAlignment = HorizontalAlignment.Right;
        Background = new SolidColorBrush(Colors.Transparent); // Needed for hit testing.
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _isDragging = false;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = CapturePointer(e.Pointer);
        _dragStartX = e.GetCurrentPoint(null).Position.X;
        _dragStartWidth = _column.Width;
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _column.Width = _dragStartWidth + e.GetCurrentPoint(null).Position.X - _dragStartX;
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
}
