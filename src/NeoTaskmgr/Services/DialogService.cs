using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using NeoTaskmgr.Core;

namespace NeoTaskmgr.Services;

/// <summary>Shows dialogs on behalf of view models. The window properties are set by the main window.</summary>
public sealed class DialogService
{
    public XamlRoot? XamlRoot { get; set; }

    /// <summary>The main window's handle, as owner for shell dialogs.</summary>
    public nint WindowHandle { get; set; }

    public WindowId WindowId { get; set; }

    public async Task<bool> ConfirmAsync(string title, string message, string primaryButtonText, string? closeButtonText = null)
    {
        var dialog = Create(title, message);
        dialog.PrimaryButtonText = primaryButtonText;
        dialog.CloseButtonText = closeButtonText ?? Strings.Get("DialogCancel");
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

    /// <summary>Asks for a command to run, like the Run dialog. Returns null if cancelled.</summary>
    public async Task<(string Command, bool AsAdministrator)?> RunNewTaskAsync()
    {
        var command = new TextBox { PlaceholderText = Strings.Get("RunNewTaskPlaceholder"), MinWidth = 300 };
        AutomationProperties.SetName(command, Strings.Get("RunNewTaskOpen"));
        var browse = new Button { Content = Strings.Get("RunNewTaskBrowse") };
        // When this app is elevated, everything it starts is elevated too.
        var asAdministrator = new CheckBox
        {
            Content = Strings.Get(Elevation.IsElevated ? "RunNewTaskElevated" : "RunNewTaskAsAdministrator"),
            IsChecked = Elevation.IsElevated,
            IsEnabled = !Elevation.IsElevated,
        };

        var commandRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        Grid.SetColumn(browse, 1);
        commandRow.Children.Add(command);
        commandRow.Children.Add(browse);

        var dialog = Create(Strings.Get("RunNewTaskTitle"), null);
        dialog.Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Strings.Get("RunNewTaskMessage"), TextWrapping = TextWrapping.Wrap },
                commandRow,
                asAdministrator,
            },
        };
        dialog.PrimaryButtonText = Strings.Get("DialogOk");
        dialog.CloseButtonText = Strings.Get("DialogCancel");
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.IsPrimaryButtonEnabled = false;
        command.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(command.Text);
        browse.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker(WindowId) { FileTypeFilter = { ".exe", ".com", ".bat", ".cmd", "*" } };
            if (await picker.PickSingleFileAsync() is { } file)
            {
                command.Text = $"\"{file.Path}\"";
            }
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? (command.Text, asAdministrator.IsChecked == true)
            : null;
    }

    private ContentDialog Create(string title, string? message) => new()
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = message,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
    };
}
