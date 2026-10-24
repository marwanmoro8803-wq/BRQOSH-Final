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
        // ‏(«مِسطرة») ‏البطاقةُ قيدٌ في دفتر: عنوانٌ صغيرٌ ورقمٌ بحبرٍ داكن — لا شارةُ حروفٍ ولا شريطُ لونٍ ولا رقمٌ ملوَّن
        // ‏(كان لون الرقم لونَ البطاقة فيسقط تباينُه إلى ١٫٨ في بعضها). والسالبُ بين قوسين بحبرٍ أحمر كما في الدفاتر.
        // ‏وبقي المُعامل `accentHex` والرمزُ `monogram` في التوقيع كي لا ينكسر أيُّ نداء.
        _ = (monogram, accentHex);

        var titleBlock = new TextBlock
        {
            Text = title,
            Style = UiResources.StyleOf(host, "KpiTitleText"),
            Margin = new Thickness(0, 0, 0, 8),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var valuePanel = new StackPanel { Orientation = Orientation.Horizontal };
        var valueBlock = new TextBlock
        {
            Text = FormatValue(value, kind),
            Style = UiResources.StyleOf(host, "KpiValueText"),
        };
        if (value < 0m)
            valueBlock.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        valuePanel.Children.Add(valueBlock);

        if (kind == KpiValueKind.Currency)
        {
            valuePanel.Children.Add(new TextBlock
            {
                Text = " " + CurrencyLabel.Short,
                Style = UiResources.StyleOf(host, "KpiCurrencySuffixText"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        var body = new StackPanel();
        body.Children.Add(titleBlock);
        body.Children.Add(valuePanel);

        return new Border
        {
            Style = UiResources.StyleOf(host, "KpiMetricCard"),
            Width = double.NaN,
            MinWidth = 200,
            MinHeight = 96,
            Margin = new Thickness(0),
            Child = body,
        };
    }

    private static string FormatValue(decimal value, KpiValueKind kind) =>
        kind == KpiValueKind.Count
            ? ((int)value).ToString("N0")
            : value < 0m ? $"({Math.Abs(value):N2})" : value.ToString("N2");
}
