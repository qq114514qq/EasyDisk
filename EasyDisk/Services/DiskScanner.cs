using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyDisk.Models;

namespace EasyDisk.Services;

public static class DiskScanner
{
    /// <summary>
    /// 扫描结果缓存：路径 -> 结果列表。
    /// 返回上层 / 再次进入同一目录时直接读缓存，绝不重复扫描。
    /// </summary>
    private static readonly ConcurrentDictionary<string, List<FolderItem>> _cache = new();

    public static void ClearCache() => _cache.Clear();

    public static bool HasCache(string path) =>
        !string.IsNullOrEmpty(path) && _cache.ContainsKey(Norm(path));

    private static string Norm(string p)
    {
        try { return Path.GetFullPath(p).TrimEnd('\\').ToLowerInvariant(); }
        catch { return p.ToLowerInvariant(); }
    }

    /// <summary>
    /// 扫描目录下的子文件夹大小。force=true 时忽略缓存强制重扫。
    /// 结果始终包含系统目录（标记为 IsProtected），由界面决定是否显示/允许删除。
    /// </summary>
    public static async Task<List<FolderItem>> ScanAsync(
        string root,
        IProgress<double> progress,
        CancellationToken token,
        bool force = false)
    {
        var result = new List<FolderItem>();
        if (!Directory.Exists(root)) return result;

        string key = Norm(root);

        if (!force && _cache.TryGetValue(key, out var cached))
        {
            progress?.Report(100);
            return cached;
        }

        await Task.Run(() =>
        {
            var dirs = SafeGetDirectories(root);

            for (int i = 0; i < dirs.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                string dir = dirs[i];
                bool protect = SystemGuard.IsProtected(dir);

                long size = 0;
                int count = 0;

                // 受保护目录只算顶层大小，不深入递归，避免卡在系统目录里
                if (protect)
                {
                    foreach (var f in SafeEnumerateFiles(dir))
                    {
                        if (token.IsCancellationRequested) break;
                        try { size += f.Length; count++; } catch { }
                    }
                }
                else
                {
                    ScanFolder(dir, ref size, ref count, token);
                }

                result.Add(new FolderItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    Size = size,
                    FileCount = count,
                    IsProtected = protect
                });

                progress?.Report((double)(i + 1) / dirs.Count * 100);
            }

            // 根目录下的散落文件单独作为一项
            long rootSize = 0;
            int rootCount = 0;
            foreach (var f in SafeEnumerateFiles(root))
            {
                if (token.IsCancellationRequested) break;
                try { rootSize += f.Length; rootCount++; } catch { }
            }
            if (rootCount > 0)
            {
                result.Add(new FolderItem
                {
                    Name = "(根目录散落文件)",
                    FullPath = root,
                    Size = rootSize,
                    FileCount = rootCount,
                    IsProtected = true
                });
            }
        });

        result = result.OrderByDescending(x => x.Size).ToList();
        if (result.Count > 0)
        {
            double max = Math.Max(result[0].Size, 1);
            foreach (var r in result) r.Percent = r.Size / max * 100;
        }

        if (!token.IsCancellationRequested)
            _cache[key] = result;

        return result;
    }

    private static void ScanFolder(string path, ref long size, ref int count, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        foreach (var f in SafeEnumerateFiles(path))
        {
            if (token.IsCancellationRequested) return;
            try { size += f.Length; count++; } catch { }
        }

        foreach (var d in SafeGetDirectories(path))
        {
            if (token.IsCancellationRequested) return;
            ScanFolder(d, ref size, ref count, token);
        }
    }

    public static List<string> SafeGetDirectories(string path)
    {
        try { return Directory.GetDirectories(path).ToList(); }
        catch { return new List<string>(); }
    }

    private static IEnumerable<FileInfo> SafeEnumerateFiles(string path)
    {
        try { return new DirectoryInfo(path).EnumerateFiles(); }
        catch { return Enumerable.Empty<FileInfo>(); }
    }
}
