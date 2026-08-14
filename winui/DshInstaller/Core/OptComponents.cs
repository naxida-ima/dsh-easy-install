using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DshInstaller.Core;

public class OptComponent
{
    public string Key = "";
    public string Name = "";
    public string Desc = "";
    public string Official = "";
}

public static class OptComponents
{
    public static readonly OptComponent[] Components =
    {
        new() { Key = "node",   Name = "Node.js（长期支持版）", Desc = "JavaScript 运行环境。安装器已内置一份，这里可再装系统版供其他程序使用。", Official = "https://nodejs.org/" },
        new() { Key = "python", Name = "Python 3",             Desc = "通用编程语言，很多工具脚本需要它。静默安装到当前用户，自动加入 PATH。", Official = "https://www.python.org/downloads/" },
        new() { Key = "git",    Name = "Git",                  Desc = "代码版本管理工具，开发必备。静默安装，不重启。", Official = "https://git-scm.com/download/win" },
        new() { Key = "php",    Name = "PHP",                  Desc = "网站后端语言。自动下载绿色版并解压到用户目录，加入 PATH。", Official = "https://windows.php.net/download" },
        new() { Key = "wsl",    Name = "WSL（Windows 的 Linux 子系统）", Desc = "可以在 Windows 里运行 Linux。安装需管理员权限，可能要求重启。", Official = "https://learn.microsoft.com/zh-cn/windows/wsl/install" },
    };

    private static readonly string Dldir = Path.Combine(Path.GetTempPath(), "dsh-opt");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly Dictionary<string, (string Url, string Kind)> Fallback = new()
    {
        ["node"] = ("https://nodejs.org/dist/v24.19.0/node-v24.19.0-x64.msi", "msi"),
        ["python"] = ("https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe", "python"),
        ["git"] = ("https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.1/Git-2.47.1-64-bit.exe", "git"),
        ["php"] = ("https://windows.php.net/downloads/releases/php-8.3.14-nts-Win32-vs16-x64.zip", "phpzip"),
    };

    private static string? FindOnPath(string exe)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(full)) return full;
            }
            catch { }
        }
        return null;
    }

    public static bool IsInstalled(string key)
    {
        switch (key)
        {
            case "node": return FindOnPath("node.exe") is not null;
            case "python": return FindOnPath("python.exe") is not null;
            case "git": return FindOnPath("git.exe") is not null;
            case "php": return FindOnPath("php.exe") is not null;
            case "wsl":
            {
                var sysroot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
                return File.Exists(Path.Combine(sysroot, "System32", "wsl.exe"));
            }
            default: return false;
        }
    }

    private static (string Url, string Kind)? Resolve(string key)
    {
        try
        {
            switch (key)
            {
                case "node":
                {
                    var doc = JsonDocument.Parse(Http.GetStringAsync("https://nodejs.org/dist/index.json").Result);
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("lts", out var lts) && lts.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var v = el.GetProperty("version").GetString()!;
                            return ($"https://nodejs.org/dist/{v}/node-{v}-x64.msi", "msi");
                        }
                    }
                    break;
                }
                case "python":
                {
                    var html = Http.GetStringAsync("https://www.python.org/ftp/python/").Result;
                    var best = System.Text.RegularExpressions.Regex.Matches(html, @"href=""(\d+\.\d+\.\d+)/""")
                        .Select(m => m.Groups[1].Value)
                        .OrderByDescending(s => System.Version.Parse(s))
                        .FirstOrDefault();
                    if (best is not null)
                        return ($"https://www.python.org/ftp/python/{best}/python-{best}-amd64.exe", "python");
                    break;
                }
                case "git":
                {
                    var doc = JsonDocument.Parse(Http.GetStringAsync("https://api.github.com/repos/git-for-windows/git/releases/latest").Result);
                    var tag = doc.RootElement.GetProperty("tag_name").GetString()!;
                    var ver = tag.Replace("v", "").Split(".windows.")[0];
                    return ($"https://github.com/git-for-windows/git/releases/download/{tag}/Git-{ver}-64-bit.exe", "git");
                }
                case "php":
                {
                    var html = Http.GetStringAsync("https://windows.php.net/downloads/releases/").Result;
                    var best = System.Text.RegularExpressions.Regex.Matches(html,
                            @"href=""(php-(8\.\d+\.\d+)-nts-Win32-vs16-x64\.zip)""")
                        .Select(m => (File: m.Groups[1].Value, Ver: System.Version.Parse(m.Groups[2].Value)))
                        .OrderByDescending(x => x.Ver)
                        .FirstOrDefault();
                    if (best.File is not null)
                        return ($"https://windows.php.net/downloads/releases/{best.File}", "phpzip");
                    break;
                }
            }
        }
        catch { }
        return Fallback.TryGetValue(key, out var fb) ? fb : null;
    }

    private static bool Download(string url, string dest, IProgress<int> progress)
    {
        try
        {
            Directory.CreateDirectory(Dldir);
            using var resp = Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
            if (!resp.IsSuccessStatusCode) return false;
            long total = resp.Content.Headers.ContentLength ?? 0;
            using var src = resp.Content.ReadAsStream();
            using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write);
            var buf = new byte[256 * 1024];
            long done = 0;
            int n;
            while ((n = src.Read(buf, 0, buf.Length)) > 0)
            {
                fs.Write(buf, 0, n);
                done += n;
                if (total > 0) progress?.Report((int)(done * 100 / total));
            }
            return new FileInfo(dest).Length > 0;
        }
        catch { return false; }
    }

    private static bool RunSilent(string file, string args, int timeoutSec = 900)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            return p.WaitForExit(timeoutSec * 1000) && p.ExitCode is 0 or 3010;
        }
        catch { return false; }
    }

    private static bool AddUserPath(string dir)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey("Environment");
            if (key is null) return false;
            var cur = (key.GetValue("Path") as string) ?? "";
            var parts = cur.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            if (!parts.Contains(dir))
            {
                parts.Add(dir);
                key.SetValue("Path", string.Join(";", parts), RegistryValueKind.ExpandString);
            }
            return true;
        }
        catch { return false; }
    }

    public static (bool ok, string msg) Install(string key, IProgress<int>? progress = null, Action<string>? status = null)
    {
        if (key == "wsl")
        {
            status?.Invoke("正在启用 WSL 功能…（需要管理员权限）");
            var sysroot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            var wslExe = Path.Combine(sysroot, "System32", "wsl.exe");
            if (!File.Exists(wslExe)) return (false, "系统没有 wsl.exe，请打开微软官网按说明启用");
            var ok = RunSilent(wslExe, "--install --no-distribution", 600);
            return ok
                ? (true, "WSL 功能安装成功，可能需要重启电脑生效")
                : (false, "WSL 安装未完成（可能被拒绝或需要管理员权限）");
        }

        var resolved = Resolve(key);
        if (resolved is null) return (false, "无法获取下载地址，请点击「打开官网」手动安装");
        var (url, kind) = resolved.Value;
        var ext = kind switch { "msi" => ".msi", "python" or "git" => ".exe", _ => ".zip" };
        var dest = Path.Combine(Dldir, $"{key}-latest{ext}");

        status?.Invoke("正在下载官方安装包…");
        if (!Download(url, dest, progress ?? new Progress<int>()))
            return (false, "下载失败，请检查网络或点击「打开官网」手动安装");

        status?.Invoke("正在静默安装…（无需操作，请稍候）");
        bool ok2 = kind switch
        {
            "msi" => RunSilent("msiexec", $"/i \"{dest}\" /qn /norestart"),
            "python" => RunSilent(dest, "/quiet InstallAllUsers=0 PrependPath=1 Include_test=0 Include_launcher=0"),
            "git" => RunSilent(dest, "/VERYSILENT /NORESTART /SP-"),
            "phpzip" => InstallPhpZip(dest),
            _ => false,
        };

        try { File.Delete(dest); } catch { }

        if (ok2 && IsInstalled(key)) return (true, "安装完成，新开的终端窗口即可直接使用");
        if (ok2) return (true, "安装完成（未检测到命令，可能需要重开终端或重启）");
        return (false, "安装未成功，请点击「打开官网」手动安装");
    }

    private static bool InstallPhpZip(string zipPath)
    {
        try
        {
            var target = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Path.GetTempPath(), "php");
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
            var phpDir = Directory.GetDirectories(target).FirstOrDefault() ?? target;
            return AddUserPath(phpDir);
        }
        catch { return false; }
    }
}
