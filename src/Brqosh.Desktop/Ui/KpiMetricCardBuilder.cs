using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brqosh.Desktop.Ui;

public enum KpiValueKind
{
    Currency,
    Count,
}

public static class KpiMetricCardBuilder
{
    public static Border Create(
        FrameworkElement host,
        string title,
        string monogram,
        decimal value,
        string accentHex,
        KpiValueKind kind)
    {
        var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex)!);
        var softAccent = new SolidColorBrush(Color.FromArgb(36,
            ((SolidColorBrush)accent).Color.R,
            ((SolidColorBrush)accent).Color.G,
            ((SolidColorBrush)accent).Color.B));

        var badge = new Border
        {
            Style = UiResources.StyleOf(host, "KpiMonogramBadge"),
            Background = softAccent,
            Width = 36,
            Height = 36,
            Child = new TextBlock
            {
                Text = monogram,
                FontSize = monogram.Length > 2 ? 9 : 11,
                FontWeight = FontWeights.Bold,
                Foreground = accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = title,
            Style = UiResources.StyleOf(host, "KpiTitleText"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(badge, 1);
        header.Children.Add(titleBlock);
        header.Children.Add(badge);

        var valuePanel = new StackPanel { Orientation = Orientation.Horizontal };
        valuePanel.Children.Add(new TextBlock
        {
            Text = FormatValue(value, kind),
            Style = UiResources.StyleOf(host, "KpiValueText"),
            Foreground = accent,
        });

        if (kind == KpiValueKind.Currency)
        {
            valuePanel.Children.Add(new TextBlock
            {
                Text = LocaleManager.T(" ر.س", " SAR"),
                Style = UiResources.StyleOf(host, "KpiCurrencySuffixText"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(valuePanel);

        var accentBar = new Border
        {
            Style = UiResources.StyleOf(host, "KpiAccentBar"),
            Background = accent,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(body, 0);
        Grid.SetColumn(accentBar, 1);
        grid.Children.Add(body);
        grid.Children.Add(accentBar);

        return new Border
        {
            Style = UiResources.StyleOf(host, "KpiMetricCard"),
            Width = double.NaN,
            MinWidth = 200,
            MinHeight = 112,
            Margin = new Thickness(6),
            Child = grid,
        };
    }

    private static string FormatValue(decimal value, KpiValueKind kind) =>
        kind == KpiValueKind.Count
            ? ((int)value).ToString("N0")
            : value.ToString("N2");
}
