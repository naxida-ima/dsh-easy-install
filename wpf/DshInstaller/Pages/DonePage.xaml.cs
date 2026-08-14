using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public partial class DonePage : UserControl
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
                Process.Start(new ProcessStartInfo(Paths.WebUrl) { UseShellExecute = true });
            }
        }
        catch { }
        Window.GetWindow(this)?.Close();
    }
}
