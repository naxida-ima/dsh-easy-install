using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public sealed partial class DonePage : UserControl
{
    public DonePage()
    {
        InitializeComponent();
        SummaryText.Text = $"已安装到：{Paths.InstallRoot}\n端口 {Paths.Port} · 版本 {Paths.ToolVersion}";
    }

    private void LaunchBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(Paths.SwitchExe))
            {
                Process.Start(new ProcessStartInfo(Paths.SwitchExe, "--launch-web")
                {
                    WorkingDirectory = Paths.SwitchDir,
                    UseShellExecute = true,
                });
            }
            else
            {
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(Paths.WebUrl));
            }
        }
        catch { }
    }
}
