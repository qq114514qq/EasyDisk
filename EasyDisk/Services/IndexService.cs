using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyDisk.Models;

namespace EasyDisk.Services;

/// <summary>
/// 自建索引：当本机没有 Everything 时使用。
/// 扫描指定目录下的所有文件，保存为 JSON，之后搜索直接读索引，速度很快。
/// </summary>
public static class IndexService
{
    private static string IndexFile =>
        Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "easydisk_index.json");

    public static bool HasIndex()
    {
        try { return File.Exists(IndexFile); }
        catch { return false; }
    }

    public static DateTime IndexTime()
    {
        try { return File.GetLastWriteTime(IndexFile); }
        catch { return default; }
    }

    public static async Task<int> BuildIndexAsync(IEnumerable<string> roots, IProgress<string> progress, CancellationToken token)
    {
        var entries = new List<SearchItem>();

        await Task.Run(() =>
        {
            foreach (var root in roots)
            {
                if (token.IsCancellationRequested) break;
                if (!Directory.Exists(root)) continue;

                progress?.Report(root);
                try
                {
                    foreach (var f in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        if (token.IsCancellationRequested) break;
                        try
                        {
                            entries.Add(new SearchItem
                            {
                                FullPath = f.FullName,
                                Name = f.Name,
                                DirectoryPath = f.DirectoryName ?? "",
                                Size = f.Length,
                                Modified = f.LastWriteTime
                            });
                        }
                        catch { /* 跳过无权限文件 */ }
                    }
                }
                catch { /* 跳过无权限目录 */ }
            }
        }, token);

        if (!token.IsCancellationRequested)
        {
            var json = JsonSerializer.Serialize(entries);
            await File.WriteAllTextAsync(IndexFile, json);
        }

        return entries.Count;
    }

    public static List<SearchItem> Search(string keyword, int max = 800)
    {
        var result = new List<SearchItem>();
        if (!HasIndex()) return result;

        try
        {
            var json = File.ReadAllText(IndexFile);
            var entries = JsonSerializer.Deserialize<List<SearchItem>>(json);
            if (entries == null) return result;

            var kw = keyword.ToLowerInvariant();
            foreach (var e in entries)
            {
                if (e.Name != null && e.Name.ToLowerInvariant().Contains(kw))
                {
                    result.Add(e);
                    if (result.Count >= max) break;
                }
            }
        }
        catch { /* 索引损坏时返回空 */ }

        return result;
    }
}
