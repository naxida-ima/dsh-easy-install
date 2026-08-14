using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using DshInstaller.Core;

namespace DshInstaller.Pages;

public sealed partial class DetectPage : UserControl
{
    public event EventHandler? OnCompleted;

    public DetectPage()
    {
        InitializeComponent();
    }

    public async void StartDetect()
    {
        TitleText.Text = "正在检查你的电脑…";
        Spinner.IsActive = true;
        SummaryCard.Visibility = Visibility.Collapsed;
        ItemPanel.Children.Clear();
        BtnNextDisabled();

        var results = await Task.Run(() => Detector.RunAll());

        foreach (var r in results)
            ItemPanel.Children.Add(BuildItem(r));

        var (level, msg) = Detector.Summary(results);
        TitleText.Text = "检测完成";
        Spinner.IsActive = false;
        SummaryCard.Visibility = Visibility.Visible;
        var color = level switch
        {
            CheckLevel.Ok => Color.FromArgb(255, 46, 160, 90),
            CheckLevel.Warn => Color.FromArgb(255, 200, 140, 30),
            _ => Color.FromArgb(255, 220, 60, 70),
        };
        SummaryText.Foreground = new SolidColorBrush(color);
        SummaryText.Text = (level == CheckLevel.Ok ? "✓ " : "") + msg;
        OnCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void BtnNextDisabled()
    {
        // 通过主窗口禁用下一步：依赖 OnCompleted 重新启用
    }

    private Border BuildItem(CheckItem r)
    {
        var color = r.Level switch
        {
            CheckLevel.Ok => Color.FromArgb(255, 46, 160, 90),
            CheckLevel.Warn => Color.FromArgb(255, 200, 140, 30),
            CheckLevel.Fail => Color.FromArgb(255, 220, 60, 70),
            _ => Color.FromArgb(255, 30, 120, 200),
        };
        var dot = new TextBlock
        {
            Text = "●",
            Foreground = new SolidColorBrush(color),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text = r.Label,
            FontSize = 14,
            FontWeight = Windows.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var msg = new TextBlock
        {
            Text = r.Message,
            FontSize = 13,
            Foreground = new SolidColorBrush(color),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(dot, 0);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(msg, 2);
        head.Children.Add(dot);
        head.Children.Add(label);
        head.Children.Add(msg);

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(head);
        if (!string.IsNullOrEmpty(r.Detail))
        {
            panel.Children.Add(new TextBlock
            {
                Text = r.Detail,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 110, 116, 150)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(34, 0, 0, 0),
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 10, 16, 10),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 80, 90, 130)),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
    }
}
