using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

/// <summary>Picks the template for user rows and process rows of the Users page.</summary>
public sealed partial class UsersRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? ProcessTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is UserRowViewModel ? UserTemplate : ProcessTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
