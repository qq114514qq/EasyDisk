using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EasyDisk.Services;

namespace EasyDisk.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        this.InitializeComponent();
        ThemeSwitch.IsOn = SettingsService.ThemeDark;
        MicaSwitch.IsOn = SettingsService.UseMica;
        RecycleSwitch.IsOn = SettingsService.UseRecycleBin;
    }

    private void ThemeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        bool on = ((ToggleSwitch)sender).IsOn;
        SettingsService.ThemeDark = on;
        App.MainWindowInstance?.ApplyTheme(on);
    }

    private void MicaSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        bool on = ((ToggleSwitch)sender).IsOn;
        SettingsService.UseMica = on;
        App.MainWindowInstance?.SetBackdrop(on);
    }

    private void RecycleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsService.UseRecycleBin = ((ToggleSwitch)sender).IsOn;
    }
}
