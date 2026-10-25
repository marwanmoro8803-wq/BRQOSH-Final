using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل الخطوط متناسقة مع الخلفية؟») مطبوعاتُ برقوش: كلُّ لونِ نصٍّ على كلِّ خلفيةٍ يقع عليها في المستند
/// بتباينٍ لا يقلّ عن 4.5 (WCAG AA) — وبه تبقى مقروءةً على الطابعات الرماديّة وبعد التصوير.
/// </summary>
public class ThePrintPaletteIsReadableTests
{
    private static (double R, double G, double B) Rgb(string hex) =>
        (Convert.ToInt32(hex[1..3], 16) / 255.0, Convert.ToInt32(hex[3..5], 16) / 255.0, Convert.ToInt32(hex[5..7], 16) / 255.0);

    private static double Luminance(string hex)
    {
        static double Channel(double s) => s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        var (r, g, b) = Rgb(hex);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    private static double Ratio(string a, string b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private const string White = "#FFFFFF";

    [Theory]
    [InlineData(PrintTheme.Ink, White)]
    [InlineData(PrintTheme.Ink, PrintTheme.Tint)]
    [InlineData(PrintTheme.Ink, PrintTheme.Zebra)]
    [InlineData(PrintTheme.Muted, White)]
    [InlineData(PrintTheme.Muted, PrintTheme.Tint)]
    [InlineData(PrintTheme.Muted, PrintTheme.Zebra)]
    [InlineData(PrintTheme.Brand, White)]
    [InlineData(PrintTheme.Brand, PrintTheme.Tint)]
    [InlineData(PrintTheme.Brand, PrintTheme.Zebra)]
    [InlineData(White, PrintTheme.Brand)]
    public void EveryPrintedTextColour_IsReadableOnItsBackground(string text, string background)
    {
        var ratio = Ratio(text, background);

        Assert.True(ratio >= 4.5, $"{text} على {background} = {ratio:F2} (المطلوب ≥ 4.5).");
    }

    [Fact]
    public void TheAccent_IsNeverUsedAsTextOnWhite()
    {
        // ‏البرونزيُّ الفاتح خطُّ تمييزٍ فقط (نسبته على الأبيض دون 4.5 فلا يصلح لنصٍّ عاديّ) — فلا يجوز أن يحمل نصّاً. يقاس بمصدر المولِّدات.
        Assert.True(Ratio(PrintTheme.Accent, White) < 4.5, "إن صار لونُ التمييز مقروءاً فراجع هذا الحارس.");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        var reporting = Path.Combine(dir!.FullName, "src", "Brqosh.Infrastructure", "Reporting");

        foreach (var file in Directory.GetFiles(reporting, "*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("FontColor(PrintTheme.Accent)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FontColor(Accent)", source, StringComparison.Ordinal);
        }
    }
}
