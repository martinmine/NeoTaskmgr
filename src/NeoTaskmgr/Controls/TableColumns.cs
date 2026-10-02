using System.Collections;
using NeoTaskmgr.Core.Settings;

namespace NeoTaskmgr.Controls;

/// <summary>The columns of a <see cref="DataTable"/> plus its sort state. Owned by the view model.</summary>
public sealed class TableColumns : IReadOnlyList<TableColumn>
{
    private readonly IReadOnlyList<TableColumn> _columns;

    /// <param name="name">Identifies the table in the saved settings.</param>
    public TableColumns(string name, IReadOnlyList<TableColumn> columns)
    {
        Name = name;
        _columns = columns;
        foreach (var column in columns)
        {
            column.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TableColumn.Summary))
                {
                    SummaryChanged?.Invoke(this, column);
                }
                else
                {
                    LayoutChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }

    /// <summary>A column's width or visibility changed.</summary>
    public event EventHandler? LayoutChanged;

    public event EventHandler<TableColumn>? SummaryChanged;

    public event EventHandler? SortChanged;

    public string Name { get; }
    public TableColumn? SortColumn { get; private set; }
    public bool SortDescending { get; private set; }

    public int Count => _columns.Count;
    public TableColumn this[int index] => _columns[index];

    /// <summary>Sorts by the column; sorting by the current sort column again reverses the direction.</summary>
    public void Sort(TableColumn column) => Sort(column, column == SortColumn ? !SortDescending : column.IsNumeric);

    public void Sort(TableColumn column, bool descending)
    {
        SortColumn = column;
        SortDescending = descending;
        SortChanged?.Invoke(this, EventArgs.Empty);
    }

    public TableLayout GetLayout() =>
        new(_columns.Select(c => new ColumnLayout(c.Key, c.Width, c.IsVisible)).ToList(), SortColumn?.Key, SortDescending);

    /// <summary>Restores a saved layout. Columns that no longer exist are ignored; new columns keep their defaults.</summary>
    public void ApplyLayout(TableLayout layout)
    {
        foreach (var saved in layout.Columns)
        {
            if (_columns.FirstOrDefault(c => c.Key == saved.Key) is { } column)
            {
                column.Width = saved.Width;
                column.IsVisible = saved.IsVisible;
            }
        }

        if (_columns.FirstOrDefault(c => c.Key == layout.SortColumn) is { } sortColumn)
        {
            Sort(sortColumn, layout.SortDescending);
        }
    }

    public IEnumerator<TableColumn> GetEnumerator() => _columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
