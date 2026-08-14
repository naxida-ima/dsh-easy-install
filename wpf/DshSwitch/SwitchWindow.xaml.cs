using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DshInstaller.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Forms = System.Windows.Forms;

namespace DshSwitch;

public partial class SwitchWindow : Window
{
    private readonly DispatcherTimer _timer;
    private Forms.NotifyIcon? _tray;
    private bool _running;
    private bool _busy;
    private bool _trayOnly;

    private static readonly Color GreenC = Color.FromRgb(78, 216, 132);
    private static readonly Color GrayC = Color.FromRgb(154, 160, 184);

    public SwitchWindow(bool minimized, bool launchWeb)
    {
        InitializeComponent();
        SetupTray();
        LoadVersion();
        AutoToggle.IsChecked = Installer.GetAutoStart();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
        Poll();

        if (minimized)
        {
            _trayOnly = true;
            Hide();
        }
        if (launchWeb)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(600);
                Toggle();
            });
        }
    }

    private void SetupTray()
    {
        _tray = new Forms.NotifyIcon { Visible = true, Icon = MakeTrayIcon(false) };
        _tray.Text = "DeepSeek Harness：已停止";
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示开关", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("打开界面", null, (_, _) => Dispatcher.Invoke(() => _ = OpenWeb()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Quit));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
    }

    private System.Drawing.Icon MakeTrayIcon(bool on)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var col = on ? System.Drawing.Color.FromArgb(78, 216, 132)
                         : System.Drawing.Color.FromArgb(154, 160, 184);
            using var brush = new SolidBrush(col);
            g.FillEllipse(brush, 1, 1, 14, 14);
            using var white = new SolidBrush(System.Drawing.Color.White);
            g.FillEllipse(white, 4, 4, 8, 8);
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    // ---------- 窗口 ----------
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_trayOnly)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            _timer.Stop();
            _tray?.Dispose();
        }
    }

    private void HideToTray()
    {
        Hide();
        _tray?.ShowBalloonTip(1500, "DeepSeek Harness", "仍在后台运行，双击托盘图标可打开开关", Forms.ToolTipIcon.Info);
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Quit()
    {
        _trayOnly = true;
        _timer.Stop();
        _tray?.Dispose();
        Close();
    }

    // ---------- 状态 ----------
    private void LoadVersion()
    {
        try
        {
            if (File.Exists(Paths.InstallJson))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Paths.InstallJson));
                if (doc.RootElement.TryGetProperty("dsh_version", out var v))
                    VerText.Text = "v" + v.GetString();
            }
        }
        catch { }
    }

    private void Poll()
    {
        _running = DshService.IsRunning();
        UpdateUi();
    }

    private void UpdateUi()
    {
        if (_running)
        {
            SwitchOuter.Background = new RadialGradientBrush(GreenC, Color.FromArgb(0, 78, 216, 132)) { RadiusX = 0.9, RadiusY = 0.9, GradientOrigin = new Point(0.5, 0.5) };
            SwitchCore.Background = new SolidColorBrush(Color.FromRgb(235, 250, 242));
            SwitchCore.BorderBrush = new SolidColorBrush(Color.FromRgb(78, 216, 132));
            PowerText.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
            OnOffText.Text = "ON";
            OnOffText.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
            StateText.Text = "● 正在运行";
            StateText.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
            WebBtn.IsEnabled = true;
        }
        else
        {
            SwitchOuter.Background = new RadialGradientBrush(Color.FromRgb(226, 229, 240), Color.FromRgb(244, 245, 251)) { RadiusX = 0.9, RadiusY = 0.9, GradientOrigin = new Point(0.5, 0.5) };
            SwitchCore.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            SwitchCore.BorderBrush = new SolidColorBrush(Color.FromRgb(227, 230, 240));
            PowerText.Foreground = new SolidColorBrush(Color.FromRgb(154, 160, 184));
            OnOffText.Text = "OFF";
            OnOffText.Foreground = new SolidColorBrush(Color.FromRgb(154, 160, 184));
            StateText.Text = "○ 已停止";
            StateText.Foreground = new SolidColorBrush(Color.FromRgb(130, 136, 165));
            WebBtn.IsEnabled = false;
        }
        _tray.Icon = MakeTrayIcon(_running);
        _tray.Text = _running ? "DeepSeek Harness：运行中" : "DeepSeek Harness：已停止";
    }

    private void Switch_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) Toggle();
    }

    private void Toggle()
    {
        if (_busy) return;
        if (_running)
        {
            _busy = true;
            StateText.Text = "正在停止…";
            StateText.Foreground = new SolidColorBrush(Color.FromRgb(200, 140, 30));
            _ = Task.Run(async () =>
            {
                var (ok, msg) = await Task.Run(DshService.Stop);
                Dispatcher.Invoke(() => Finish(ok, msg));
            });
        }
        else
        {
            _busy = true;
            StateText.Text = "正在启动…";
            StateText.Foreground = new SolidColorBrush(Color.FromRgb(200, 140, 30));
            _ = Task.Run(async () =>
            {
                var (ok, msg) = await Task.Run(DshService.Start);
                Dispatcher.Invoke(() =>
                {
                    Finish(ok, msg);
                    if (ok) _ = OpenWeb();
                });
            });
        }
    }

    private void Finish(bool ok, string msg)
    {
        _busy = false;
        Poll();
        StateText.Text = msg;
        StateText.Foreground = new SolidColorBrush(ok ? Color.FromRgb(46, 160, 90) : Color.FromRgb(220, 60, 70));
    }

    private async Task OpenWeb()
    {
        try { Process.Start(new ProcessStartInfo(Paths.WebUrl) { UseShellExecute = true }); }
        catch { }
        await Task.CompletedTask;
    }

    private void WebBtn_Click(object sender, RoutedEventArgs e) => _ = OpenWeb();

    private void AutoToggle_Checked(object sender, RoutedEventArgs e) => Installer.SetAutoStart(true);
    private void AutoToggle_Unchecked(object sender, RoutedEventArgs e) => Installer.SetAutoStart(false);

    // ---------- 卸载 ----------
    private void UninstallBtn_Click(object sender, RoutedEventArgs e)
    {
        var ask = MessageBox.Show(this, "确定要卸载吗？\n将停止服务并删除全部程序文件。", "卸载 DeepSeek Harness",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes) return;

        _ = Task.Run(async () =>
        {
            await Task.Run(Installer.UninstallAll);
            try
            {
                var bat = Path.Combine(Path.GetTempPath(), "dsh_uninstall.bat");
                File.WriteAllText(bat,
                    "@echo off\r\ntimeout /t 2 /nobreak >nul\r\n" +
                    $"rmdir /s /q \"{Paths.InstallRoot}\"\r\n" +
                    "del \"%~f0\"\r\n");
                Process.Start(new ProcessStartInfo(bat) { UseShellExecute = true });
            }
            catch { }
            Dispatcher.Invoke(Quit);
        });
    }
}
