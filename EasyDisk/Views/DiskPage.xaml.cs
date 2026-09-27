using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using EasyDisk.Models;
using EasyDisk.Services;
using Microsoft.UI.Dispatching;

namespace EasyDisk.Views;

public sealed partial class DiskPage : Page
{
    private readonly Stack<string> _history = new();
    private string _currentRoot = "";
    private List<FolderItem> _allItems = new();
    private CancellationTokenSource _cts;
    private bool _loading;

    public DiskPage()
    {
        this.InitializeComponent();
        LoadLocations();
    }

    private void LoadLocations()
    {
        var items = new List<KeyValuePair<string, string>>();

        try
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 常用且安全的位置排在前面，避免一上来就扫 C:\ 根目录
            void Add(string label, string path)
            {
                if (Directory.Exists(path)) items.Add(new KeyValuePair<string, string>(label, path));
            }

            Add("下载", Path.Combine(user, "Downloads"));
            Add("桌面", Path.Combine(user, "Desktop"));
            Add("文档", Path.Combine(user, "Documents"));
            Add("图片", Path.Combine(user, "Pictures"));
            Add("用户目录", user);

            foreach (var d in DriveInfo.GetDrives().Where(x => x.IsReady))
            {
                string root = d.RootDirectory.FullName;
                string label = $"{root} 盘";
                if (!items.Any(x => x.Value == root)) Add(label, root);
            }
        }
        catch { }

        DriveBox.DisplayMemberPath = "Key";
        DriveBox.SelectedValuePath = "Value";
        DriveBox.ItemsSource = items;
        if (items.Count > 0) DriveBox.SelectedIndex = 0;
    }

    private string CurrentPath =>
        DriveBox.SelectedValue as string ?? DriveBox.SelectedItem as string;

    private void DriveBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _history.Clear();
        BackButton.IsEnabled = false;
        FolderList.ItemsSource = null;
        _allItems = new List<FolderItem>();
        StatusText.Text = "点“扫描”开始分析";
    }

    private void ShowSystemSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        bool showSystem = ShowSystemSwitch.IsOn;

        var view = showSystem
            ? _allItems
            : _allItems.Where(x => !x.IsProtected).ToList();

        double max = view.Count > 0 ? Math.Max(view[0].Size, 1) : 1;
        foreach (var v in view) v.Percent = max > 0 ? v.Size / max * 100 : 0;

        FolderList.ItemsSource = view;

        long total = view.Sum(x => x.Size);
        int hidden = _allItems.Count(x => x.IsProtected);
        StatusText.Text = string.IsNullOrEmpty(_currentRoot)
            ? ""
            : $"{_currentRoot}   {view.Count} 项，合计 {FolderItem.Format(total)}" +
              (hidden > 0 && !showSystem ? $"（已隐藏 {hidden} 个系统目录）" : "");

        RefreshDeleteButton();
    }

    private void RefreshDeleteButton()
    {
        if (FolderList.SelectedItem is FolderItem item)
        {
            bool canDelete = !item.IsProtected && item.Name != "(根目录散落文件)";
            DeleteButton.IsEnabled = canDelete;
            HintText.Text = item.IsProtected
                ? "系统保护目录，禁止删除"
                : "";
        }
        else
        {
            DeleteButton.IsEnabled = false;
        }
    }

    private void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshDeleteButton();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        string root = CurrentPath;
        if (string.IsNullOrEmpty(root)) return;
        await RunScanAsync(root, pushHistory: false, force: false);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        string root = string.IsNullOrEmpty(_currentRoot) ? CurrentPath : _currentRoot;
        if (string.IsNullOrEmpty(root)) return;
        await RunScanAsync(root, pushHistory: false, force: true);
    }

    private async void FolderList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_loading) return;
        if (FolderList.SelectedItem is not FolderItem item) return;
        if (item.Name == "(根目录散落文件)") return;

        await RunScanAsync(item.FullPath, pushHistory: true, force: false);
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (_history.Count == 0) return;

        string parent = _history.Pop();
        BackButton.IsEnabled = _history.Count > 0;

        // 走缓存，返回上层绝不重新扫描
        await RunScanAsync(parent, pushHistory: false, force: false);
    }

    private async Task RunScanAsync(string root, bool pushHistory, bool force)
    {
        if (pushHistory && !string.IsNullOrEmpty(_currentRoot) && _currentRoot != root)
            _history.Push(_currentRoot);

        _currentRoot = root;
        BackButton.IsEnabled = _history.Count > 0;

        // 已有缓存且非强制刷新 -> 直接显示，不重扫
        if (!force && DiskScanner.HasCache(root))
        {
            _allItems = await DiskScanner.ScanAsync(root, null, CancellationToken.None, force: false);
            ApplyFilter();
            StatusText.Text = $"{root}   （已缓存，未重复扫描）";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _loading = true;

        ScanButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;
        StatusText.Text = "正在扫描 " + root;
        HintText.Text = "";
        FolderList.ItemsSource = null;

        var progress = new Progress<double>(v =>
        {
            ScanProgress.Value = v;
            StatusText.Text = $"正在扫描 {root}  {v:0}%";
        });

        try
        {
            var items = await DiskScanner.ScanAsync(root, progress, _cts.Token, force: force);
            // 防御性：确保不为 null
            _allItems = items ?? new List<FolderItem>();

            // 在 UI 线程应用过滤与绑定
            if (DispatcherQueue?.HasThreadAccess ?? false)
                ApplyFilter();
            else
                DispatcherQueue.TryEnqueue(() => ApplyFilter());
        }
        catch (Exception ex)
        {
            StatusText.Text = "扫描出错：" + ex.Message;
        }
        finally
        {
            _loading = false;
            ScanProgress.Visibility = Visibility.Collapsed;
            ScanButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is not FolderItem item) return;

        // 双重保险：受保护目录一律不允许删除
        if (item.IsProtected || SystemGuard.IsProtected(item.FullPath))
        {
            await ShowDialog("无法删除", "这是系统保护目录，出于安全考虑不允许删除。");
            return;
        }
        if (item.Name == "(根目录散落文件)") return;

        bool toBin = SettingsService.UseRecycleBin;

        var dialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"{item.FullPath}\n\n大小：{item.SizeText}，{item.FileCount} 个文件\n\n" +
                      (toBin ? "将移入回收站，可恢复。" : "将永久删除，无法恢复！"),
            PrimaryButtonText = toBin ? "移入回收站" : "永久删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            StatusText.Text = "正在删除…";
            if (toBin)
            {
                await Task.Run(() => RecycleBin.Send(item.FullPath));
            }
            else
            {
                await Task.Run(() => Directory.Delete(item.FullPath, true));
            }

            DiskScanner.ClearCache();
            _history.Clear();
            BackButton.IsEnabled = false;

            await RunScanAsync(_currentRoot, pushHistory: false, force: true);
            HintText.Text = "已删除";
        }
        catch (Exception ex)
        {
            HintText.Text = "删除失败：" + ex.Message;
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
