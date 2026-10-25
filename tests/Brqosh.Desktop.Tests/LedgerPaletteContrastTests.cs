using System.Windows;
using System.Windows.Media;
using Brqosh.Desktop.Themes;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») <b>المفاتيحُ الدلاليّة الجديدة تُقاس كأخواتها:</b> كانت خلفيّاتُ الحالات وحالاتُ التعطيل والتمرير ألواناً زرقاءَ مثبّتةً
/// داخل الأنماط (#FEE2E2 و#10B981 و#12384F…) فلا تتبع الثيم ولا تُقلب في الداكن؛ نقلناها إلى <see cref="ThemeManager"/> ويحرس هذا الملفُّ
/// أنّ كلَّ زوجٍ يقع عليه نصٌّ أو مؤشّرُ تركيزٍ مقروءٌ في الثيمين — 4.5 للنصّ و3 للمؤشّر (WCAG 1.4.11).
/// </summary>
public class LedgerPaletteContrastTests
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

    private static ResourceDictionary Palette(bool dark)
    {
        // ‏قاموسٌ فارغ عمداً: SetColor ينشئ الفرشاةَ إن غابت، فيُكتشف مفتاحٌ نُسي في إحدى اللوحتين (يُرمى KeyNotFound عند القراءة).
        var r = new ResourceDictionary();
        ThemeManager.ApplyTo(r, dark);
        return r;
    }

    private static Color Of(ResourceDictionary r, string brushKey) => ((SolidColorBrush)r[brushKey]).Color;

    public static IEnumerable<object[]> BothThemes() => new[] { new object[] { true }, new object[] { false } };

    [Theory]
    [MemberData(nameof(BothThemes))]
    public void EveryStatusColour_IsReadableOnItsSoftBackground(bool dark)
    {
        var p = Palette(dark);
        foreach (var status in new[] { "Success", "Danger", "Warning", "Info" })
        {
            var ratio = Ratio(Of(p, status + "Brush"), Of(p, status + "SoftBrush"));
            Assert.True(ratio >= 4.5, $"{(dark ? "الداكن" : "الفاتح")}: {status} على {status}Soft = {ratio:F2}.");
        }
    }

    [Theory]
    [MemberData(nameof(BothThemes))]
    public void TheSolidStatusButtons_KeepTheirLabelReadable(bool dark)
    {
        var p = Palette(dark);
        // ‏زرُّ النجاح (عادي ومرّر) وتمريرُ «حذف» الناعم يكتبان OnPrimary فوق لون الحالة الممتلئ.
        foreach (var fill in new[] { "SuccessBrush", "SuccessDarkBrush", "DangerBrush" })
        {
            var ratio = Ratio(Of(p, "OnPrimaryBrush"), Of(p, fill));
            Assert.True(ratio >= 4.5, $"{(dark ? "الداكن" : "الفاتح")}: OnPrimary على {fill} = {ratio:F2}.");
        }
    }

    [Theory]
    [MemberData(nameof(BothThemes))]
    public void TheFocusRingAndTheNavBar_AreVisibleOnEverySurfaceTheyLandOn(bool dark)
    {
        var p = Palette(dark);
        // ‏الحلقةُ (AppFocusVisual) بلون AccentBrush: عنصرُ واجهةٍ ≥ 3 على الورقة والخلفيّة والشريط الجانبيّ وحالته المضغوطة.
        foreach (var surface in new[] { "BackgroundBrush", "SurfaceBrush", "SidebarBrush", "SidebarHoverBrush" })
        {
            var ratio = Ratio(Of(p, "AccentBrush"), Of(p, surface));
            Assert.True(ratio >= 3.0, $"{(dark ? "الداكن" : "الفاتح")}: AccentBrush على {surface} = {ratio:F2}.");
        }
    }

    [Theory]
    [MemberData(nameof(BothThemes))]
    public void TheNavItemText_IsReadableOnTheSidebarAndItsHoverState(bool dark)
    {
        var p = Palette(dark);
        foreach (var surface in new[] { "SidebarBrush", "SidebarHoverBrush" })
        {
            var ratio = Ratio(Of(p, "SidebarTextBrush"), Of(p, surface));
            Assert.True(ratio >= 4.5, $"{(dark ? "الداكن" : "الفاتح")}: SidebarText على {surface} = {ratio:F2}.");
        }
    }

    [Theory]
    [MemberData(nameof(BothThemes))]
    public void TheScrollThumb_StandsApartFromThePaper(bool dark)
    {
        var p = Palette(dark);
        // ‏مقبضُ التمرير غيرُ نصٍّ لكنّه أداةٌ: لا يذوب في الخلفيّة (≥ 1.3 كالفاصل) ويشتدّ عند التمرير إلى RuleStrong.
        var rest = Ratio(Of(p, "ScrollThumbBrush"), Of(p, "BackgroundBrush"));
        Assert.True(rest >= 1.3, $"{(dark ? "الداكن" : "الفاتح")}: ScrollThumb على Background = {rest:F2}.");
        var hover = Ratio(Of(p, "RuleStrongBrush"), Of(p, "BackgroundBrush"));
        Assert.True(hover >= 3.0, $"{(dark ? "الداكن" : "الفاتح")}: RuleStrong على Background = {hover:F2}.");
    }
}
