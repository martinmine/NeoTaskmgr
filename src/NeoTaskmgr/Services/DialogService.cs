using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NeoTaskmgr.Services;

/// <summary>Shows dialogs on behalf of view models. <see cref="XamlRoot"/> is set by the main window once loaded.</summary>
public sealed class DialogService
{
    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmAsync(string title, string message, string primaryButtonText)
    {
        var dialog = Create(title, message);
        dialog.PrimaryButtonText = primaryButtonText;
        dialog.CloseButtonText = Strings.Get("DialogCancel");
        dialog.DefaultButton = ContentDialogButton.Primary;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = Create(title, message);
        dialog.CloseButtonText = Strings.Get("DialogOk");
        await dialog.ShowAsync();
    }

    /// <summary>Lets the user pick processors. Returns the new mask, or null if cancelled.</summary>
    public async Task<ulong?> PickAffinityAsync(string processName, ulong currentMask, ulong systemMask)
    {
        var processors = new List<(CheckBox CheckBox, ulong Bit)>();
        var processorPanel = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 100 };
        for (var i = 0; i < 64; i++)
        {
            var bit = 1ul << i;
            if ((systemMask & bit) != 0)
            {
                var checkBox = new CheckBox { Content = Strings.Format("AffinityProcessor", i), IsChecked = (currentMask & bit) != 0, MinWidth = 0 };
                processors.Add((checkBox, bit));
                processorPanel.Children.Add(checkBox);
            }
        }

        var all = new CheckBox { Content = Strings.Get("AffinityAllProcessors") };
        var dialog = Create(Strings.Get("AffinityTitle"), null);
        dialog.Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = Strings.Format("AffinityMessage", processName), TextWrapping = TextWrapping.Wrap },
                all,
                new ScrollViewer { Content = processorPanel, MaxHeight = 300 },
            },
        };
        dialog.PrimaryButtonText = Strings.Get("DialogOk");
        dialog.CloseButtonText = Strings.Get("DialogCancel");
        dialog.DefaultButton = ContentDialogButton.Primary;

        // Keep "All processors" in sync, and require at least one processor.
        var updating = false;
        void Update()
        {
            updating = true;
            var checkedCount = processors.Count(p => p.CheckBox.IsChecked == true);
            all.IsChecked = checkedCount == processors.Count ? true : checkedCount == 0 ? false : null;
            dialog.IsPrimaryButtonEnabled = checkedCount > 0;
            updating = false;
        }

        foreach (var (checkBox, _) in processors)
        {
            checkBox.Click += (_, _) => Update();
        }

        all.Click += (_, _) =>
        {
            if (!updating)
            {
                var check = all.IsChecked != false;
                processors.ForEach(p => p.CheckBox.IsChecked = check);
                Update();
            }
        };
        Update();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return processors.Where(p => p.CheckBox.IsChecked == true).Aggregate(0ul, (mask, p) => mask | p.Bit);
    }

    private ContentDialog Create(string title, string? message) => new()
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = message,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
    };
}
