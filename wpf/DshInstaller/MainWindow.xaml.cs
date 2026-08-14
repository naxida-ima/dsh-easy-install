using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DshInstaller.Pages;

namespace DshInstaller;

public partial class MainWindow : Window
{
    private static readonly string[] StepNames = { "欢迎", "环境检测", "可选组件", "安装", "完成" };
    private static readonly Color Brand = Color.FromRgb(77, 107, 254);
    private static readonly Color Green = Color.FromRgb(46, 160, 90);
    private static readonly Color Gray = Color.FromRgb(226, 229, 240);
    private static readonly Color TextDim = Color.FromRgb(154, 160, 184);
    private static readonly Color TextMain = Color.FromRgb(26, 29, 46);

    private int _step;
    private readonly List<(Border circle, TextBlock num, TextBlock label)> _rail = new();
    private DetectPage? _detectPage;
    private InstallPage? _installPage;

    public MainWindow()
    {
        InitializeComponent();
        BuildRail();
        ShowStep(0);
    }

    // ---------- 标题栏 ----------
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    // ---------- 步骤条 ----------
    private void BuildRail()
    {
        RailPanel.Children.Clear();
        _rail.Clear();
        for (int i = 0; i < StepNames.Length; i++)
        {
            var circle = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Gray),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var num = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextDim),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            circle.Child = num;
            var label = new TextBlock
            {
                Text = StepNames[i],
                FontSize = 14,
                Foreground = new SolidColorBrush(TextDim),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 9) };
            row.Children.Add(circle);
            row.Children.Add(label);
            RailPanel.Children.Add(row);
            _rail.Add((circle, num, label));

            if (i < StepNames.Length - 1)
            {
                RailPanel.Children.Add(new Rectangle
                {
                    Width = 2,
                    Height = 16,
                    Fill = new SolidColorBrush(Color.FromRgb(237, 238, 245)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(16, 0, 0, 0),
                });
            }
        }
    }

    private void UpdateRail()
    {
        for (int i = 0; i < _rail.Count; i++)
        {
            var (circle, num, label) = _rail[i];
            if (i < _step)
            {
                circle.Background = new SolidColorBrush(Green);
                num.Text = "✓";
                num.Foreground = Brushes.White;
                label.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                label.FontWeight = FontWeights.Normal;
            }
            else if (i == _step)
            {
                circle.Background = new SolidColorBrush(Brand);
                num.Text = (i + 1).ToString();
                num.Foreground = Brushes.White;
                label.Foreground = new SolidColorBrush(TextMain);
                label.FontWeight = FontWeights.Bold;
            }
            else
            {
                circle.Background = new SolidColorBrush(Gray);
                num.Text = (i + 1).ToString();
                num.Foreground = new SolidColorBrush(TextDim);
                label.Foreground = new SolidColorBrush(TextDim);
                label.FontWeight = FontWeights.Normal;
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
                PageHost.Content = new WelcomePage();
                BtnNext.Content = "开始检测 →";
                BtnNext.IsEnabled = true;
                break;
            case 1:
                _detectPage = new DetectPage();
                _detectPage.OnCompleted += (_, _) =>
                {
                    BtnNext.IsEnabled = true;
                };
                PageHost.Content = _detectPage;
                BtnNext.Content = "下一步 →";
                BtnNext.IsEnabled = false;
                _detectPage.StartDetect();
                break;
            case 2:
                PageHost.Content = new OptionalPage();
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
                PageHost.Content = _installPage;
                BtnNext.Content = "下一步 →";
                BtnNext.IsEnabled = false;
                break;
            case 4:
                PageHost.Content = new DonePage();
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
