using Microsoft.UI.Xaml;
using NeoTaskmgr.Core.Settings;
using NeoTaskmgr.ViewModels;

namespace NeoTaskmgr;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = new SettingsViewModel(new SettingsStore(SettingsStore.DefaultFilePath));
        _window = new MainWindow(settings);
        _window.Activate();
    }
}
