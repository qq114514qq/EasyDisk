using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using EasyDisk.Models;
using EasyDisk.Services;

namespace EasyDisk.Views;

public sealed partial class SearchPage : Page
{
    private readonly DispatcherTimer _debounce = new();
    private CancellationTokenSource _indexCts;

    public SearchPage()
    {
        this.InitializeComponent();
        _debounce.Interval = TimeSpan.FromMilliseconds(150);
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            DoSearch();
        };
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (EverythingService.IsAvailable())
        {
            StatusText.Text = "⚡ 已连接 Everything —— 全盘毫秒级搜索";
        }
        else if (EverythingService.IsInstalled())
        {
            StatusText.Text = "检测到已安装 Everything，但未运行。点“启动 Everything”开启全盘秒搜。";
        }
        else if (IndexService.HasIndex())
        {
            StatusText.Text = $"未检测到 Everything，使用自建索引（{IndexService.IndexTime():yyyy-MM-dd HH:mm}）";
        }
        else
        {
            StatusText.Text = "未检测到 Everything。点“建立索引”后即可搜索常用目录。";
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void DoSearch()
    {
        string kw = SearchBox.Text?.Trim();
        if (string.IsNullOrEmpty(kw))
        {
            ResultList.ItemsSource = null;
            UpdateStatus();
            return;
        }

        List<SearchItem> results = EverythingService.IsAvailable()
            ? EverythingService.Search(kw)
            : IndexService.Search(kw);

        ResultList.ItemsSource = results;

        string src = EverythingService.IsAvailable() ? "Everything" : "自建索引";
        StatusText.Text = $"{src}：找到 {results.Count} 个结果";
    }

    private async void StartEverything_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "正在启动 Everything…";
        bool ok = EverythingService.TryStart();
        if (!ok)
        {
            await ShowDialog("未能启动 Everything",
                "没找到 Everything.exe。\n\n请到 https://www.voidtools.com/ 下载安装（免费），" +
                "安装后保持后台运行，本工具即可全盘秒搜。\n\n不装也能用：点“建立索引”搜索常用目录。");
            StatusText.Text = "未找到 Everything";
            return;
        }

        // 等它起来
        await Task.Delay(1500);
        UpdateStatus();

        if (EverythingService.IsAvailable()) DoSearch();
    }

    private async void BuildIndex_Click(object sender, RoutedEventArgs e)
    {
        if (EverythingService.IsAvailable())
        {
            StatusText.Text = "Everything 已可用，秒搜已开启，无需建立索引。";
            return;
        }

        _indexCts = new CancellationTokenSource();

        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<string>
        {
            Path.Combine(user, "Desktop"),
            Path.Combine(user, "Documents"),
            Path.Combine(user, "Downloads"),
            Path.Combine(user, "Pictures")
        }.Where(Directory.Exists).ToList();

        if (roots.Count == 0)
        {
            StatusText.Text = "没有可索引的目录。";
            return;
        }

        StatusText.Text = "正在建立索引，请稍候…";
        try
        {
            int count = await IndexService.BuildIndexAsync(
                roots,
                new Progress<string>(p => StatusText.Text = "正在索引：" + p),
                _indexCts.Token);

            StatusText.Text = $"索引完成，共 {count} 个文件。";
        }
        catch (Exception ex)
        {
            StatusText.Text = "索引失败：" + ex.Message;
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not SearchItem item) return;
        try
        {
            Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = "打开失败：" + ex.Message;
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not SearchItem item) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\""));
        }
        catch (Exception ex)
        {
            StatusText.Text = "打开失败：" + ex.Message;
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not SearchItem item) return;
        try
        {
            var dp = new DataPackage();
            dp.SetText(item.FullPath);
            Clipboard.SetContent(dp);
            StatusText.Text = "路径已复制";
        }
        catch (Exception ex)
        {
            StatusText.Text = "复制失败：" + ex.Message;
        }
    }

    private async Task ShowDialog(string title, string content)
    {
        try
        {
            await new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "知道了",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
        catch { }
    }
}
