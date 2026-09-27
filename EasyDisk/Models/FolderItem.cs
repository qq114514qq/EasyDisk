using System.Collections.ObjectModel;

namespace EasyDisk.Models;

public class FolderItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
    public int FileCount { get; set; }
    public double Percent { get; set; }

    /// <summary>是否为系统/受保护目录（不允许删除）</summary>
    public bool IsProtected { get; set; }

    /// <summary>受保护时显示的提示文字</summary>
    public string ProtectionText => IsProtected ? "系统保护 · 不可删除" : "";

    /// <summary>受保护项在界面上显示为红色</summary>
    public bool ShowWarning => IsProtected;

    public string SizeText => Format(Size);
    public string Summary => $"{FileCount} 个文件  •  {Format(Size)}";

    public static string Format(long bytes)
    {
        if (bytes < 0) bytes = 0;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double d = bytes;
        int i = 0;
        while (d >= 1024 && i < units.Length - 1)
        {
            d /= 1024;
            i++;
        }
        return i == 0 ? $"{d:0} {units[i]}" : $"{d:0.##} {units[i]}";
    }
}
