using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NeoTaskmgr.Controls;
using NeoTaskmgr.Core;
using NeoTaskmgr.Core.Settings;
using NeoTaskmgr.Pages;
using NeoTaskmgr.Services;
using NeoTaskmgr.ViewModels;
using Windows.Graphics;
using Windows.System;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace NeoTaskmgr;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<AppPage, Type> PageTypes = new()
    {
        [AppPage.Processes] = typeof(ProcessesPage),
        [AppPage.Performance] = typeof(PerformancePage),
        [AppPage.StartupApps] = typeof(StartupAppsPage),
        [AppPage.Details] = typeof(DetailsPage),
        [AppPage.Services] = typeof(ServicesPage),
    };

    // Default window size in device-independent pixels.
    private const int DefaultWidth = 1100;
    private const int DefaultHeight = 760;

    private readonly MainViewModel _viewModel;
    private readonly SettingsViewModel _settings;
    private readonly DialogService _dialogs;
    private readonly bool _isPlacementRestored;

    // Kept alive for its ColorValuesChanged event.
    private readonly UISettings _uiSettings = new();

    // The last bounds while not maximized or minimized, saved so the window reopens at that size.
    private RectInt32 _restoredBounds;

    public MainWindow(MainViewModel viewModel, DialogService dialogs)
    {
        _viewModel = viewModel;
        _settings = viewModel.Settings;
        _dialogs = dialogs;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _dialogs.WindowHandle = WindowNative.GetWindowHandle(this);
        _dialogs.WindowId = AppWindow.Id;
        NotElevatedInfoBar.IsOpen = !Elevation.IsElevated;
        viewModel.Processes.GoToDetailsRequested += GoToDetails;
        viewModel.Processes.GoToServicesRequested += GoToServices;
        viewModel.Details.GoToServicesRequested += GoToServices;
        viewModel.Services.GoToDetailsRequested += GoToDetails;
        viewModel.Processes.SwitchedToApp += MinimizeOnUse;

        ApplyTheme();
        ApplyAlwaysOnTop();
        _settings.PropertyChanged += Settings_PropertyChanged;
        UpdateHeatMap();
        RootGrid.ActualThemeChanged += (_, _) => UpdateHeatMap();
        // Raised off the UI thread when the accent color changes.
        _uiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateHeatMap);

        _isPlacementRestored = RestorePlacement();
        AppWindow.Changed += AppWindow_Changed;
        Closed += (_, _) => _viewModel.SaveSession(new WindowPlacement(
            _restoredBounds.X, _restoredBounds.Y, _restoredBounds.Width, _restoredBounds.Height,
            Presenter.State == OverlappedPresenterState.Maximized));
    }

    private OverlappedPresenter Presenter => (OverlappedPresenter)AppWindow.Presenter;

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _dialogs.XamlRoot = RootGrid.XamlRoot;
        if (!_isPlacementRestored)
        {
            var scale = RootGrid.XamlRoot.RasterizationScale;
            AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
        }
    }

    /// <summary>Moves the window to where it was last time, if that is still on a display.</summary>
    private bool RestorePlacement()
    {
        _restoredBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        if (_settings.Current.Window is not { } window)
        {
            return false;
        }

        var bounds = new RectInt32(window.X, window.Y, window.Width, window.Height);
        if (DisplayArea.GetFromRect(bounds, DisplayAreaFallback.None) is null)
        {
            return false; // The display it was on is gone.
        }

        AppWindow.MoveAndResize(bounds);
        _restoredBounds = bounds;
        if (window.IsMaximized)
        {
            Presenter.Maximize();
        }

        return true;
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((args.DidPositionChange || args.DidSizeChange) && Presenter.State == OverlappedPresenterState.Restored)
        {
            _restoredBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }
    }

    private void MinimizeOnUse()
    {
        if (_settings.Current.MinimizeOnUse)
        {
            Presenter.Minimize();
        }
    }

    private void FocusSearch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel.RefreshCommand.Execute(null);
        args.Handled = true;
    }

    /// <summary>Ctrl+1 to Ctrl+5 open the pages in navigation order.</summary>
    private void GoToPage_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Navigate((AppPage)(sender.Key - VirtualKey.Number1));
        args.Handled = true;
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        Navigate(_settings.Current.StartPage);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var transition = new SuppressNavigationTransitionInfo();
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage), _settings, transition);
        }
        else if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            var page = Enum.Parse<AppPage>(tag);
            NavFrame.Navigate(PageTypes[page], GetPageViewModel(page), transition);
        }

        (NavFrame.Content as ISearchablePage)?.ApplySearch(SearchBox.Text);
    }

    private object? GetPageViewModel(AppPage page) => page switch
    {
        AppPage.Processes => _viewModel.Processes,
        AppPage.Performance => _viewModel.Performance,
        AppPage.StartupApps => _viewModel.StartupApps,
        AppPage.Details => _viewModel.Details,
        AppPage.Services => _viewModel.Services,
        _ => null,
    };

    private void GoToDetails(int pid)
    {
        SearchBox.Text = string.Empty;
        _viewModel.Details.Select(pid);
        Navigate(AppPage.Details);
    }

    /// <summary>Shows the Services page filtered to the services of the process.</summary>
    private void GoToServices(int pid)
    {
        Navigate(AppPage.Services);
        SearchBox.Text = pid.ToString(CultureInfo.CurrentCulture);
    }

    private void Navigate(AppPage page) =>
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(item => (string)item.Tag == page.ToString());

    private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args) => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs args)
    {
        ClearSearchButton.Visibility = XamlHelpers.NotEmpty(SearchBox.Text);
        (NavFrame.Content as ISearchablePage)?.ApplySearch(SearchBox.Text);
    }

    /// <summary>
    /// Shrinks the TextBox's built-in delete button away, as it only shows while focused;
    /// <see cref="ClearSearchButton"/> replaces it and stays visible whenever there is text.
    /// </summary>
    private void SearchBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (FindDescendant(SearchBox, "DeleteButton") is FrameworkElement deleteButton)
        {
            deleteButton.Width = 0;
            deleteButton.Margin = new Thickness(0);
        }
    }

    private static DependencyObject? FindDescendant(DependencyObject parent, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element && element.Name == name)
            {
                return child;
            }

            if (FindDescendant(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.ThemeIndex):
                ApplyTheme();
                break;
            case nameof(SettingsViewModel.AlwaysOnTop):
                ApplyAlwaysOnTop();
                break;
        }
    }

    private void ApplyTheme()
    {
        var theme = _settings.Current.Theme;
        RootGrid.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    private void UpdateHeatMap() => HeatMap.Update(_uiSettings, RootGrid.ActualTheme);

    private void ApplyAlwaysOnTop() => Presenter.IsAlwaysOnTop = _settings.Current.AlwaysOnTop;
}
