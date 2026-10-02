using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels.Performance;

namespace NeoTaskmgr.Pages;

public sealed partial class PerformancePage : Page
{
    public PerformancePage()
    {
        InitializeComponent();
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public PerformanceViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (PerformanceViewModel)e.Parameter;
        base.OnNavigatedTo(e);
    }
}
