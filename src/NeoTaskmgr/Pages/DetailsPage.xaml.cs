using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr.Pages;

public sealed partial class DetailsPage : Page, ISearchablePage
{
    public DetailsPage()
    {
        InitializeComponent();
        // Show the selected process, e.g. after "Go to details" on the Processes page.
        Loaded += (_, _) =>
        {
            if (ViewModel.SelectedRow is { } row)
            {
                Table.ScrollIntoView(row);
            }
        };
    }

    /// <summary>Passed as the navigation parameter; set before the page loads its bindings.</summary>
    public DetailsViewModel ViewModel { get; private set; } = null!;

    public void ApplySearch(string text) => ViewModel.ApplySearch(text);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (DetailsViewModel)e.Parameter;
        base.OnNavigatedTo(e);
    }

    /// <summary>Checks the selected process's current priority in the "Set priority" submenu.</summary>
    private void RowMenu_Opening(object sender, object e)
    {
        var current = ViewModel.SelectedRow?.Priority?.ToString();
        foreach (var item in PriorityMenu.Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = (string)item.Tag == current;
        }
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
