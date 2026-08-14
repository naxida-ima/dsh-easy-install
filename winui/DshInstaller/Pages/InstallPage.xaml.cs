using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public sealed partial class InstallPage : UserControl
{
    public event EventHandler<InstallFinishedArgs>? OnFinished;

    private bool _installing;

    public InstallPage()
    {
        InitializeComponent();
    }

    public void StartInstall()
    {
        if (_installing) return;
        _installing = true;
        StartBtn.IsEnabled = false;
        StartBtn.Content = "安装中…";
        LogBox.Text = "";

        string lastPhase = "";
        void Phase(string p)
        {
            if (p == lastPhase) return;
            lastPhase = p;
            PhaseText.Text = p;
            Log(p);
        }
        void Log(string txt) => LogBox.Text += txt + Environment.NewLine;

        var progress = new Progress<(long done, long total, string phase)>(v =>
        {
            if (!string.IsNullOrEmpty(v.phase)) Phase(v.phase);
            if (v.total > 0)
            {
                var pct = (int)Math.Min(100, v.done * 100 / v.total);
                InstBar.Value = pct;
                PctText.Text = pct + "%";
            }
        });

        _ = Task.Run(async () =>
        {
            var (ok, msg) = await Task.Run(() =>
                Installer.InstallAll((d, t, p) => ((IProgress<(long, long, string)>)progress).Report((d, t, p))));
            DispatcherQueue.TryEnqueue(() =>
            {
                _installing = false;
                StartBtn.IsEnabled = true;
                StartBtn.Content = "开始安装";
                if (ok)
                {
                    PhaseText.Text = "✅ " + msg;
                    PhaseText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 160, 90));
                    InstBar.Value = 100;
                    PctText.Text = "100%";
                    Log("✔ 安装成功！");
                    OnFinished?.Invoke(this, new InstallFinishedArgs(true));
                }
                else
                {
                    PhaseText.Text = "❌ " + msg;
                    PhaseText.Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 60, 70));
                    Log("✘ " + msg);
                    OnFinished?.Invoke(this, new InstallFinishedArgs(false));
                }
            });
        });
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e) => StartInstall();
}

public class InstallFinishedArgs : EventArgs
{
    public bool Success { get; }
    public InstallFinishedArgs(bool success) => Success = success;
}
