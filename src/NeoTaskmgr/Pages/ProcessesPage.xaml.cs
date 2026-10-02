using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

public sealed partial class ProcessesPage : Page, ISearchablePage
{
    public ProcessesPage()
    {
        InitializeComponent();
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public ProcessesViewModel ViewModel { get; private set; } = null!;

    public void ApplySearch(string text) => ViewModel.ApplySearch(text);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (ProcessesViewModel)e.Parameter;
        base.OnNavigatedTo(e);
    }

    private void EndTaskAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Delete in a text box (such as the search box) edits text instead.
        if (FocusManager.GetFocusedElement(XamlRoot) is not TextBox && ViewModel.EndTaskCommand.CanExecute(null))
        {
            ViewModel.EndTaskCommand.Execute(null);
            args.Handled = true;
        }
    }
}
