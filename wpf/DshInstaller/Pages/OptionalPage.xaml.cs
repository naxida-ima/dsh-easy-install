using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public partial class OptionalPage : UserControl
{
    public OptionalPage()
    {
        InitializeComponent();
        foreach (var c in OptComponents.Components)
            CardPanel.Children.Add(BuildCard(c));
    }

    private Border BuildCard(OptComponent comp)
    {
        var dot = new TextBlock { Text = "●", FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = comp.Name, FontSize = 14, FontWeight = FontWeights.SemiBold,
                                   VerticalAlignment = VerticalAlignment.Center };
        var state = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                                    HorizontalAlignment = HorizontalAlignment.Right };
        var btnWeb = new Button { Content = "打开官网", Style = (Style)FindResource("SecondaryButton") };
        var btnInstall = new Button { Content = "下载安装", Style = (Style)FindResource("PrimaryButton") };

        var head = new Grid { Margin = new Thickness(18, 12, 18, 0) };
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

        var prog = new ProgressBar { Height = 5, Margin = new Thickness(38, 10, 18, 0),
                                     Visibility = Visibility.Collapsed, Minimum = 0, Maximum = 100 };

        var panel = new StackPanel();
        panel.Children.Add(head);
        panel.Children.Add(new TextBlock
        {
            Text = comp.Desc,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 116, 150)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(38, 4, 18, 12),
        });
        panel.Children.Add(prog);

        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(227, 230, 240)),
            BorderThickness = new Thickness(1),
            Child = panel,
            Margin = new Thickness(0, 0, 0, 10),
        };

        Refresh();
        void Refresh()
        {
            if (OptComponents.IsInstalled(comp.Key))
            {
                dot.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
                state.Text = "已安装 ✓";
                state.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
                btnInstall.IsEnabled = false;
                btnInstall.Content = "已安装";
            }
            else
            {
                dot.Foreground = new SolidColorBrush(Color.FromRgb(154, 160, 184));
                state.Text = "未安装";
                state.Foreground = new SolidColorBrush(Color.FromRgb(130, 136, 165));
                btnInstall.IsEnabled = true;
                btnInstall.Content = "下载安装";
            }
        }

        btnWeb.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(comp.Official) { UseShellExecute = true }); }
            catch { }
        };

        btnInstall.Click += async (_, _) =>
        {
            btnInstall.IsEnabled = false;
            btnInstall.Content = "处理中…";
            state.Text = "准备中…";
            state.Foreground = new SolidColorBrush(Color.FromRgb(200, 140, 30));
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
                state.Foreground = new SolidColorBrush(Color.FromRgb(46, 160, 90));
                Refresh();
            }
            else
            {
                state.Text = "✗ " + msg;
                state.Foreground = new SolidColorBrush(Color.FromRgb(220, 60, 70));
                btnInstall.IsEnabled = true;
                btnInstall.Content = "重试";
            }
        };

        return card;
    }
}
