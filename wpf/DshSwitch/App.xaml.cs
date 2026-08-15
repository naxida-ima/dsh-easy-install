using System.Windows;

namespace DshSwitch;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--selftest"))
        {
            var code = SwitchSelfTest.Run();
            Shutdown(code);
            return;
        }
        var minimized = e.Args.Contains("--minimized");
        var launchWeb = e.Args.Contains("--launch-web");
        var win = new SwitchWindow(minimized, launchWeb);
        win.Show();
    }
}
