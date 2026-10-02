using System.Runtime.CompilerServices;
using NeoTaskmgr.Core.Settings;
using NeoTaskmgr.Mvvm;

namespace NeoTaskmgr.ViewModels;

/// <summary>
/// The app-wide settings. Every change is saved immediately and raises <see cref="ObservableObject.PropertyChanged"/>
/// so the window and the sampler can react.
/// </summary>
public sealed class SettingsViewModel(SettingsStore store) : ObservableObject
{
    private AppSettings _settings = store.Load();

    public AppSettings Current => _settings;

    // The *Index properties back ComboBox.SelectedIndex; item order in XAML matches the enum order.

    public int StartPageIndex
    {
        get => (int)_settings.StartPage;
        set => Update(_settings with { StartPage = (AppPage)value });
    }

    public int UpdateSpeedIndex
    {
        get => (int)_settings.UpdateSpeed;
        set => Update(_settings with { UpdateSpeed = (UpdateSpeed)value });
    }

    public int ThemeIndex
    {
        get => (int)_settings.Theme;
        set => Update(_settings with { Theme = (AppTheme)value });
    }

    public bool AlwaysOnTop
    {
        get => _settings.AlwaysOnTop;
        set => Update(_settings with { AlwaysOnTop = value });
    }

    private void Update(AppSettings settings, [CallerMemberName] string? propertyName = null)
    {
        // ComboBox reports -1 while its items are being reset; ignore that and unchanged values.
        if (settings == _settings || !Enum.IsDefined(settings.StartPage) || !Enum.IsDefined(settings.UpdateSpeed) || !Enum.IsDefined(settings.Theme))
        {
            return;
        }

        _settings = settings;
        store.Save(settings);
        OnPropertyChanged(propertyName);
    }
}
