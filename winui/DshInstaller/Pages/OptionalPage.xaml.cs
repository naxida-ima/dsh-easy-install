using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public sealed partial class OptionalPage : UserControl
{
    public OptionalPage()
    {
        InitializeComponent();
        foreach (var c in OptComponents.Components)
            CardPanel.Children.Add(BuildCard(c));
    }

    private Border BuildCard(OptComponent comp)
    {
        var dot = new TextBlock { Text = "●", FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = comp.Name, FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var state = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var btnWeb = new Button { Content = "打开官网", MinWidth = 90, Padding = new Thickness(14, 6, 14, 6) };
        var btnInstall = new Button { Content = "下载安装", MinWidth = 110, Padding = new Thickness(16, 6, 16, 6), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var desc = new TextBlock { Text = comp.Desc, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 110, 116, 150)), TextWrapping = TextWrapping.Wrap };

        var head = new Grid { ColumnSpacing = 10, Margin = new Thickness(16, 12, 16, 0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(dot, 0);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(state, 2);
        Grid.SetColumn(btnWeb, 3);
        Grid.SetColumn(btnInstall, 4);
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(dot);
        head.Children.Add(name);
        head.Children.Add(state);
        head.Children.Add(btnWeb);
        head.Children.Add(btnInstall);

        var prog = new ProgressBar { Height = 6, Margin = new Thickness(46, 8, 16, 0), Visibility = Visibility.Collapsed, Minimum = 0, Maximum = 100 };

        var panel = new StackPanel { Spacing = 0 };
        panel.Children.Add(head);
        panel.Children.Add(new TextBlock { Text = comp.Desc, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 110, 116, 150)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(46, 4, 16, 12) });
        panel.Children.Add(prog);

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 80, 90, 130)),
            BorderThickness = new Thickness(1),
            Child = panel,
        };

        // 初始状态
        Refresh();

        void Refresh()
        {
            if (OptComponents.IsInstalled(comp.Key))
            {
                dot.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 160, 90));
                state.Text = "已安装 ✓";
                state.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 160, 90));
                btnInstall.IsEnabled = false;
                btnInstall.Content = "已安装";
            }
            else
            {
                dot.Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 155, 190));
                state.Text = "未安装";
                state.Foreground = new SolidColorBrush(Color.FromArgb(255, 130, 136, 165));
                btnInstall.IsEnabled = true;
                btnInstall.Content = "下载安装";
            }
        }

        btnWeb.Click += (_, _) =>
        {
            try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(comp.Official)); }
            catch { }
        };

        btnInstall.Click += async (_, _) =>
        {
            btnInstall.IsEnabled = false;
            btnInstall.Content = "处理中…";
            state.Text = "准备中…";
            state.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 140, 30));
            prog.Visibility = Visibility.Visible;
            prog.Value = 0;

            var progress = new Progress<int>(v => prog.Value = v);
            var status = new Progress<string>(s => state.Text = s);
            var (ok, msg) = await Task.Run(() =>
                OptComponents.Install(comp.Key, progress, s => ((IProgress<string>)status).Report(s)));

            prog.Visibility = Visibility.Collapsed;
            if (ok)
            {
                state.Text = "✓ " + msg;
                state.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 160, 90));
                Refresh();
            }
            else
            {
                state.Text = "✗ " + msg;
                state.Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 60, 70));
                btnInstall.IsEnabled = true;
                btnInstall.Content = "重试";
            }
        };

        return card;
    }
}
