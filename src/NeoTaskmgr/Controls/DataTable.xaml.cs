using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace NeoTaskmgr.Controls;

/// <summary>
/// A virtualized table with sortable, resizable and hideable columns.
/// Each row template must have a <see cref="TableRowPanel"/> root with one cell per column, in column order.
/// </summary>
public sealed partial class DataTable : UserControl
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(TableColumns), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).OnColumnsChanged(e)));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(object), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.ItemsSource = e.NewValue));

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.ItemTemplate = (DataTemplate)e.NewValue));

    public static readonly DependencyProperty ItemTemplateSelectorProperty = DependencyProperty.Register(
        nameof(ItemTemplateSelector), typeof(DataTemplateSelector), typeof(DataTable),
        new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.ItemTemplateSelector = (DataTemplateSelector)e.NewValue));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.SelectedItem = e.NewValue));

    public static readonly DependencyProperty RowContextFlyoutProperty = DependencyProperty.Register(
        nameof(RowContextFlyout), typeof(FlyoutBase), typeof(DataTable), new PropertyMetadata(null));

    private readonly Dictionary<TableColumn, HeaderCell> _headerCells = [];
    private bool _isLoaded;

    public DataTable()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _isLoaded = true;
            Attach(Columns);
        };
        Unloaded += (_, _) =>
        {
            _isLoaded = false;
            Detach(Columns);
        };
    }

    public TableColumns? Columns
    {
        get => (TableColumns?)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Picks a template per row, for tables with different kinds of rows.</summary>
    public DataTemplateSelector? ItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Shown when a row is right-clicked (or the context menu key is pressed), after selecting that row.</summary>
    public FlyoutBase? RowContextFlyout
    {
        get => (FlyoutBase?)GetValue(RowContextFlyoutProperty);
        set => SetValue(RowContextFlyoutProperty, value);
    }

    public void ScrollIntoView(object item) => RowsList.ScrollIntoView(item);

    private void OnColumnsChanged(DependencyPropertyChangedEventArgs e)
    {
        Detach(e.OldValue as TableColumns);

        HeaderPanel.Children.Clear();
        _headerCells.Clear();
        if (e.NewValue is TableColumns columns)
        {
            foreach (var column in columns)
            {
                var cell = new HeaderCell(column, isFirst: _headerCells.Count == 0, () => Columns?.Sort(column));
                _headerCells[column] = cell;
                HeaderPanel.Children.Add(cell.Root);
            }
        }

        HeaderPanel.Columns = Columns;
        Attach(Columns);
    }

    // The columns are owned by a view model that outlives this control, so only listen while loaded.
    private void Attach(TableColumns? columns)
    {
        if (_isLoaded && columns is not null)
        {
            columns.SortChanged += Columns_SortChanged;
            columns.SummaryChanged += Columns_SummaryChanged;
            UpdateHeaders();
        }
    }

    private void Detach(TableColumns? columns)
    {
        if (columns is not null)
        {
            columns.SortChanged -= Columns_SortChanged;
            columns.SummaryChanged -= Columns_SummaryChanged;
        }
    }

    private void Columns_SortChanged(object? sender, EventArgs e) => UpdateHeaders();

    private void Columns_SummaryChanged(object? sender, TableColumn column) => _headerCells[column].Update(Columns!);

    private void UpdateHeaders()
    {
        foreach (var cell in _headerCells.Values)
        {
            cell.Update(Columns!);
        }
    }

    /// <summary>Right-clicking the header shows a menu to pick the visible columns.</summary>
    private void HeaderPanel_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (Columns is null)
        {
            return;
        }

        var menu = new MenuFlyout();
        foreach (var column in Columns.Where(c => c.CanHide))
        {
            var item = new ToggleMenuFlyoutItem { Text = column.Header, IsChecked = column.IsVisible };
            item.Click += (_, _) => column.IsVisible = item.IsChecked;
            menu.Items.Add(item);
        }

        var options = new FlyoutShowOptions();
        if (e.TryGetPosition(HeaderPanel, out var position))
        {
            options.Position = position;
        }

        menu.ShowAt(HeaderPanel, options);
        e.Handled = true;
    }

    private void RowsList_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (RowContextFlyout is null || FindRow(e.OriginalSource as DependencyObject) is not { } row)
        {
            return;
        }

        RowsList.SelectedItem = row;
        var options = new FlyoutShowOptions();
        if (e.TryGetPosition(RowsList, out var position))
        {
            options.Position = position;
        }

        RowContextFlyout.ShowAt(RowsList, options);
        e.Handled = true;
    }

    private object? FindRow(DependencyObject? element)
    {
        for (; element is not null && element != RowsList; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ListViewItem container)
            {
                return RowsList.ItemFromContainer(container);
            }
        }

        return null;
    }

    private void RowsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => SelectedItem = RowsList.SelectedItem;

    /// <summary>Makes rows at least as wide as the visible area, so the selection highlight spans the table.</summary>
    private void HorizontalScroller_SizeChanged(object sender, SizeChangedEventArgs e) => ContentGrid.MinWidth = e.NewSize.Width;

    /// <summary>A column header: optional summary on top, then the title, with the sort indicator above. Clicking it sorts.</summary>
    private sealed class HeaderCell
    {
        private readonly TableColumn _column;
        private readonly TextBlock _summary;

        // Centered above the title like Task Manager, so it takes no width from a narrow column's title.
        private readonly FontIcon _sortIcon = new()
        {
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

        /// <summary>Half the row cell spacing: the padding on each side of a title.</summary>
        private const double HalfSpacing = 6;

        public HeaderCell(TableColumn column, bool isFirst, Action sort)
        {
            _column = column;

            // The first header also reaches left to the edge of the row highlight, keeping its title in place.
            var leading = isFirst ? 2 * HalfSpacing : HalfSpacing;
            _sortIcon.Margin = new Thickness(leading, 2, HalfSpacing, 0);
            _summary = new TextBlock
            {
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            // Left-aligned even for numeric columns, so a title too long for its column keeps its first word visible.
            var title = new TextBlock { Text = column.Header, TextTrimming = TextTrimming.CharacterEllipsis };

            var button = new Button
            {
                Content = new StackPanel { Children = { _summary, title } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(leading, 4, HalfSpacing, 6),
                Background = null,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
            };
            ToolTipService.SetToolTip(button, column.Header);
            AutomationProperties.SetName(button, column.Header);
            button.Click += (_, _) => sort();

            Root = new Grid
            {
                Margin = new Thickness(isFirst ? -HalfSpacing : 0, 0, 0, 0),
                Children = { button, _sortIcon, new ColumnGripper(column) },
            };
        }

        public Grid Root { get; }

        public void Update(TableColumns columns)
        {
            _summary.Text = _column.Summary;
            _summary.Visibility = _column.Summary is null ? Visibility.Collapsed : Visibility.Visible;
            _sortIcon.Visibility = _column == columns.SortColumn ? Visibility.Visible : Visibility.Collapsed;
            _sortIcon.Glyph = columns.SortDescending ? "" : ""; // ChevronDown / ChevronUp
        }
    }
}
