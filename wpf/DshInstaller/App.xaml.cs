using System.Windows;
using DshInstaller.Core;

namespace DshInstaller;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--selftest"))
        {
            var code = SelfTest.Run();
            Shutdown(code);
            return;
        }
        var win = new MainWindow();
        win.Show();
    }
}
