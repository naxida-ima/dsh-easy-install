using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DshInstaller.Core;

public static class Bundle
{
    public static Dictionary<string, string> LoadChecksums()
    {
        try
        {
            var p = Paths.BundleFile("checksums.json");
            if (File.Exists(p))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(p)) ?? new();
        }
        catch { }
        return new();
    }

    public static Dictionary<string, string> LoadBundleInfo()
    {
        try
        {
            var p = Paths.BundleFile("bundle_info.json");
            if (File.Exists(p))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(p)) ?? new();
        }
        catch { }
        return new();
    }

    public static string Sha256(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexStringLower(sha.ComputeHash(fs));
    }

    /// <summary>校验离线资源完整。返回问题列表（空 = 就绪）</summary>
    public static List<string> Verify()
    {
        var problems = new List<string>();
        var checks = LoadChecksums();
        foreach (var name in new[] { "node.zip", "dsh.zip" })
        {
            var p = Paths.BundleFile(name);
            if (!File.Exists(p) || new FileInfo(p).Length == 0)
            {
                problems.Add($"{name} 缺失或为空");
                continue;
            }
            if (checks.TryGetValue(name, out var expect))
            {
                try
                {
                    if (!string.Equals(Sha256(p), expect, StringComparison.OrdinalIgnoreCase))
                        problems.Add($"{name} 校验不一致（文件损坏），请重新下载");
                }
                catch (Exception e)
                {
                    problems.Add($"{name} 校验失败：{e.Message}");
                }
            }
        }
        return problems;
    }

    public static void ExtractZip(string zipPath, string dest, Action<long, long, string>? progress, string phase)
    {
        Directory.CreateDirectory(dest);
        using var zip = ZipFile.OpenRead(zipPath);
        long total = zip.Entries.Sum(e => (long)e.Length);
        long done = 0;
        foreach (var entry in zip.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(dest, entry.FullName));
            if (!target.StartsWith(Path.GetFullPath(dest) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(target, Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                continue; // 防路径穿越
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            done += entry.Length;
            progress?.Invoke(done, Math.Max(total, 1), phase);
        }
        progress?.Invoke(total, Math.Max(total, 1), phase);
    }
}
