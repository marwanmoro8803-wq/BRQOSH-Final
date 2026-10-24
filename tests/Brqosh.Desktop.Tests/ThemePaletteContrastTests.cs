using System.Windows;
using System.Windows.Media;
using Brqosh.Desktop.Themes;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل الخطوط متناسقة مع الخلفية؟») <b>الجواب يُقاس لا يُقال:</b> كلُّ لونِ نصٍّ في لوحة الثيم على كلِّ
/// خلفيةٍ يقع عليها فعلاً، في الثيمين، بنسبة تباين WCAG — 4.5 للنصّ العاديّ (AA) و3 للعناصر الكبيرة/الفاصلة.
///
/// <para>يقرأ الألوان من <see cref="ThemeManager.ApplyTo"/> نفسِها لا من قيمٍ منسوخة، فلا ينفصل عن الحقيقة إن عُدّلت اللوحة.</para>
/// </summary>
public class ThemePaletteContrastTests
{
    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Ratio(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static Color Of(ResourceDictionary r, string brushKey) => ((SolidColorBrush)r[brushKey]).Color;

    private static ResourceDictionary Palette(bool dark)
    {
        var r = new ResourceDictionary();
        foreach (var key in new[]
                 {
                     "BackgroundBrush", "SurfaceBrush", "GlassSurfaceBrush", "PrimaryBrush", "PrimaryDarkBrush", "PrimaryLightBrush",
                     "PrimarySoftBrush", "AccentBrush", "ElectricCyanBrush", "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush",
                     "BorderBrush", "BorderLightBrush", "StatusBarBrush", "StatusBarTextBrush", "SidebarBrush", "RowAltBrush", "RowHoverBrush",
                 })
            r[key] = new SolidColorBrush(Colors.Magenta);

        ThemeManager.ApplyTo(r, dark);
        return r;
    }

    public static IEnumerable<object[]> TextOnSurface()
    {
        foreach (var dark in new[] { true, false })
        foreach (var text in new[] { "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush" })
        foreach (var background in new[] { "BackgroundBrush", "SurfaceBrush", "GlassSurfaceBrush", "RowAltBrush", "RowHoverBrush" })
            yield return new object[] { dark, text, background };
    }

    [Theory]
    [MemberData(nameof(TextOnSurface))]
    public void EveryTextColour_IsReadableOnEverySurfaceItLandsOn(bool dark, string text, string background)
    {
        var palette = Palette(dark);
        var ratio = Ratio(Of(palette, text), Of(palette, background));

        Assert.True(ratio >= 4.5,
            $"{(dark ? "الداكن" : "الفاتح")}: {text} على {background} = {ratio:F2} (المطلوب ≥ 4.5).");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheStatusBarText_IsReadableOnTheStatusBar(bool dark)
    {
        var palette = Palette(dark);
        var ratio = Ratio(Of(palette, "StatusBarTextBrush"), Of(palette, "StatusBarBrush"));

        Assert.True(ratio >= 4.5, $"{(dark ? "الداكن" : "الفاتح")}: شريط الحالة = {ratio:F2}.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheSidebarsFixedNavColours_AreReadableOnTheSidebar(bool dark)
    {
        // ‏عناصرُ التنقّل ألوانُها ثابتةٌ في النمط (#94A3B8 عاديّ و#E2E8F0 عند المرور و#FFFFFF للنشط) فوق الشريط الجانبيّ
        // ‏— وهو داكنٌ في الثيمين، فيُقاس كلٌّ على الخلفية التي يقع عليها فعلاً.
        var palette = Palette(dark);
        var sidebar = Of(palette, "SidebarBrush");

        Assert.True(Ratio(Color.FromRgb(0x94, 0xA3, 0xB8), sidebar) >= 4.5, "نصّ عنصر التنقّل العاديّ على الشريط الجانبيّ.");
        Assert.True(Ratio(Colors.White, sidebar) >= 4.5, "نصّ عنصر التنقّل النشط على الشريط الجانبيّ.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheMainActionColour_IsDistinctFromItsSurface(bool dark)
    {
        // ‏لون الأزرار الرئيسة والروابط (PrimaryBrush) عنصرُ واجهةٍ لا نصٌّ عادي: الحدُّ الأدنى 3 (WCAG 1.4.11) على خلفية الصفحة والبطاقة.
        var palette = Palette(dark);
        foreach (var surface in new[] { "BackgroundBrush", "SurfaceBrush" })
        {
            var ratio = Ratio(Of(palette, "PrimaryBrush"), Of(palette, surface));
            Assert.True(ratio >= 3.0, $"{(dark ? "الداكن" : "الفاتح")}: PrimaryBrush على {surface} = {ratio:F2}.");
        }
    }
}
