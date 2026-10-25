using System.Windows;
using System.Windows.Media;
using Brqosh.Desktop.Themes;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس» · إمكانيّة الوصول) في «التباين العالي» تُستبدل اللوحةُ بألوان النظام، فلا يبقى ورقٌ كريميّ تحت نصٍّ يختاره المستخدم أبيضَ على أسود.
/// </summary>
public class HighContrastThemeTests
{
    private static Color Of(ResourceDictionary r, string key) => ((SolidColorBrush)r[key]).Color;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InHighContrast_TheInkAndPaperAreTheSystemWindowPair_InBothThemes(bool dark)
    {
        var (bg, surface, text, primary, onPrimary, muted) = WpfTestHost.Invoke(() =>
        {
            var r = new ResourceDictionary();
            foreach (var k in System.Windows.Application.Current.Resources.Keys.Cast<object>().Where(k => System.Windows.Application.Current.Resources[k] is SolidColorBrush))
                r[k] = new SolidColorBrush(((SolidColorBrush)System.Windows.Application.Current.Resources[k]).Color);
            ThemeManager.ApplyTo(r, dark, highContrast: true);
            return (Of(r, "BackgroundBrush"), Of(r, "SurfaceBrush"), Of(r, "TextPrimaryBrush"), Of(r, "PrimaryBrush"), Of(r, "OnPrimaryBrush"), Of(r, "TextMutedBrush"));
        });

        Assert.Equal(SystemColors.WindowColor, bg);
        Assert.Equal(SystemColors.WindowColor, surface);
        Assert.Equal(SystemColors.WindowTextColor, text);
        Assert.Equal(SystemColors.HighlightColor, primary);          // ‏خلفيةُ الزرّ الأساسيّ
        Assert.Equal(SystemColors.HighlightTextColor, onPrimary);    // ‏ونصُّه — زوجُ النظام المضمون
        Assert.Equal(SystemColors.GrayTextColor, muted);
    }

    [Fact]
    public void WithoutHighContrast_TheLedgerPaletteIsUntouched()
    {
        var bg = WpfTestHost.Invoke(() =>
        {
            var r = new ResourceDictionary();
            foreach (var k in System.Windows.Application.Current.Resources.Keys.Cast<object>().Where(k => System.Windows.Application.Current.Resources[k] is SolidColorBrush))
                r[k] = new SolidColorBrush(((SolidColorBrush)System.Windows.Application.Current.Resources[k]).Color);
            ThemeManager.ApplyTo(r, dark: false);
            return Of(r, "BackgroundBrush");
        });

        Assert.NotEqual(SystemColors.WindowColor, bg);
    }
}
