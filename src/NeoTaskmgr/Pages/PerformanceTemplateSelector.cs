using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NeoTaskmgr.ViewModels.Performance;

namespace NeoTaskmgr.Pages;

/// <summary>Picks the graphs layout for the selected device on the Performance page.</summary>
public sealed partial class PerformanceTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CpuTemplate { get; set; }
    public DataTemplate? MemoryTemplate { get; set; }
    public DataTemplate? DiskTemplate { get; set; }
    public DataTemplate? NetworkTemplate { get; set; }
    public DataTemplate? GpuTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        CpuViewModel => CpuTemplate,
        MemoryViewModel => MemoryTemplate,
        DiskViewModel => DiskTemplate,
        NetworkViewModel => NetworkTemplate,
        GpuViewModel => GpuTemplate,
        _ => null,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
