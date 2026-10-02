using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NeoTaskmgr.Core.Sampling;
using NeoTaskmgr.Core.Settings;
using NeoTaskmgr.Core.Startup;
using NeoTaskmgr.Services;
using NeoTaskmgr.ViewModels;
using NeoTaskmgr.ViewModels.Performance;

namespace NeoTaskmgr;

public partial class App : Application
{
    private MainWindow? _window;
    private SamplingService? _sampler;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = new SettingsViewModel(new SettingsStore(SettingsStore.DefaultFilePath));
        var sampler = _sampler = new SamplingService(settings.Current.UpdateSpeed);
        var dialogs = new DialogService();
        var runner = new ProcessCommandRunner(dialogs, sampler.RefreshNow);
        var viewModel = new MainViewModel(
            settings,
            new ProcessesViewModel(runner),
            new PerformanceViewModel(),
            new StartupAppsViewModel(StartupProvider.CreateDefault(), runner),
            new DetailsViewModel(runner),
            new ServicesViewModel(runner),
            sampler.RefreshNow);

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        sampler.Sampled += snapshot => dispatcher.TryEnqueue(() => viewModel.Apply(snapshot));
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.UpdateSpeedIndex))
            {
                sampler.Speed = settings.Current.UpdateSpeed;
            }
        };

        _window = new MainWindow(viewModel, dialogs);
        _window.Closed += (_, _) => sampler.Dispose();
        _window.Activate();
        sampler.Start();
    }
}
