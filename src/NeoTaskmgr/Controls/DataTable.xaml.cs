using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace NeoTaskmgr.Controls;

/// <summary>
/// A virtualized table with sortable, resizable and hideable columns.
/// The <see cref="ItemTemplate"/> must have a <see cref="TableRowPanel"/> root with one cell per column, in column order.
/// </summary>
public sealed partial class DataTable : UserControl
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(TableColumns), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).OnColumnsChanged(e)));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(object), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.ItemsSource = e.NewValue));

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.ItemTemplate = (DataTemplate)e.NewValue));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(DataTable), new PropertyMetadata(null, (d, e) => ((DataTable)d).RowsList.SelectedItem = e.NewValue));

    public static readonly DependencyProperty RowContextFlyoutProperty = DependencyProperty.Register(
        nameof(RowContextFlyout), typeof(FlyoutBase), typeof(DataTable), new PropertyMetadata(null));

    private readonly Dictionary<TableColumn, FontIcon> _sortIcons = [];

    public DataTable()
    {
        InitializeComponent();
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

    private void OnColumnsChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TableColumns oldColumns)
        {
            oldColumns.SortChanged -= Columns_SortChanged;
        }

        HeaderPanel.Children.Clear();
        _sortIcons.Clear();
        if (e.NewValue is TableColumns columns)
        {
            foreach (var column in columns)
            {
                HeaderPanel.Children.Add(CreateHeaderCell(column));
            }

            columns.SortChanged += Columns_SortChanged;
            UpdateSortIcons();
        }

        HeaderPanel.Columns = Columns;
    }

    private Grid CreateHeaderCell(TableColumn column)
    {
        var sortIcon = new FontIcon { FontSize = 10, Visibility = Visibility.Collapsed };
        _sortIcons[column] = sortIcon;

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (column.IsNumeric)
        {
            content.Children.Add(sortIcon);
        }

        content.Children.Add(new TextBlock { Text = column.Header, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!column.IsNumeric)
        {
            content.Children.Add(sortIcon);
        }

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = column.IsNumeric ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Padding = new Thickness(0, 0, 12, 0),
            Background = null,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
        };
        ToolTipService.SetToolTip(button, column.Header);
        AutomationProperties.SetName(button, column.Header);
        button.Click += (_, _) => Columns?.Sort(column);

        return new Grid { Children = { button, new ColumnGripper(column) } };
    }

    private void Columns_SortChanged(object? sender, EventArgs e) => UpdateSortIcons();

    private void UpdateSortIcons()
    {
        foreach (var (column, icon) in _sortIcons)
        {
            icon.Visibility = column == Columns?.SortColumn ? Visibility.Visible : Visibility.Collapsed;
            icon.Glyph = Columns?.SortDescending == true ? "" : ""; // ChevronDown / ChevronUp
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
}
