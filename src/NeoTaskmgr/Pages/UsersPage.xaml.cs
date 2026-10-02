using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;
using Windows.System;

namespace NeoTaskmgr.Pages;

public sealed partial class UsersPage : Page, ISearchablePage
{
    public UsersPage()
    {
        InitializeComponent();
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public UsersViewModel ViewModel { get; private set; } = null!;

    public void ApplySearch(string text) => ViewModel.ApplySearch(text);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (UsersViewModel)e.Parameter;
        base.OnNavigatedTo(e);
    }

    private void EndTaskAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is not TextBox && ViewModel.EndTaskCommand.CanExecute(null))
        {
            ViewModel.EndTaskCommand.Execute(null);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Right expands and Left collapses the selected user. Handled before the list view, which would otherwise
    /// consume the arrow keys.
    /// </summary>
    private void Table_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Right or VirtualKey.Left && e.OriginalSource is ListViewItem)
        {
            e.Handled = ViewModel.SetSelectedExpanded(e.Key == VirtualKey.Right);
        }
    }
}
