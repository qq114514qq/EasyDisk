using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EasyDisk.Models;
using Microsoft.Win32;

namespace EasyDisk.Services;

/// <summary>
/// Everything 深度集成。
/// 自动定位本机 Everything 安装目录并加载 Everything64.dll（无需手动复制 dll），
/// 之后即可毫秒级全盘文件名搜索。Everything 未安装/未运行时自动回退到自建索引。
/// </summary>
public static class EverythingService
{
    private const string Dll = "Everything64.dll";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    private static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport(Dll)]
    private static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    private static extern bool Everything_QueryW(bool bWait);

    [DllImport(Dll)]
    private static extern uint Everything_GetNumResults();

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport(Dll)]
    private static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);

    [DllImport(Dll)]
    private static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);

    [DllImport(Dll)]
    private static extern uint Everything_GetLastError();

    private const uint REQUEST_FULL_PATH = 0x00000004;
    private const uint REQUEST_SIZE = 0x00000010;
    private const uint REQUEST_DATE_MODIFIED = 0x00000040;

    private static bool _initDone;
    private static bool _usable;

    /// <summary>Everything 是否可用（已装 dll 且服务在运行）</summary>
    public static bool IsAvailable()
    {
        if (!_initDone) Init();
        if (!_usable) return false;

        try
        {
            Everything_SetSearchW("");
            return Everything_GetLastError() == 0;
        }
        catch { return false; }
    }

    /// <summary>Everything 是否已安装（dll 能加载，不代表正在运行）</summary>
    public static bool IsInstalled()
    {
        if (!_initDone) Init();
        return _usable;
    }

    /// <summary>尝试启动本机 Everything（找到安装目录就拉起来）</summary>
    public static bool TryStart()
    {
        string exe = FindEverythingExe();
        if (exe == null) return false;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }

    private static void Init()
    {
        _initDone = true;
        _usable = false;

        foreach (var path in CandidateDllPaths())
        {
            try
            {
                if (File.Exists(path) && LoadLibraryW(path) != IntPtr.Zero)
                {
                    // dll 加载成功即认为可用；是否真在跑由 IsAvailable 判定
                    _usable = true;
                    return;
                }
            }
            catch { }
        }
    }

    private static IEnumerable<string> CandidateDllPaths()
    {
        // 1. 程序自身目录（用户手动放了一份）
        yield return Path.Combine(AppContext.BaseDirectory, Dll);

        // 2. 注册表里 Everything 的安装位置
        string fromReg = ReadInstallDirFromRegistry();
        if (fromReg != null)
        {
            yield return Path.Combine(fromReg, Dll);
        }

        // 3. 常见安装路径
        string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        yield return Path.Combine(pf, "Everything", Dll);
        yield return Path.Combine(pf, "Everything 1.5a", Dll);
        yield return @"C:\Program Files\Everything\" + Dll;

        // 4. 便携版：桌面 / 下载
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(user, "Desktop", "Everything", Dll);
        yield return Path.Combine(user, "Downloads", "Everything", Dll);
    }

    private static string ReadInstallDirFromRegistry()
    {
        string[] keys =
        {
            @"SOFTWARE\voidtools\Everything",
            @"SOFTWARE\WOW6432Node\voidtools\Everything",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Everything"
        };

        foreach (var k in keys)
        {
            try
            {
                using var lm = Registry.LocalMachine.OpenSubKey(k);
                if (lm != null)
                {
                    var v = lm.GetValue("InstallLocation") ?? lm.GetValue("InstallDir") ?? lm.GetValue("Path");
                    if (v is string s && Directory.Exists(s)) return s;
                }
                using var cu = Registry.CurrentUser.OpenSubKey(k);
                if (cu != null)
                {
                    var v = cu.GetValue("InstallLocation") ?? cu.GetValue("InstallDir") ?? cu.GetValue("Path");
                    if (v is string s2 && Directory.Exists(s2)) return s2;
                }
            }
            catch { }
        }
        return null;
    }

    private static string FindEverythingExe()
    {
        string fromReg = ReadInstallDirFromRegistry();
        if (fromReg != null)
        {
            string exe = Path.Combine(fromReg, "Everything.exe");
            if (File.Exists(exe)) return exe;
        }

        string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        string[] tries =
        {
            Path.Combine(pf, "Everything", "Everything.exe"),
            Path.Combine(pf, "Everything 1.5a", "Everything.exe"),
            @"C:\Program Files\Everything\Everything.exe"
        };
        foreach (var t in tries)
            if (File.Exists(t)) return t;

        return null;
    }

    /// <summary>全盘文件名搜索，返回最多 max 条结果</summary>
    public static List<SearchItem> Search(string keyword, int max = 800)
    {
        var list = new List<SearchItem>();
        if (!IsAvailable()) return list;

        try
        {
            Everything_SetSearchW(keyword);
            Everything_SetRequestFlags(REQUEST_FULL_PATH | REQUEST_SIZE | REQUEST_DATE_MODIFIED);
            if (!Everything_QueryW(true)) return list;

            uint count = Everything_GetNumResults();
            int take = (int)Math.Min(count, (uint)max);
            var sb = new StringBuilder(4096);

            for (uint i = 0; i < take; i++)
            {
                sb.Clear();
                Everything_GetResultFullPathNameW(i, sb, 4096);
                string full = sb.ToString();
                if (string.IsNullOrEmpty(full)) continue;

                Everything_GetResultSize(i, out long size);
                Everything_GetResultDateModified(i, out long ft);

                list.Add(new SearchItem
                {
                    FullPath = full,
                    Name = Path.GetFileName(full),
                    DirectoryPath = Path.GetDirectoryName(full) ?? "",
                    Size = size,
                    Modified = ft > 0 ? DateTime.FromFileTime(ft) : default
                });
            }
        }
        catch { }

        return list;
    }
}
