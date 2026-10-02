using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using NeoTaskmgr.Core;
using NeoTaskmgr.Core.Settings;
using NeoTaskmgr.Pages;
using NeoTaskmgr.ViewModels;
using Windows.Graphics;

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

    private readonly SettingsViewModel _settings;

    public MainWindow(SettingsViewModel settings)
    {
        _settings = settings;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        NotElevatedInfoBar.IsOpen = !Elevation.IsElevated;

        ApplyTheme();
        ApplyAlwaysOnTop();
        _settings.PropertyChanged += Settings_PropertyChanged;
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var scale = RootGrid.XamlRoot.RasterizationScale;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        var startTag = _settings.Current.StartPage.ToString();
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(item => (string)item.Tag == startTag);
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
            NavFrame.Navigate(PageTypes[Enum.Parse<AppPage>(tag)], null, transition);
        }

        (NavFrame.Content as ISearchablePage)?.ApplySearch(SearchBox.Text);
    }

    private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args) => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
        (NavFrame.Content as ISearchablePage)?.ApplySearch(sender.Text);

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

    private void ApplyAlwaysOnTop()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = _settings.Current.AlwaysOnTop;
        }
    }
}
