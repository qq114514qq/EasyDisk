using System;
using System.IO;

namespace EasyDisk.Services;

/// <summary>
/// 系统目录保护：识别 Windows / Program Files / AppData 等
/// 一旦误删会导致系统损坏的目录，禁止在界面上删除，默认也不显示。
/// </summary>
public static class SystemGuard
{
    // 盘根目录下的一级目录名，命中即视为受保护
    private static readonly string[] ProtectedRootDirs =
    {
        "windows", "program files", "program files (x86)", "programdata",
        "$recycle.bin", "system volume information", "perflogs", "windowsapps",
        "recovery", "boot", "documents and settings", "winsxs", "sysprep",
        "program files windowsapps", "onedrivetemp", "esd", "sources", "drivers"
    };

    // 路径中任意一段命中即受保护（如 Users\xxx\AppData）
    private static readonly string[] ProtectedSegments =
    {
        "appdata", "application data", "local settings", "$windows.~ws", "$windows.~bt",
        "windows.old", "microsoft", "nethood", "printhood", "sendto", "cookies",
        "recent", "templates", "start menu"
    };

    public static bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            string full = Path.GetFullPath(path).TrimEnd('\\').ToLowerInvariant();

            // 盘根目录本身（如 C:\）
            if (full.Length <= 3) return true;

            string[] segs = full.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 0) return true;

            // 第一级目录（如 C:\Windows）
            string first = segs.Length > 1 ? segs[1] : segs[0];
            foreach (var p in ProtectedRootDirs)
            {
                if (first == p) return true;
            }

            // Users 目录本身和用户主目录（C:\Users、C:\Users\xxx）受保护
            if (first == "users" && segs.Length <= 3) return true;

            // 路径中任意一段命中敏感段
            for (int i = 2; i < segs.Length; i++)
            {
                foreach (var s in ProtectedSegments)
                {
                    if (segs[i] == s) return true;
                }
            }

            return false;
        }
        catch
        {
            // 解析异常时按受保护处理，宁可不删
            return true;
        }
    }
}
