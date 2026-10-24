using System.Windows;
using System.Windows.Media;

namespace Brqosh.Desktop.Themes;

/// <summary>لوحتا «مِسطرة»: ورقٌ وحبرٌ وختم — فاتحةٌ وداكنة.</summary>
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

    /// <summary>«مِسطرة» الداكنة — حبرُ الليل على ورقٍ كحليّ؛ القيمُ من مواصفة التصميم وتحرسها اختباراتُ التباين.</summary>
    private static void ApplyLuxuryDark(ResourceDictionary r)
    {
        SetColor(r, "BackgroundColor", "BackgroundBrush", "#0A141D");
        SetColor(r, "SurfaceColor", "SurfaceBrush", "#101C27");
        SetColor(r, "GlassSurfaceColor", "GlassSurfaceBrush", "#162433");
        SetColor(r, "PrimaryColor", "PrimaryBrush", "#86B2D6");
        SetColor(r, "PrimaryDarkColor", "PrimaryDarkBrush", "#A9CBE6");
        SetColor(r, "PrimaryLightColor", "PrimaryLightBrush", "#5E8FB8");
        SetColor(r, "OnPrimaryColor", "OnPrimaryBrush", "#0A141D");
        SetColor(r, "PrimarySoftColor", "PrimarySoftBrush", "#1E3550");
        SetColor(r, "AccentColor", "AccentBrush", "#D9B54A");
        SetColor(r, "AccentDarkColor", "AccentDarkBrush", "#E6C565");
        SetColor(r, "AccentInkColor", "AccentInkBrush", "#D9B54A");
        SetColor(r, "ElectricCyanColor", "ElectricCyanBrush", "#86B2D6");
        SetColor(r, "TextPrimaryColor", "TextPrimaryBrush", "#EEE8DA");
        SetColor(r, "TextSecondaryColor", "TextSecondaryBrush", "#C9C0AE");
        SetColor(r, "TextMutedColor", "TextMutedBrush", "#A39C8C");
        SetColor(r, "BorderColor", "BorderBrush", "#24384C");
        SetColor(r, "BorderLightColor", "BorderLightBrush", "#172634");
        SetColor(r, "RuleStrongColor", "RuleStrongBrush", "#5E6B78");
        SetColor(r, "StatusBarColor", "StatusBarBrush", "#060E15");
        SetColor(r, "StatusBarTextColor", "StatusBarTextBrush", "#A7B4C1");
        SetColor(r, "SidebarColor", "SidebarBrush", "#060E15");
        SetColor(r, "SidebarMidColor", "SidebarMidBrush", "#060E15");
        SetColor(r, "SidebarHoverColor", "SidebarHoverBrush", "#0F1F2D");
        SetColor(r, "SidebarTextColor", "SidebarTextBrush", "#C9D6E2");
        SetColor(r, "SidebarMutedColor", "SidebarMutedBrush", "#9FB3C6");
        SetColor(r, "RowAltColor", "RowAltBrush", "#0D1922");
        SetColor(r, "RowHoverColor", "RowHoverBrush", "#1A2B3C");
        SetColor(r, "SuccessColor", "SuccessBrush", "#6FBF8E");
        SetColor(r, "DangerColor", "DangerBrush", "#E8907F");
        SetColor(r, "WarningColor", "WarningBrush", "#E0B24F");
        SetColor(r, "InfoColor", "InfoBrush", "#86B2D6");
        UpdateSidebarGradient(r, "#060E15", "#060E15", "#060E15");   // ‏مصمَتٌ: لا تدرّج
    }

    /// <summary>«مِسطرة» الفاتحة — ورقٌ دافئ وحبرٌ أزرقُ داكن؛ الكحليّ #0F3D5E (حبر) والذهبيّ #C9A227 (ختم) كما في المطبوعات.</summary>
    private static void ApplyLuxuryLight(ResourceDictionary r)
    {
        SetColor(r, "BackgroundColor", "BackgroundBrush", "#F5F1E8");
        SetColor(r, "SurfaceColor", "SurfaceBrush", "#FFFDF8");
        SetColor(r, "GlassSurfaceColor", "GlassSurfaceBrush", "#EFEADF");
        SetColor(r, "PrimaryColor", "PrimaryBrush", "#0F3D5E");
        SetColor(r, "PrimaryDarkColor", "PrimaryDarkBrush", "#0A2C45");
        SetColor(r, "PrimaryLightColor", "PrimaryLightBrush", "#2B5F86");
        SetColor(r, "OnPrimaryColor", "OnPrimaryBrush", "#FFFFFF");
        SetColor(r, "PrimarySoftColor", "PrimarySoftBrush", "#DDE6EE");
        SetColor(r, "AccentColor", "AccentBrush", "#C9A227");
        SetColor(r, "AccentDarkColor", "AccentDarkBrush", "#A8861C");
        SetColor(r, "AccentInkColor", "AccentInkBrush", "#846510");
        SetColor(r, "ElectricCyanColor", "ElectricCyanBrush", "#0F3D5E");
        SetColor(r, "TextPrimaryColor", "TextPrimaryBrush", "#14212D");
        SetColor(r, "TextSecondaryColor", "TextSecondaryBrush", "#38444F");
        SetColor(r, "TextMutedColor", "TextMutedBrush", "#56606A");
        SetColor(r, "BorderColor", "BorderBrush", "#DDD6C6");
        SetColor(r, "BorderLightColor", "BorderLightBrush", "#ECE6D8");
        SetColor(r, "RuleStrongColor", "RuleStrongBrush", "#8F8676");
        SetColor(r, "StatusBarColor", "StatusBarBrush", "#ECE6D9");
        SetColor(r, "StatusBarTextColor", "StatusBarTextBrush", "#38444F");
        SetColor(r, "SidebarColor", "SidebarBrush", "#0B3150");
        SetColor(r, "SidebarMidColor", "SidebarMidBrush", "#0B3150");
        SetColor(r, "SidebarHoverColor", "SidebarHoverBrush", "#123E60");
        SetColor(r, "SidebarTextColor", "SidebarTextBrush", "#C9D6E2");
        SetColor(r, "SidebarMutedColor", "SidebarMutedBrush", "#9FB3C6");
        SetColor(r, "RowAltColor", "RowAltBrush", "#F9F6EF");
        SetColor(r, "RowHoverColor", "RowHoverBrush", "#E9EEF3");
        SetColor(r, "SuccessColor", "SuccessBrush", "#2E6B45");
        SetColor(r, "DangerColor", "DangerBrush", "#B3261E");
        SetColor(r, "WarningColor", "WarningBrush", "#8C5A0B");
        SetColor(r, "InfoColor", "InfoBrush", "#2F5F86");
        UpdateSidebarGradient(r, "#0B3150", "#0B3150", "#0B3150");   // ‏مصمَتٌ: لا تدرّج
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
