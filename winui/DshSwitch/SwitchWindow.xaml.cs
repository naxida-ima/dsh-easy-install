using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using DshInstaller.Core;

namespace DshSwitch;

public sealed partial class SwitchWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _running;
    private bool _busy;
    private bool _closing;
    private readonly bool _minimized;
    private readonly bool _launchWeb;

    public SwitchWindow(bool minimized, bool launchWeb)
    {
        InitializeComponent();
        _minimized = minimized;
        _launchWeb = launchWeb;
        SetupWindow();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();

        LoadVersion();
        LoadAutoStart();
        Poll();

        if (minimized)
            this.AppWindow.Hide();
        if (launchWeb)
        {
            var _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                DispatcherQueue.TryEnqueue(() => Toggle());
            });
        }
    }

    private void SetupWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(480, 620));
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        appWindow.Move(new PointInt32(
            area.WorkArea.X + (area.WorkArea.Width - 480) / 2,
            area.WorkArea.Y + (area.WorkArea.Height - 620) / 2));
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.ico");
            if (System.IO.File.Exists(iconPath))
                appWindow.SetIcon(iconPath);
        }
        catch { }
    }

    private void LoadVersion()
    {
        try
        {
            if (System.IO.File.Exists(Paths.InstallJson))
            {
                var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(Paths.InstallJson));
                if (doc.RootElement.TryGetProperty("dsh_version", out var v))
                    VerText.Text = "v" + v.GetString();
            }
        }
        catch { }
    }

    private void LoadAutoStart()
    {
        AutoSwitch.IsOn = Installer.GetAutoStart();
    }

    private void Poll()
    {
        var running = DshService.IsRunning();
        _running = running;
        UpdateUi();
    }

    private void UpdateUi()
    {
        var green = Color.FromArgb(255, 78, 216, 132);
        var gray = Color.FromArgb(255, 138, 144, 178);
        if (_running)
        {
            GlowRing.Fill = new SolidColorBrush(Color.FromArgb(40, 78, 216, 132));
            OuterRing.Fill = new SolidColorBrush(Color.FromArgb(30, 78, 216, 132));
            OuterRing.Stroke = new SolidColorBrush(Color.FromArgb(120, 78, 216, 132));
            InnerCircle.Fill = new SolidColorBrush(Color.FromArgb(255, 32, 60, 46));
            PowerIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 110, 255, 170));
            OnOffText.Text = "ON";
            OnOffText.Foreground = new SolidColorBrush(Color.FromArgb(255, 110, 255, 170));
            StateText.Text = "● 正在运行";
            StateText.Foreground = new SolidColorBrush(green);
            WebBtn.IsEnabled = true;
        }
        else
        {
            GlowRing.Fill = new SolidColorBrush(Color.FromArgb(20, 90, 95, 130));
            OuterRing.Fill = new SolidColorBrush(Color.FromArgb(20, 27, 30, 51));
            OuterRing.Stroke = new SolidColorBrush(Color.FromArgb(80, 128, 138, 178));
            InnerCircle.Fill = new SolidColorBrush(Color.FromArgb(255, 42, 46, 76));
            PowerIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 203, 220));
            OnOffText.Text = "OFF";
            OnOffText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 203, 220));
            StateText.Text = "○ 已停止";
            StateText.Foreground = new SolidColorBrush(gray);
            WebBtn.IsEnabled = false;
        }
    }

    private void Toggle()
    {
        if (_busy) return;
        if (_running)
        {
            _busy = true;
            StateText.Text = "正在停止…";
            StateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 181, 68));
            _ = Task.Run(async () =>
            {
                var (ok, msg) = await Task.Run(DshService.Stop);
                DispatcherQueue.TryEnqueue(() => Finish(ok, msg));
            });
        }
        else
        {
            _busy = true;
            StateText.Text = "正在启动…";
            StateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 181, 68));
            _ = Task.Run(async () =>
            {
                var (ok, msg) = await Task.Run(DshService.Start);
                DispatcherQueue.TryEnqueue(() =>
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
        StateText.Foreground = new SolidColorBrush(ok
            ? Color.FromArgb(255, 46, 160, 90)
            : Color.FromArgb(255, 220, 60, 70));
    }

    private async Task OpenWeb()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(Paths.WebUrl));
        }
        catch { }
    }

    private void SwitchTapped(object sender, TappedRoutedEventArgs e) => Toggle();

    private void WebBtn_Click(object sender, RoutedEventArgs e) => _ = OpenWeb();

    private void AutoSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        Installer.SetAutoStart(AutoSwitch.IsOn);
    }

    private void UninstallBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "卸载 DeepSeek Harness",
            Content = "确定要卸载吗？\n将停止服务并删除全部程序文件。",
            PrimaryButtonText = "卸载",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot,
        };
        _ = dialog.ShowAsync().AsTask().ContinueWith(async t =>
        {
            if (t.Result != ContentDialogResult.Primary) return;
            await Task.Run(() =>
            {
                Installer.UninstallAll();
                try
                {
                    var bat = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh_uninstall.bat");
                    System.IO.File.WriteAllText(bat,
                        "@echo off\r\ntimeout /t 2 /nobreak >nul\r\n" +
                        $"rmdir /s /q \"{Paths.InstallRoot}\"\r\n" +
                        "del \"%~f0\"\r\n");
                    Process.Start(new ProcessStartInfo(bat) { UseShellExecute = true });
                }
                catch { }
            });
            DispatcherQueue.TryEnqueue(() =>
            {
                _closing = true;
                Close();
            });
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
