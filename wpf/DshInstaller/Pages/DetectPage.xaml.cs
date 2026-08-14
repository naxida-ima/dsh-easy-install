using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public partial class DetectPage : UserControl
{
    public event EventHandler? OnCompleted;

    public DetectPage() => InitializeComponent();

    public async void StartDetect()
    {
        TitleText.Text = "正在检查你的电脑…";
        Spinner.Visibility = Visibility.Visible;
        SummaryCard.Visibility = Visibility.Collapsed;
        ItemPanel.Children.Clear();

        var results = await Task.Run(() => Detector.RunAll());

        foreach (var r in results)
            ItemPanel.Children.Add(BuildItem(r));

        var (level, msg) = Detector.Summary(results);
        TitleText.Text = "检测完成";
        Spinner.Visibility = Visibility.Collapsed;
        SummaryCard.Visibility = Visibility.Visible;
        var color = level switch
        {
            CheckLevel.Ok => Color.FromRgb(46, 160, 90),
            CheckLevel.Warn => Color.FromRgb(200, 140, 30),
            _ => Color.FromRgb(220, 60, 70),
        };
        SummaryText.Foreground = new SolidColorBrush(color);
        SummaryText.Text = (level == CheckLevel.Ok ? "✓ " : "") + msg;
        OnCompleted?.Invoke(this, EventArgs.Empty);
    }

    private Border BuildItem(CheckItem r)
    {
        var color = r.Level switch
        {
            CheckLevel.Ok => Color.FromRgb(46, 160, 90),
            CheckLevel.Warn => Color.FromRgb(200, 140, 30),
            CheckLevel.Fail => Color.FromRgb(220, 60, 70),
            _ => Color.FromRgb(30, 120, 200),
        };

        var dot = new TextBlock { Text = "●", FontSize = 13, Foreground = new SolidColorBrush(color),
                                  VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = r.Label, FontSize = 14, FontWeight = FontWeights.SemiBold,
                                    VerticalAlignment = VerticalAlignment.Center };
        var msg = new TextBlock { Text = r.Message, FontSize = 13, Foreground = new SolidColorBrush(color),
                                  HorizontalAlignment = HorizontalAlignment.Right,
                                  VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        var head = new Grid { Margin = new Thickness(18, 12, 18, 0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(dot, 0);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(msg, 2);
        head.Children.Add(dot);
        head.Children.Add(label);
        head.Children.Add(msg);

        var panel = new StackPanel();
        panel.Children.Add(head);
        if (!string.IsNullOrEmpty(r.Detail))
        {
            panel.Children.Add(new TextBlock
            {
                Text = r.Detail,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 116, 150)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(40, 3, 18, 12),
            });
        }

        return new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(227, 230, 240)),
            BorderThickness = new Thickness(1),
            Child = panel,
            Margin = new Thickness(0, 0, 0, 8),
        };
    }
}
