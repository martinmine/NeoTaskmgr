using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

public sealed partial class AppHistoryPage : Page, ISearchablePage
{
    public AppHistoryPage()
    {
        InitializeComponent();
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public AppHistoryViewModel ViewModel { get; private set; } = null!;

    public void ApplySearch(string text) => ViewModel.ApplySearch(text);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (AppHistoryViewModel)e.Parameter;
        _ = ViewModel.ReloadAsync(); // The usage is only read when the page is shown.
        base.OnNavigatedTo(e);
    }
}
