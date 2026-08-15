using System;
using System.IO;
using DshInstaller.Core;
using P = DshInstaller.Core.Paths;

namespace DshSwitch;

/// <summary>开关自测：检查安装完整性 + 服务启停。</summary>
public static class SwitchSelfTest
{
    public static int Run()
    {
        var logFile = Path.Combine(P.LogDir, "switch-selftest.log");
        void L(string m)
        {
            try
            {
                Directory.CreateDirectory(P.LogDir);
                File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {m}{Environment.NewLine}");
            }
            catch { }
        }

        try
        {
            L("== switch selftest start ==");
            L($"install.json: {File.Exists(P.InstallJson)}");
            L($"node.exe: {File.Exists(P.NodeExe)}");
            L($"dsh entry: {File.Exists(P.DshEntry)}");
            if (!File.Exists(P.InstallJson) || !File.Exists(P.NodeExe) || !File.Exists(P.DshEntry))
            {
                L("SWITCH SELFTEST FAIL: install incomplete");
                return 2;
            }

            var (ok, msg) = DshService.Start();
            if (!ok)
            {
                L("START FAIL: " + msg);
                return 3;
            }
            L($"start OK, port_open={Detector.PortOpen(P.Port)}");
            var (ok2, msg2) = DshService.Stop();
            L($"stop: ok={ok2} msg={msg2}");

            L("SWITCH SELFTEST PASS");
            return 0;
        }
        catch (Exception ex)
        {
            L("SELFTEST EXCEPTION: " + ex);
            return 9;
        }
    }
}
