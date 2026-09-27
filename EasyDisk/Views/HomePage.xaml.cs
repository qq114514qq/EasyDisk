using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EasyDisk.Models;

namespace EasyDisk.Views;

public sealed partial class HomePage : Page
{
    public class DriveInfoVm
    {
        public string Label { get; set; } = "";
        public string SpaceText { get; set; } = "";
        public double UsedPercent { get; set; }
    }

    public HomePage()
    {
        this.InitializeComponent();
        LoadDrives();
    }

    private void LoadDrives()
    {
        var list = new List<DriveInfoVm>();
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(x => x.IsReady))
            {
                long total = d.TotalSize;
                long free = d.AvailableFreeSpace;
                long used = total - free;
                list.Add(new DriveInfoVm
                {
                    Label = $"{d.Name}  ({d.VolumeLabel})",
                    SpaceText = $"{FolderItem.Format(used)} / {FolderItem.Format(total)}   可用 {FolderItem.Format(free)}",
                    UsedPercent = total > 0 ? (double)used / total * 100 : 0
                });
            }
        }
        catch { }

        DrivesList.ItemsSource = list;
    }

    private void GoDisk_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.NavigateTo("DiskPage");
    }

    private void GoSearch_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.NavigateTo("SearchPage");
    }
}
