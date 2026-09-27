using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EasyDisk.Views;
using EasyDisk.Services;

namespace EasyDisk;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        // 窗口标题
        this.Title = "EasyDisk";

        // 默认选中首页并导航，保证窗口一定有内容
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));

        // 应用保存的外观设置
        ApplyTheme(SettingsService.ThemeDark);
        SetBackdrop(SettingsService.UseMica);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    public void NavigateTo(string tag)
    {
        switch (tag)
        {
            case "HomePage":
                ContentFrame.Navigate(typeof(HomePage));
                break;
            case "DiskPage":
                ContentFrame.Navigate(typeof(DiskPage));
                break;
            case "SearchPage":
                ContentFrame.Navigate(typeof(SearchPage));
                break;
            case "SettingsPage":
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
        }
    }

    public void ApplyTheme(bool dark)
    {
        if (Content is FrameworkElement fe)
        {
            fe.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }

    public void SetBackdrop(bool useMica)
    {
        try
        {
            // 创建 MicaBackdrop 时不显式引用 MicaKind（在某些目标框架中该枚举可能不可用），
            // 直接使用默认构造即可与 XAML 中的设置一致。
            SystemBackdrop = useMica ? new MicaBackdrop() : null;
        }
        catch
        {
            SystemBackdrop = null;
        }
    }
}
