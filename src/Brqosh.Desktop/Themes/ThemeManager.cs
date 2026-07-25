using System.Windows;
using System.Windows.Media;

namespace Brqosh.Desktop.Themes;

/// <summary>Luxury dark/light palettes — Cosmic Cinematic · Pearl Executive.</summary>
public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    public static void Apply(bool dark)
    {
        IsDark = dark;
        ApplyTo(System.Windows.Application.Current.Resources, dark);
        Changed?.Invoke();
    }

    /// <summary>
    /// يطبّق اللوحة على قاموس موارد بعينه — يفصل الثيم عن <c>Application.Current</c>
    /// ليصير قابلاً للقياس في اختبار بلا تشغيل التطبيق (CEEF-222).
    /// </summary>
    public static void ApplyTo(ResourceDictionary r, bool dark)
    {
        EnsureMutableBrushes(r);
        if (dark)
            ApplyLuxuryDark(r);
        else
            ApplyLuxuryLight(r);
    }

    public static void Toggle() => Apply(!IsDark);

    private static void ApplyLuxuryDark(ResourceDictionary r)
    {
        SetColor(r, "BackgroundColor", "BackgroundBrush", "#050B18");
        SetColor(r, "SurfaceColor", "SurfaceBrush", "#0B1426");
        SetColor(r, "GlassSurfaceColor", "GlassSurfaceBrush", "#142338");
        SetColor(r, "PrimaryColor", "PrimaryBrush", "#22D3EE");
        SetColor(r, "PrimaryDarkColor", "PrimaryDarkBrush", "#06B6D4");
        SetColor(r, "PrimaryLightColor", "PrimaryLightBrush", "#67E8F9");
        SetColor(r, "PrimarySoftColor", "PrimarySoftBrush", "#0E7490");
        SetColor(r, "AccentColor", "AccentBrush", "#D4AF37");
        SetColor(r, "ElectricCyanColor", "ElectricCyanBrush", "#22D3EE");
        SetColor(r, "TextPrimaryColor", "TextPrimaryBrush", "#F8FAFC");
        SetColor(r, "TextSecondaryColor", "TextSecondaryBrush", "#CBD5E1");
        SetColor(r, "TextMutedColor", "TextMutedBrush", "#94A3B8");
        SetColor(r, "BorderColor", "BorderBrush", "#1E3A5F");
        SetColor(r, "BorderLightColor", "BorderLightBrush", "#0F172A");
        SetColor(r, "StatusBarColor", "StatusBarBrush", "#030712");
        SetColor(r, "StatusBarTextColor", "StatusBarTextBrush", "#94A3B8");
        SetColor(r, "SidebarColor", "SidebarBrush", "#050B18");
        // (CEEF-222) صفوف الجداول تتبع الثيم — وإلا نصّ #F8FAFC فوق أبيض.
        SetColor(r, "RowAltColor", "RowAltBrush", "#0F1B2E");
        SetColor(r, "RowHoverColor", "RowHoverBrush", "#1B2E4A");
        UpdateSidebarGradient(r, "#050B18", "#0B1426", "#050B18");
    }

    private static void ApplyLuxuryLight(ResourceDictionary r)
    {
        SetColor(r, "BackgroundColor", "BackgroundBrush", "#F7F5F0");
        SetColor(r, "SurfaceColor", "SurfaceBrush", "#FFFFFF");
        SetColor(r, "GlassSurfaceColor", "GlassSurfaceBrush", "#F8FAFC");
        SetColor(r, "PrimaryColor", "PrimaryBrush", "#0D9488");
        SetColor(r, "PrimaryDarkColor", "PrimaryDarkBrush", "#0F766E");
        SetColor(r, "PrimaryLightColor", "PrimaryLightBrush", "#14B8A6");
        SetColor(r, "PrimarySoftColor", "PrimarySoftBrush", "#CCFBF1");
        SetColor(r, "AccentColor", "AccentBrush", "#B76E79");
        SetColor(r, "ElectricCyanColor", "ElectricCyanBrush", "#0D9488");
        SetColor(r, "TextPrimaryColor", "TextPrimaryBrush", "#1C1917");
        SetColor(r, "TextSecondaryColor", "TextSecondaryBrush", "#44403C");
        SetColor(r, "TextMutedColor", "TextMutedBrush", "#57534E");
        SetColor(r, "BorderColor", "BorderBrush", "#E7E5E4");
        SetColor(r, "BorderLightColor", "BorderLightBrush", "#F5F5F4");
        SetColor(r, "StatusBarColor", "StatusBarBrush", "#1C1917");
        SetColor(r, "StatusBarTextColor", "StatusBarTextBrush", "#D6D3D1");
        SetColor(r, "SidebarColor", "SidebarBrush", "#1C1917");
        SetColor(r, "RowAltColor", "RowAltBrush", "#FAF9F7");
        SetColor(r, "RowHoverColor", "RowHoverBrush", "#F0EEEA");
        UpdateSidebarGradient(r, "#1C1917", "#292524", "#1C1917");
    }

    /// <summary>يحدّث لون الفرشاة المشتركة — كل العناصر المرتبطة تتحدّث فوراً.</summary>
    private static void SetColor(ResourceDictionary r, string colorKey, string brushKey, string hex)
    {
        var color = ParseColor(hex);
        r[colorKey] = color;
        if (r[brushKey] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
                r[brushKey] = new SolidColorBrush(color);
            else
                brush.Color = color;
        }
        else
            r[brushKey] = new SolidColorBrush(color);
    }

    private static void UpdateSidebarGradient(ResourceDictionary r, string top, string mid, string bottom)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };
        brush.GradientStops.Add(new GradientStop(ParseColor(top), 0));
        brush.GradientStops.Add(new GradientStop(ParseColor(mid), 0.55));
        brush.GradientStops.Add(new GradientStop(ParseColor(bottom), 1));
        r["SidebarGradientBrush"] = brush;
    }

    private static Color ParseColor(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;

    /// <summary>
    /// (CEEF-217) يضمن أن فُرش الثيم قابلة للتعديل في مكانها.
    ///
    /// فرشاة XAML في قاموس موارد تُجمَّد عادةً، فيضطر <see cref="SetColor"/> إلى
    /// **استبدالها** بنسخة جديدة — وكل عنصر التقط النسخة القديمة (عبر FindResource)
    /// يبقى بلونها إلى الأبد. أثره مقيس بلقطة شاشة: نصّ لوحة المساعد ظلّ بألوان
    /// الثيم الداكن (#F8FAFC و#CBD5E1) فوق خلفية فاتحة، بتباين 1.05 و1.48 مقابل
    /// 4.5 المطلوبة — نصّ يكاد لا يُرى، وهو مصدر انطباع «الواجهة مبهمة».
    ///
    /// بإذابة التجميد مرة واحدة قبل أول رسم، تصير كل التبديلات تعديلاً في المكان
    /// فيتتبّعها كل عنصر تلقائياً — دون تعديل عشرات مواضع الاستخدام.
    /// </summary>
    private static void EnsureMutableBrushes(ResourceDictionary r)
    {
        // بلا علَم ساكن: المرور مُبطِل لذاته (بعد أول مرّة لا فرشاة مجمّدة)، والعلَم
        // كان يمنع تحرير قاموس ثانٍ — وهو ما يحتاجه الاختبار.
        foreach (var key in r.Keys.Cast<object>().ToList())
        {
            if (r[key] is SolidColorBrush { IsFrozen: true } frozen)
                r[key] = new SolidColorBrush(frozen.Color);
        }
    }
}
