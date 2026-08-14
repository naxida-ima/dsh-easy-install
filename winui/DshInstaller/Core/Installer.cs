using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace DshInstaller.Core;

public static class Installer
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "DeepSeekHarnessSwitch";

    // ---------- 快捷方式 ----------
    private static bool CreateShortcut(string lnkPath, string target, string workDir, string icon, string desc)
    {
        try
        {
            dynamic shell = Marshal.GetActiveObject("WScript.Shell");
            var lnk = shell.CreateShortcut(lnkPath);
            lnk.TargetPath = target;
            lnk.WorkingDirectory = workDir;
            lnk.IconLocation = $"{icon},0";
            lnk.Description = desc;
            lnk.Save();
            Marshal.FinalReleaseComObject(lnk);
            Marshal.FinalReleaseComObject(shell);
            return true;
        }
        catch
        {
            try
            {
                Type? t = Type.GetTypeFromProgID("WScript.Shell");
                if (t is null) return false;
                dynamic shell = Activator.CreateInstance(t)!;
                var lnk = shell.CreateShortcut(lnkPath);
                lnk.TargetPath = target;
                lnk.WorkingDirectory = workDir;
                lnk.IconLocation = $"{icon},0";
                lnk.Description = desc;
                lnk.Save();
                Marshal.FinalReleaseComObject(lnk);
                Marshal.FinalReleaseComObject(shell);
                return true;
            }
            catch (Exception e)
            {
                Paths.Log($"shortcut failed: {e.Message}");
                return false;
            }
        }
    }

    public static (bool desktop, bool startMenu) CreateShortcuts()
    {
        var result = (desktop: false, startMenu: false);
        var target = Paths.SwitchExe;
        var icon = Paths.SwitchIcon;
        if (!File.Exists(target)) return result;

        try
        {
            var deskLnk = Path.Combine(Paths.DesktopDir, "DeepSeek Harness 开关.lnk");
            result.desktop = CreateShortcut(deskLnk, target, Paths.SwitchDir, icon, "DeepSeek Harness 开关");
        }
        catch { }

        try
        {
            Directory.CreateDirectory(Paths.StartMenuDir);
            var smLnk = Path.Combine(Paths.StartMenuDir, "DeepSeek Harness 开关.lnk");
            result.startMenu = CreateShortcut(smLnk, target, Paths.SwitchDir, icon, "DeepSeek Harness 开关");
        }
        catch { }
        return result;
    }

    // ---------- 开机自启 ----------
    public static bool SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return false;
            if (enabled)
                key.SetValue(RunName, $"\"{Paths.SwitchExe}\" --minimized", RegistryValueKind.String);
            else
                key.DeleteValue(RunName, false);
            return true;
        }
        catch (Exception e)
        {
            Paths.Log($"autostart failed: {e.Message}");
            return false;
        }
    }

    public static bool GetAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunName) is not null;
        }
        catch { return false; }
    }

    // ---------- 主安装流程 ----------
    public static (bool ok, string msg) InstallAll(Action<long, long, string> progress)
    {
        var phases = new Dictionary<string, string>
        {
            ["verify"] = "校验离线资源完整性…",
            ["stop"] = "停止旧的服务实例…",
            ["node"] = "部署内置 Node.js 运行环境…",
            ["dsh"] = "部署 DeepSeek Harness 程序…",
            ["switch"] = "部署桌面开关…",
            ["config"] = "写入配置…",
            ["shortcut"] = "创建桌面快捷方式…",
            ["autostart"] = "设置开机自启…",
        };
        void Phase(string key) => progress?.Invoke(0, 100, phases[key]);

        // 0 校验
        Phase("verify");
        var problems = Bundle.Verify();
        if (problems.Count > 0)
            return (false, "离线资源不完整：" + string.Join("；", problems));

        // 1 停止旧服务
        Phase("stop");
        try { DshService.Stop(); } catch { }

        Directory.CreateDirectory(Paths.InstallRoot);
        var info = Bundle.LoadBundleInfo();

        // 2 Node
        Phase("node");
        Bundle.ExtractZip(Paths.BundleFile("node.zip"), Paths.RuntimeNodeDir, progress, phases["node"]);

        // 3 dsh
        Phase("dsh");
        Bundle.ExtractZip(Paths.BundleFile("dsh.zip"), Paths.RuntimeAppDir, progress, phases["dsh"]);

        // 4 switch（自带）
        Phase("switch");
        CopySwitch();

        // 5 配置
        Phase("config");
        WriteInstallJson(info);

        // 6 快捷方式
        Phase("shortcut");
        CreateShortcuts();

        // 7 自启
        Phase("autostart");
        SetAutoStart(true);

        progress?.Invoke(100, 100, "安装完成");
        return (true, "安装完成");
    }

    private static void CopySwitch()
    {
        try
        {
            var src = Path.Combine(AppContext.BaseDirectory, "switch");
            if (!Directory.Exists(src)) return;
            Directory.CreateDirectory(Paths.SwitchDir);
            foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(src, file);
                var dest = Path.Combine(Paths.SwitchDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
        }
        catch (Exception e)
        {
            Paths.Log($"copy switch failed: {e.Message}");
        }
    }

    private static void WriteInstallJson(Dictionary<string, string> info)
    {
        var data = new Dictionary<string, object>
        {
            ["version"] = Paths.ToolVersion,
            ["dsh_version"] = info.TryGetValue("dsh_version", out var dv) ? dv : "unknown",
            ["node_version"] = info.TryGetValue("node_version", out var nv) ? nv : "unknown",
            ["installed_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["install_root"] = Paths.InstallRoot,
            ["port"] = Paths.Port,
        };
        File.WriteAllText(Paths.InstallJson, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---------- 卸载 ----------
    public static void UninstallAll()
    {
        try { DshService.Stop(); } catch { }
        SetAutoStart(false);
        try { File.Delete(Path.Combine(Paths.DesktopDir, "DeepSeek Harness 开关.lnk")); } catch { }
        try { File.Delete(Path.Combine(Paths.StartMenuDir, "DeepSeek Harness 开关.lnk")); } catch { }
        try
        {
            if (Directory.Exists(Paths.StartMenuDir) && !Directory.EnumerateFileSystemEntries(Paths.StartMenuDir).Any())
                Directory.Delete(Paths.StartMenuDir);
        }
        catch { }
        try { Directory.Delete(Paths.InstallRoot, recursive: true); } catch { }
    }
}
