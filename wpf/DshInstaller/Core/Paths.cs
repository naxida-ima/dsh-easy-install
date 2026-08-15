using System.IO;

namespace DshInstaller.Core;

/// <summary>路径与全局常量（安装器与开关共用）</summary>
public static class Paths
{
    public const string AppName = "DeepSeek Harness";
    public const string AppKey = "DeepSeekHarness";
    public const string ToolVersion = "2.0.2";
    public const int Port = 3080;
    public const string WebUrl = "http://127.0.0.1:3080";

    public const long MinMemBytes = 4L * 1024 * 1024 * 1024;
    public const long MinDiskBytes = 1500L * 1024 * 1024;

    public static string LocalAppData
    {
        get
        {
            var la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(la)) return la;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
        }
    }

    public static string InstallRoot => Path.Combine(LocalAppData, AppKey);
    public static string RuntimeNodeDir => Path.Combine(InstallRoot, "runtime", "node");
    public static string NodeExe => Path.Combine(RuntimeNodeDir, "node.exe");
    public static string RuntimeAppDir => Path.Combine(InstallRoot, "runtime", "app");
    public static string DshEntry => Path.Combine(RuntimeAppDir, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
    public static string SwitchDir => Path.Combine(InstallRoot, "switch");
    public static string SwitchExe => Path.Combine(SwitchDir, "switch.exe");
    public static string LogDir => Path.Combine(InstallRoot, "logs");
    public static string DshLogFile => Path.Combine(LogDir, "dsh.log");
    public static string PidFile => Path.Combine(InstallRoot, "runtime", "dsh.pid");
    public static string InstallJson => Path.Combine(InstallRoot, "install.json");

    /// <summary>安装包自带的离线资源目录（exe 同目录 _assets）</summary>
    public static string BundleDir => Path.Combine(AppContext.BaseDirectory, "_assets");
    public static string BundleFile(string name) => Path.Combine(BundleDir, name);

    public static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string StartMenuDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);

    public static string SwitchIcon => Path.Combine(AppContext.BaseDirectory, "icon.ico");

    public static void Log(string msg, string tag = "dsh")
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{tag}] {msg}";
            File.AppendAllText(Path.Combine(LogDir, "switch.log"), line + Environment.NewLine);
        }
        catch { }
    }
}
