using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;
using Windows.System;

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
        if (!IsTextInputFocused() && ViewModel.EndTaskCommand.CanExecute(null))
        {
            ViewModel.EndTaskCommand.Execute(null);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Right expands and Left collapses the selected app. Handled before the list view, which would otherwise
    /// consume the arrow keys.
    /// </summary>
    private void Table_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Right or VirtualKey.Left && e.OriginalSource is ListViewItem)
        {
            e.Handled = ViewModel.SetSelectedExpanded(e.Key == VirtualKey.Right);
        }
    }

    // Keys in a text box (such as the search box) edit text instead.
    private bool IsTextInputFocused() => FocusManager.GetFocusedElement(XamlRoot) is TextBox;
}
