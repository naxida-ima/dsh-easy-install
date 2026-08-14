using Microsoft.UI.Xaml;

namespace DshSwitch;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var minimized = Environment.GetCommandLineArgs().Contains("--minimized");
        var launchWeb = Environment.GetCommandLineArgs().Contains("--launch-web");
        _window = new SwitchWindow(minimized, launchWeb);
        _window.Activate();
    }
}
