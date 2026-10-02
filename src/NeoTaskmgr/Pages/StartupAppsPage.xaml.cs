using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

public sealed partial class StartupAppsPage : Page, ISearchablePage
{
    public StartupAppsPage()
    {
        InitializeComponent();
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public StartupAppsViewModel ViewModel { get; private set; } = null!;

    public void ApplySearch(string text) => ViewModel.ApplySearch(text);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (StartupAppsViewModel)e.Parameter;
        _ = ViewModel.ReloadAsync(); // The list is only read when the page is shown.
        base.OnNavigatedTo(e);
    }
}
