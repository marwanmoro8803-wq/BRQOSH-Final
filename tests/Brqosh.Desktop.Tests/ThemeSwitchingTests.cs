using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-217) الثيم يجب أن يتبدّل فعلياً — لا أن يتبدّل نصفه.
///
/// كان قاموس الثيم يربط كل فُرَش الأنماط بـ StaticResource: تُحلّ **مرة واحدة عند
/// تحليل XAML** ولا تتبع تبديل الثيم. والتطبيق يبدأ بـ ThemeManager.Apply(true)،
/// فالنصوص المبنيّة برمجياً (FindResource وقت الإنشاء) تأخذ ألوان الثيم الداكن
/// بينما الخلفيات تبقى على قيم XAML الفاتحة.
///
/// النتيجة المقيسة بلقطة شاشة: نصّ لوحة المساعد #F8FAFC فوق بطاقة #FFFFFF —
/// تباين 1.05 مقابل 4.5 المطلوبة. نصّ يكاد لا يُرى، وهو مصدر انطباع «الواجهة مبهمة».
/// بعد التحويل إلى DynamicResource صار 17.57.
/// </summary>
public class ThemeSwitchingTests
{
    private static string ThemePath()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Brqosh.Desktop", "Themes", "BrqoshTheme.xaml"));
        Assert.True(File.Exists(path), $"Theme not found: {path}");
        return path;
    }

    /// <summary>لا فرشاة في Setter مربوطة بـ StaticResource — وإلا تجمّدت على ثيم واحد.</summary>
    [Fact]
    public void ThemeBrushes_UseDynamicResource_NotStatic()
    {
        var xaml = File.ReadAllText(ThemePath());

        var frozen = Regex.Matches(xaml, @"Value=""\{StaticResource ([A-Za-z]*Brush)\}""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(frozen.Count == 0,
            "فُرَش مربوطة بـ StaticResource لا تتبع تبديل الثيم:\n" +
            string.Join("\n", frozen.Select(f => "  - " + f)));
    }

    /// <summary>وحارس للحارس: التحويل تمّ فعلاً ولم يُفرّغ الملف من الروابط.</summary>
    [Fact]
    public void ThemeBrushes_AreActuallyBound()
    {
        var xaml = File.ReadAllText(ThemePath());
        var dynamic = Regex.Matches(xaml, @"Value=""\{DynamicResource [A-Za-z]*Brush\}""").Count;

        Assert.True(dynamic >= 80,
            $"عدد الروابط الحيّة {dynamic} — يُتوقع 80+ (كان 83 عند الإصلاح).");
    }

    /// <summary>
    /// تعريفات الفُرَش نفسها تبقى StaticResource على الألوان — ThemeManager يعدّل
    /// لون الفرشاة في مكانها، فالرباط الحيّ هنا لا لزوم له ويكلّف أداءً.
    /// </summary>
    [Fact]
    public void BrushDefinitions_StillReferenceColorsStatically()
    {
        var xaml = File.ReadAllText(ThemePath());
        var definitions = Regex.Matches(xaml, @"<SolidColorBrush[^>]*Color=""\{StaticResource [A-Za-z]*Color\}""").Count;

        Assert.True(definitions > 0, "تعريفات الفُرَش اختفت — راجع التحويل.");
    }
}
