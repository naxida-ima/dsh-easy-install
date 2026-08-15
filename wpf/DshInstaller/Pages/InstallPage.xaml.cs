using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public partial class InstallPage : UserControl
{
    public event EventHandler<InstallFinishedArgs>? OnFinished;
    private bool _installing;

    /// <summary>安装是否已成功（成功后按钮变灰、可进入下一步）</summary>
    public bool InstallSucceeded { get; private set; }

    public InstallPage()
    {
        InitializeComponent();
        // 检测已有安装记录（例如从完成页返回本页时）
        if (File.Exists(Paths.InstallJson))
        {
            InstallSucceeded = true;
            StartBtn.IsEnabled = false;
            StartBtn.Content = "已安装完成 ✓";
            PhaseText.Text = "✅ 检测到已完成安装";
            PhaseText.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
            InstBar.Value = 100;
            PctText.Text = "100%";
            LogBox.Text = "✔ 检测到 DeepSeek Harness 已安装。\n可点击「完成 →」进入下一步，或点「重装」覆盖安装。\n";
        }
    }

    public void StartInstall()
    {
        if (_installing || InstallSucceeded) return;
        _installing = true;
        StartBtn.IsEnabled = false;
        StartBtn.Content = "安装中…";
        LogBox.Text = "";

        string lastPhase = "";
        void Log(string txt) => LogBox.Text += txt + Environment.NewLine;

        var progress = new Progress<(long done, long total, string phase)>(v =>
        {
            if (!string.IsNullOrEmpty(v.phase) && v.phase != lastPhase)
            {
                lastPhase = v.phase;
                PhaseText.Text = v.phase;
                Log(v.phase);
            }
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
            await Dispatcher.InvokeAsync(() =>
            {
                _installing = false;
                if (ok)
                {
                    InstallSucceeded = true;
                    StartBtn.IsEnabled = false;
                    StartBtn.Content = "已安装完成 ✓";
                    PhaseText.Text = "✅ " + msg;
                    PhaseText.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
                    InstBar.Value = 100;
                    PctText.Text = "100%";
                    Log("✔ 安装成功！");
                    OnFinished?.Invoke(this, new InstallFinishedArgs(true));
                }
                else
                {
                    InstallSucceeded = false;
                    StartBtn.IsEnabled = true;
                    StartBtn.Content = "重试安装";
                    PhaseText.Text = "❌ " + msg;
                    PhaseText.Foreground = new SolidColorBrush(Color.FromRgb(220, 60, 70));
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
