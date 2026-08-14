using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace DshInstaller.Core;

/// <summary>DeepSeek Harness 服务管理：启动 / 停止 / 状态检测</summary>
public static class DshService
{
    public static bool IsRunning() => Detector.PortOpen(Paths.Port);

    private static int? ReadPid()
    {
        try
        {
            if (File.Exists(Paths.PidFile))
                return int.Parse(File.ReadAllText(Paths.PidFile).Trim());
        }
        catch { }
        return null;
    }

    private static void WritePid(int pid)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Paths.PidFile)!);
            File.WriteAllText(Paths.PidFile, pid.ToString());
        }
        catch { }
    }

    private static bool ProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch { return false; }
    }

    private static int? FindPidByPort(int port)
    {
        try
        {
            var psi = new ProcessStartInfo("netstat", "-ano")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            var outText = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            foreach (var line in outText.Split('\n'))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && parts[0] == "TCP"
                    && parts[1].EndsWith($":{port}") && parts[3] == "LISTENING")
                {
                    if (int.TryParse(parts[4], out var pid) && pid != 0)
                        return pid;
                }
            }
        }
        catch { }
        return null;
    }

    private static void TaskKill(int pid)
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskkill", $"/PID {pid} /T /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch { }
    }

    public static (bool ok, string msg) Start()
    {
        if (IsRunning()) return (true, "服务已在运行");
        if (!File.Exists(Paths.NodeExe)) return (false, "未找到 Node 运行时，请先运行安装程序");
        if (!File.Exists(Paths.DshEntry)) return (false, "未找到 DeepSeek Harness 程序文件，请先运行安装程序");

        try
        {
            Directory.CreateDirectory(Paths.LogDir);
            var psi = new ProcessStartInfo(Paths.NodeExe, $"\"{Paths.DshEntry}\" web")
            {
                WorkingDirectory = Paths.RuntimeAppDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };
            var envPath = Path.Combine(Paths.RuntimeNodeDir) + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
            psi.Environment["PATH"] = envPath;
            using var proc = Process.Start(psi);
            if (proc is null) return (false, "启动进程失败");
            WritePid(proc.Id);
        }
        catch (Exception e)
        {
            Paths.Log($"start error: {e.Message}");
            return (false, $"启动失败：{e.Message}");
        }

        // 等待端口就绪
        for (int i = 0; i < 90; i++)
        {
            if (IsRunning()) return (true, "服务已启动");
            Thread.Sleep(500);
        }
        return (false, "启动超时，请查看日志");
    }

    public static (bool ok, string msg) Stop()
    {
        if (!IsRunning())
        {
            try { File.Delete(Paths.PidFile); } catch { }
            return (true, "服务已停止");
        }
        var pid = ReadPid();
        if (pid is int p && ProcessAlive(p))
            TaskKill(p);
        else
        {
            var byPort = FindPidByPort(Paths.Port);
            if (byPort is int bp) TaskKill(bp);
        }
        for (int i = 0; i < 30; i++)
        {
            if (!IsRunning()) break;
            Thread.Sleep(400);
        }
        try { File.Delete(Paths.PidFile); } catch { }
        return IsRunning()
            ? (false, "未能完全停止（端口仍占用），请关闭占用程序后重试")
            : (true, "服务已停止");
    }
}
