using System.Windows;

namespace DshSwitch;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var minimized = e.Args.Contains("--minimized");
        var launchWeb = e.Args.Contains("--launch-web");
        var win = new SwitchWindow(minimized, launchWeb);
        win.Show();
    }
}
