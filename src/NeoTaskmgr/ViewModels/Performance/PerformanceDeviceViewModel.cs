using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels.Performance;

public enum PerformanceDeviceKind
{
    Cpu,
    Memory,
    Disk,
    Network,
    Gpu,
}

/// <summary>The last <see cref="Length"/> values of a measurement, oldest first, for a graph.</summary>
public sealed class GraphHistory(double maximum = 100) : ObservableObject
{
    public const int Length = 60;

    private double[] _values = new double[Length];
    private double _maximum = maximum;

    /// <summary>A new array after every <see cref="Add"/>, so bindings update.</summary>
    public IReadOnlyList<double> Values => _values;

    public double Maximum
    {
        get => _maximum;
        set => SetProperty(ref _maximum, value);
    }

    public double Peak => _values.Max();

    public void Add(double value)
    {
        var values = new double[Length];
        Array.Copy(_values, 1, values, 0, Length - 1);
        values[^1] = value;
        _values = values;
        OnPropertyChanged(nameof(Values));
    }
}

/// <summary>The line beside a statistic that identifies its series in a graph, like Task Manager's read and write speeds.</summary>
public enum StatLine
{
    None,
    /// <summary>The graph's main, solid series.</summary>
    Solid,
    /// <summary>The graph's dashed second series.</summary>
    Dashed,
}

/// <summary>A labeled value below the graphs: a large statistic, or a fact in the list beside them.</summary>
public sealed class StatViewModel(string label, PerformanceDeviceKind kind = default, StatLine line = StatLine.None) : ObservableObject
{
    private string _value = string.Empty;

    public string Label { get; } = label;

    /// <summary>The device the statistic belongs to, which gives <see cref="Line"/> its color.</summary>
    public PerformanceDeviceKind Kind { get; } = kind;

    public StatLine Line { get; } = line;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>A row of statistics below the graphs.</summary>
public sealed class StatRow
{
    public List<StatViewModel> Stats { get; } = [];
}

/// <summary>A device on the Performance page: an entry in the device list and the details shown when it is selected.</summary>
public abstract class PerformanceDeviceViewModel(string key, PerformanceDeviceKind kind) : ObservableObject
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _detail = string.Empty;
    private string _description = string.Empty;

    /// <summary>Identifies the device across snapshots.</summary>
    public string Key { get; } = key;

    public PerformanceDeviceKind Kind { get; } = kind;

    public string Title
    {
        get => _title;
        protected set => SetProperty(ref _title, value);
    }

    /// <summary>The second line in the device list.</summary>
    public string Subtitle
    {
        get => _subtitle;
        protected set => SetProperty(ref _subtitle, value);
    }

    /// <summary>An optional third line in the device list.</summary>
    public string Detail
    {
        get => _detail;
        protected set => SetProperty(ref _detail, value);
    }

    /// <summary>The device's name or model, shown next to the title in the details.</summary>
    public string Description
    {
        get => _description;
        protected set => SetProperty(ref _description, value);
    }

    /// <summary>The graph shown in the device list.</summary>
    public GraphHistory History { get; } = new();

    /// <summary>An optional second series for the device list graph.</summary>
    public virtual GraphHistory? SecondaryHistory => null;

    /// <summary>The statistics shown in large text below the graphs, in rows like Task Manager.</summary>
    public List<StatRow> StatRows { get; } = [];

    /// <summary>The facts listed beside the statistics, such as the base speed or capacity.</summary>
    public List<StatViewModel> Properties { get; } = [];

    /// <summary>Used by the list view as the device's accessible name, e.g. "CPU 6% 4.13 GHz".</summary>
    public override string ToString() => string.Join(", ", new[] { Title, Subtitle, Detail }.Where(s => s.Length > 0));

    protected StatViewModel AddStat(string labelKey, bool newRow = false, StatLine line = StatLine.None)
    {
        if (newRow || StatRows.Count == 0)
        {
            StatRows.Add(new StatRow());
        }

        var stat = new StatViewModel(Strings.Get(labelKey), Kind, line);
        StatRows[^1].Stats.Add(stat);
        return stat;
    }

    protected StatViewModel AddProperty(string labelKey)
    {
        var property = new StatViewModel(Strings.Format("PropertyLabel", Strings.Get(labelKey)));
        Properties.Add(property);
        return property;
    }
}
