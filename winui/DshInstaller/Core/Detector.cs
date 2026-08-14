using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DshInstaller.Core;

public enum CheckLevel { Ok, Warn, Fail, Info }

public class CheckItem
{
    public string Key = "";
    public string Label = "";
    public CheckLevel Level = CheckLevel.Ok;
    public string Message = "";
    public string Detail = "";
    public int Order;

    public static CheckItem Ok(string key, string label, string msg, string detail = "", int order = 0)
        => new() { Key = key, Label = label, Level = CheckLevel.Ok, Message = msg, Detail = detail, Order = order };
    public static CheckItem Warn(string key, string label, string msg, string detail = "", int order = 0)
        => new() { Key = key, Label = label, Level = CheckLevel.Warn, Message = msg, Detail = detail, Order = order };
    public static CheckItem Fail(string key, string label, string msg, string detail = "", int order = 0)
        => new() { Key = key, Label = label, Level = CheckLevel.Fail, Message = msg, Detail = detail, Order = order };
    public static CheckItem Info(string key, string label, string msg, string detail = "", int order = 0)
        => new() { Key = key, Label = label, Level = CheckLevel.Info, Message = msg, Detail = detail, Order = order };
}

public static class Detector
{
    // ---------- Win32 ----------
    [StructLayout(LayoutKind.Sequential)]
    private struct OSVERSIONINFOEXW
    {
        public uint dwOSVersionInfoSize;
        public uint dwMajorVersion;
        public uint dwMinorVersion;
        public uint dwBuildNumber;
        public uint dwPlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szCSDVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("ntdll.dll")]
    private static extern int RtlGetVersion(ref OSVERSIONINFOEXW info);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static (string name, uint major, uint build)? OsVersion()
    {
        try
        {
            var info = new OSVERSIONINFOEXW { dwOSVersionInfoSize = (uint)Marshal.SizeOf<OSVERSIONINFOEXW>() };
            if (RtlGetVersion(ref info) != 0) return null;
            string name = info.dwMajorVersion switch
            {
                10 when info.dwBuildNumber >= 22000 => "Windows 11",
                10 => "Windows 10",
                6 when info.dwMinorVersion == 1 => "Windows 7",
                6 => "Windows 8/8.1",
                _ => $"Windows (NT {info.dwMajorVersion}.{info.dwMinorVersion})"
            };
            return (name, info.dwMajorVersion, info.dwBuildNumber);
        }
        catch { return null; }
    }

    private static ulong TotalMemBytes()
    {
        try
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref ms) ? ms.ullTotalPhys : 0;
        }
        catch { return 0; }
    }

    private static long FreeDiskBytes(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var root = Path.GetPathRoot(Path.GetFullPath(dir));
            var di = new DriveInfo(root ?? "C:");
            return di.AvailableFreeSpace;
        }
        catch { return 0; }
    }

    public static bool PortOpen(int port, int timeoutMs = 700)
    {
        try
        {
            using var c = new TcpClient();
            var task = c.ConnectAsync("127.0.0.1", port);
            return task.Wait(timeoutMs) && c.Connected;
        }
        catch { return false; }
    }

    private static List<string> DetectBrowsers()
    {
        var found = new List<string>();
        var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
        var cands = new Dictionary<string, string[]>
        {
            ["Edge"] = [Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
                        Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe")],
            ["Chrome"] = [Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
                          Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe")],
            ["Firefox"] = [Path.Combine(pf, "Mozilla Firefox", "firefox.exe"),
                           Path.Combine(pf86, "Mozilla Firefox", "firefox.exe")],
        };
        foreach (var (name, paths) in cands)
            foreach (var p in paths)
                if (File.Exists(p)) { found.Add(name); break; }
        return found;
    }

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

    private static bool NetOk(int timeoutMs = 4000)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("api.deepseek.com", timeoutMs);
            return reply?.Status == IPStatus.Success;
        }
        catch
        {
            try
            {
                using var c = new TcpClient();
                var task = c.ConnectAsync("api.deepseek.com", 443);
                return task.Wait(timeoutMs) && c.Connected;
            }
            catch { return false; }
        }
    }

    private static bool ReadInstalled(out string version)
    {
        version = "?";
        try
        {
            if (File.Exists(Paths.InstallJson))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Paths.InstallJson));
                if (doc.RootElement.TryGetProperty("dsh_version", out var v))
                    version = v.GetString() ?? "?";
                return true;
            }
        }
        catch { }
        return false;
    }

    // ---------- 检测主入口 ----------
    public static List<CheckItem> RunAll()
    {
        var list = new List<CheckItem>();

        // 1 位数
        if (Environment.Is64BitOperatingSystem)
            list.Add(CheckItem.Ok("arch", "系统位数", "64 位系统 ✓",
                "安装包需要 64 位 Windows，您的电脑符合要求。", 1));
        else
            list.Add(CheckItem.Fail("arch", "系统位数", "32 位系统，无法安装",
                "DeepSeek Harness 需要 64 位 Windows。", 1));

        // 2 系统版本
        var ver = OsVersion();
        if (ver is null)
            list.Add(CheckItem.Info("os", "操作系统", "无法获取系统版本", "", 2));
        else if (ver.Value.major >= 10)
            list.Add(CheckItem.Ok("os", "操作系统", $"{ver.Value.name}（内部版本 {ver.Value.build}）✓",
                "DeepSeek Harness 支持 Windows 10 及更新版本。", 2));
        else
            list.Add(CheckItem.Fail("os", "操作系统", $"{ver.Value.name} 太旧，无法安装",
                "需要 Windows 10/11（内置运行环境已不支持 Win7）。", 2));

        // 3 内存
        var mem = TotalMemBytes();
        if (mem == 0)
            list.Add(CheckItem.Warn("mem", "内存", "无法检测内存", "建议 4GB 以上。", 3));
        else if (mem >= Paths.MinMemBytes)
            list.Add(CheckItem.Ok("mem", "内存", $"{mem / 1024 / 1024 / 1024} GB 内存 ✓", "内存充足。", 3));
        else
            list.Add(CheckItem.Warn("mem", "内存", $"内存仅 {mem / 1024 / 1024 / 1024} GB",
                "建议 4GB 以上内存，内存不足时界面可能较慢。", 3));

        // 4 磁盘
        var disk = FreeDiskBytes(Paths.InstallRoot);
        if (disk == 0)
            list.Add(CheckItem.Warn("disk", "磁盘空间", "无法检测磁盘空间", "安装需要约 1.5GB。", 4));
        else if (disk >= Paths.MinDiskBytes)
            list.Add(CheckItem.Ok("disk", "磁盘空间", $"可用 {disk / 1024 / 1024 / 1024} GB ✓", "空间充足。", 4));
        else
            list.Add(CheckItem.Fail("disk", "磁盘空间", $"可用空间不足（仅 {disk / 1024 / 1024 / 1024} GB）",
                "安装需要约 1.5GB，请清理磁盘。", 4));

        // 5 端口
        if (PortOpen(Paths.Port))
            list.Add(CheckItem.Warn("port", "端口 3080", "端口被占用",
                "DeepSeek Harness 需要 3080 端口，当前被其他程序占用（可能已安装实例在运行）。", 5));
        else
            list.Add(CheckItem.Ok("port", "端口 3080", "端口空闲 ✓", "", 5));

        // 6 浏览器
        var browsers = DetectBrowsers();
        if (browsers.Count > 0)
            list.Add(CheckItem.Ok("browser", "浏览器", $"检测到：{string.Join("、", browsers.Take(3))} ✓", "", 6));
        else
            list.Add(CheckItem.Warn("browser", "浏览器", "未找到常见浏览器",
                "DeepSeek Harness 的界面需要浏览器打开。", 6));

        // 7 已有 Node
        if (FindOnPath("node.exe") is string nd)
            list.Add(CheckItem.Info("node", "电脑已有 Node.js", $"已安装：{nd}",
                "安装包会使用自带的内置运行环境，互不影响。", 7));
        else
            list.Add(CheckItem.Ok("node", "电脑已有 Node.js", "无需另外安装 ✓", "", 7));

        // 8 已安装
        if (ReadInstalled(out var iv))
            list.Add(CheckItem.Info("installed", "已有安装", $"已安装过（版本 {iv}）", "会重新校验并覆盖，放心继续。", 8));
        else
            list.Add(CheckItem.Ok("installed", "已有安装", "全新安装 ✓", "", 8));

        // 9 网络
        if (NetOk())
            list.Add(CheckItem.Ok("net", "网络", "可以联网 ✓",
                "安装过程不需要网络，使用 AI 功能时需要联网。", 9));
        else
            list.Add(CheckItem.Warn("net", "网络", "暂时检测不到外网",
                "安装不需要网络，但使用 AI 对话功能时需要联网。", 9));

        return list.OrderBy(x => x.Order).ToList();
    }

    public static (CheckLevel level, string msg) Summary(List<CheckItem> items)
    {
        int fails = items.Count(x => x.Level == CheckLevel.Fail);
        int warns = items.Count(x => x.Level == CheckLevel.Warn);
        if (fails > 0)
            return (CheckLevel.Fail, $"发现 {fails} 个需要处理的问题，处理后可继续安装");
        if (warns > 0)
            return (CheckLevel.Warn, $"有 {warns} 个小提示（不阻塞安装），可以继续");
        return (CheckLevel.Ok, "环境检查全部通过，可以放心安装");
    }
}
