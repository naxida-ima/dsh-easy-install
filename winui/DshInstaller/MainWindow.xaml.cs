using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;
using DshInstaller.Core;
using DshInstaller.Pages;

namespace DshInstaller;

public sealed partial class MainWindow : Window
{
    private static readonly string[] StepNames = { "欢迎", "环境检测", "可选组件", "安装", "完成" };

    private int _step;
    private readonly List<(Border circle, TextBlock label)> _railItems = new();
    private DetectPage? _detectPage;
    private InstallPage? _installPage;
    private DonePage? _donePage;

    public MainWindow()
    {
        InitializeComponent();
        SetupWindow();
        BuildRail();
        ShowStep(0);
    }

    private void SetupWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(1020, 720));
        appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        appWindow.Move(new PointInt32(
            area.WorkArea.X + (area.WorkArea.Width - 1020) / 2,
            area.WorkArea.Y + (area.WorkArea.Height - 720) / 2));
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.ico");
            if (System.IO.File.Exists(iconPath))
                appWindow.SetIcon(iconPath);
        }
        catch { }
    }

    // ---------- 步骤条 ----------
    private void BuildRail()
    {
        RailPanel.Children.Clear();
        _railItems.Clear();
        for (int i = 0; i < StepNames.Length; i++)
        {
            var circle = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 90, 95, 133)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var num = new TextBlock
            {
                Text = (i + 1).ToString(),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 238, 240, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            circle.Child = num;

            var label = new TextBlock
            {
                Text = StepNames[i],
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 144, 184)),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
            row.Children.Add(circle);
            row.Children.Add(label);
            RailPanel.Children.Add(row);
            _railItems.Add((circle, label));

            if (i < StepNames.Length - 1)
            {
                RailPanel.Children.Add(new Rectangle
                {
                    Width = 2,
                    Height = 14,
                    Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 64, 96)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(15, 0, 0, 0),
                });
            }
        }
        UpdateRail();
    }

    private void UpdateRail()
    {
        for (int i = 0; i < _railItems.Count; i++)
        {
            var (circle, label) = _railItems[i];
            if (i < _step)
            {
                circle.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 78, 216, 132));
                ((TextBlock)circle.Child).Text = "✓";
                ((TextBlock)circle.Child).Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 14, 28));
                label.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 64, 96));
            }
            else if (i == _step)
            {
                circle.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 77, 107, 254));
                ((TextBlock)circle.Child).Text = (i + 1).ToString();
                ((TextBlock)circle.Child).Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                label.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 22, 40));
                label.FontWeight = Windows.UI.Text.FontWeights.Bold;
            }
            else
            {
                circle.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 90, 95, 133));
                ((TextBlock)circle.Child).Text = (i + 1).ToString();
                ((TextBlock)circle.Child).Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 238, 240, 255));
                label.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 144, 184));
                label.FontWeight = Windows.UI.Text.FontWeights.Normal;
            }
        }
    }

    // ---------- 导航 ----------
    public void ShowStep(int i)
    {
        _step = i;
        UpdateRail();
        BtnBack.IsEnabled = i > 0;

        switch (i)
        {
            case 0:
                ContentFrame.Navigate(typeof(WelcomePage));
                BtnNext.Content = "开始检测 →";
                BtnNext.IsEnabled = true;
                break;
            case 1:
                _detectPage = new DetectPage();
                _detectPage.OnCompleted += (_, args) =>
                {
                    BtnNext.IsEnabled = true;
                };
                ContentFrame.Content = _detectPage;
                BtnNext.Content = "下一步 →";
                BtnNext.IsEnabled = false;
                _detectPage.StartDetect();
                break;
            case 2:
                ContentFrame.Navigate(typeof(OptionalPage));
                BtnNext.Content = "开始安装 →";
                BtnNext.IsEnabled = true;
                break;
            case 3:
                _installPage = new InstallPage();
                _installPage.OnFinished += (_, args) =>
                {
                    BtnNext.Content = "完成 →";
                    BtnNext.IsEnabled = args.Success;
                };
                ContentFrame.Content = _installPage;
                BtnNext.Content = "下一步 →";
                BtnNext.IsEnabled = false;
                break;
            case 4:
                _donePage = new DonePage();
                ContentFrame.Content = _donePage;
                BtnNext.Content = "关闭";
                BtnNext.IsEnabled = true;
                break;
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) ShowStep(_step - 1);
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
            case 1:
            case 2:
                ShowStep(_step + 1);
                break;
            case 3:
                _installPage?.StartInstall();
                break;
            case 4:
                Close();
                break;
        }
    }
}
