using System.Collections.ObjectModel;
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

/// <summary>A labeled value in the statistics below the graphs.</summary>
public sealed class StatViewModel(string label) : ObservableObject
{
    private string _value = string.Empty;

    public string Label { get; } = label;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
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

    public ObservableCollection<StatViewModel> Stats { get; } = [];

    protected StatViewModel AddStat(string labelKey)
    {
        var stat = new StatViewModel(Strings.Get(labelKey));
        Stats.Add(stat);
        return stat;
    }
}
