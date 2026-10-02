using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

public sealed partial class ServicesPage : Page, ISearchablePage
{
    public ServicesPage()
    {
        InitializeComponent();
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public ServicesViewModel ViewModel { get; private set; } = null!;

    public void ApplySearch(string text) => ViewModel.ApplySearch(text);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (ServicesViewModel)e.Parameter;
        base.OnNavigatedTo(e);
    }
}
