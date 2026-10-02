using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

/// <summary>Picks the template for section headers and process rows of the Processes page.</summary>
public sealed partial class ProcessesRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SectionTemplate { get; set; }
    public DataTemplate? ProcessTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is SectionRowViewModel ? SectionTemplate : ProcessTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
