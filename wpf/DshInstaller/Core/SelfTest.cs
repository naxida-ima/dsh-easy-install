using System;
using System.IO;

namespace DshInstaller.Core;

/// <summary>无头自测：完整跑一遍 校验→安装→启动服务→检查端口→停止。供 CI smoke test 调用。</summary>
public static class SelfTest
{
    public static int Run()
    {
        var logFile = Path.Combine(Paths.LogDir, "selftest.log");
        void L(string m)
        {
            try
            {
                Directory.CreateDirectory(Paths.LogDir);
                File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {m}{Environment.NewLine}");
            }
            catch { }
        }

        try
        {
            L("== selftest start ==");
            L($"install_root: {Paths.InstallRoot}");
            L($"bundle_dir: {Paths.BundleDir}");
            L($"node.zip: {File.Exists(Paths.BundleFile("node.zip"))}");
            L($"dsh.zip: {File.Exists(Paths.BundleFile("dsh.zip"))}");

            var problems = Bundle.Verify();
            if (problems.Count > 0)
            {
                L("VERIFY FAIL: " + string.Join(";", problems));
                return 2;
            }
            L("verify OK");

            var (ok, msg) = Installer.InstallAll((_, _, _) => { });
            if (!ok)
            {
                L("INSTALL FAIL: " + msg);
                return 3;
            }
            L("install OK");
            L($"node.exe: {File.Exists(Paths.NodeExe)}");
            L($"dsh entry: {File.Exists(Paths.DshEntry)}");
            L($"switch.exe: {File.Exists(Paths.SwitchExe)}");
            L($"install.json: {File.Exists(Paths.InstallJson)}");

            var (ok2, msg2) = DshService.Start();
            if (!ok2)
            {
                L("START FAIL: " + msg2);
                return 4;
            }
            L($"start OK, port_open={Detector.PortOpen(Paths.Port)}");

            var (ok3, msg3) = DshService.Stop();
            L($"stop: ok={ok3} msg={msg3}");
            L($"port after stop: {Detector.PortOpen(Paths.Port)}");

            L("SELFTEST PASS");
            return 0;
        }
        catch (Exception ex)
        {
            L("SELFTEST EXCEPTION: " + ex);
            return 9;
        }
    }
}
