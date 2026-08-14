using System.Windows;

namespace DshInstaller;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var win = new MainWindow();
        win.Show();
    }
}
