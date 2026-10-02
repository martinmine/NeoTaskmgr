using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.Controls;

/// <summary>A column of a <see cref="DataTable"/>. Its cell is the child at the same index in each row's <see cref="TableRowPanel"/>.</summary>
public class TableColumn(string key, string header, double width, bool isNumeric = false, bool isVisible = true, bool canHide = true)
    : ObservableObject
{
    public const double MinWidth = 40;

    private double _width = width;
    private bool _isVisible = isVisible;
    private string? _summary;

    public string Key { get; } = key;
    public string Header { get; } = header;
    public double DefaultWidth { get; } = width;

    /// <summary>Numeric columns are right-aligned and sort descending first.</summary>
    public bool IsNumeric { get; } = isNumeric;

    public bool CanHide { get; } = canHide;

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, Math.Max(MinWidth, value));
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value || !CanHide);
    }

    /// <summary>A total shown above the header, such as overall CPU usage.</summary>
    public string? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }
}

/// <summary>A column that knows how to sort rows of type <typeparamref name="TRow"/>.</summary>
public sealed class TableColumn<TRow>(
    string key, string header, double width, Comparison<TRow> compare, bool isNumeric = false, bool isVisible = true, bool canHide = true)
    : TableColumn(key, header, width, isNumeric, isVisible, canHide)
{
    public Comparison<TRow> Compare { get; } = compare;
}
