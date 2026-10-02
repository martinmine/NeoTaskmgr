using System.Collections;

namespace NeoTaskmgr.Controls;

/// <summary>The columns of a <see cref="DataTable"/> plus its sort state. Owned by the view model.</summary>
public sealed class TableColumns : IReadOnlyList<TableColumn>
{
    private readonly IReadOnlyList<TableColumn> _columns;

    public TableColumns(IReadOnlyList<TableColumn> columns)
    {
        _columns = columns;
        foreach (var column in columns)
        {
            column.PropertyChanged += (_, _) => LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A column's width or visibility changed.</summary>
    public event EventHandler? LayoutChanged;

    public event EventHandler? SortChanged;

    public TableColumn? SortColumn { get; private set; }
    public bool SortDescending { get; private set; }

    public int Count => _columns.Count;
    public TableColumn this[int index] => _columns[index];

    /// <summary>Sorts by the column; sorting by the current sort column again reverses the direction.</summary>
    public void Sort(TableColumn column)
    {
        SortDescending = column == SortColumn ? !SortDescending : column.IsNumeric;
        SortColumn = column;
        SortChanged?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerator<TableColumn> GetEnumerator() => _columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
