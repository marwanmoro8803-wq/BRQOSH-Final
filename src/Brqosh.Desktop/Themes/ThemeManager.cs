using System.Windows;
using System.Windows.Media;

namespace Brqosh.Desktop.Themes;

/// <summary>لوحتا «مِسطرة»: ورقٌ وحبرٌ وختم — فاتحةٌ وداكنة.</summary>
public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    private static bool _watchingHighContrast;

    public static void Apply(bool dark)
    {
        IsDark = dark;
        ApplyTo(System.Windows.Application.Current.Resources, dark, SystemParameters.HighContrast);
        Changed?.Invoke();
        WatchHighContrast();
    }

    /// <summary>من يشغّل «التباين العالي» أو يطفئه والبرنامج مفتوحٌ يرى الأثرَ فوراً بلا إعادة تشغيل.</summary>
    private static void WatchHighContrast()
    {
        if (_watchingHighContrast)
            return;
        _watchingHighContrast = true;
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SystemParameters.HighContrast))
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(IsDark)));
        };
    }

    /// <summary>
    /// (إمكانيّة الوصول) في «التباين العالي» تُستبدل ألوانُ اللوحة كلُّها بألوان النظام التي اختارها المستخدم (<c>Window</c> · <c>WindowText</c> · <c>Highlight</c> ·
    /// <c>GrayText</c>): الأزواجُ الأمامية/الخلفية تبقى من أزواج النظام المضمونة التباين، والدلالةُ (نجاحٌ/خطر) لا تُحمل باللون وحده بل بنصّها.
    /// </summary>
    public static void ApplyHighContrast(ResourceDictionary r)
    {
        EnsureMutableBrushes(r);
        var window = SystemColors.WindowColor;
        var text = SystemColors.WindowTextColor;
        var hi = SystemColors.HighlightColor;
        var onHi = SystemColors.HighlightTextColor;
        var gray = SystemColors.GrayTextColor;

        foreach (var k in new[] { "Background", "Surface", "GlassSurface", "PrimarySoft", "RowAlt", "RowHover", "StatusBar", "Sidebar", "SidebarMid", "SidebarHover",
                                  "SuccessSoft", "DangerSoft", "WarningSoft", "InfoSoft", "DisabledBackground" })
            SetColor(r, k + "Color", k + "Brush", window);
        foreach (var k in new[] { "TextPrimary", "TextSecondary", "StatusBarText", "SidebarText", "Border", "BorderLight", "RuleStrong", "ScrollThumb", "Success", "Danger", "Warning", "Info", "SuccessDark",
                                  "AccentInk" })
            SetColor(r, k + "Color", k + "Brush", text);
        foreach (var k in new[] { "TextMuted", "SidebarMuted", "DisabledForeground" })
            SetColor(r, k + "Color", k + "Brush", gray);
        foreach (var k in new[] { "Primary", "PrimaryDark", "PrimaryLight", "Accent", "AccentDark", "ElectricCyan" })
            SetColor(r, k + "Color", k + "Brush", hi);
        SetColor(r, "OnPrimaryColor", "OnPrimaryBrush", onHi);
        UpdateSidebarGradient(r, window, window, window);
    }

    /// <summary>
    /// يطبّق اللوحة على قاموس موارد بعينه — يفصل الثيم عن <c>Application.Current</c>
    /// ليصير قابلاً للقياس في اختبار بلا تشغيل التطبيق (CEEF-222).
    /// </summary>
    public static void ApplyTo(ResourceDictionary r, bool dark, bool highContrast = false)
    {
        EnsureMutableBrushes(r);
        if (dark)
            ApplyLuxuryDark(r);
        else
            ApplyLuxuryLight(r);
        if (highContrast)
            ApplyHighContrast(r);
    }

    public static void Toggle() => Apply(!IsDark);

    /// <summary>«مِسطرة» الداكنة — حبرُ الليل على ورقٍ كحليّ؛ القيمُ من مواصفة التصميم وتحرسها اختباراتُ التباين.</summary>
    private static void ApplyLuxuryDark(ResourceDictionary r)
    {
        SetColor(r, "BackgroundColor", "BackgroundBrush", "#14110D");
        SetColor(r, "SurfaceColor", "SurfaceBrush", "#1D1913");
        SetColor(r, "GlassSurfaceColor", "GlassSurfaceBrush", "#262018");
        SetColor(r, "PrimaryColor", "PrimaryBrush", "#D9A566");
        SetColor(r, "PrimaryDarkColor", "PrimaryDarkBrush", "#E8BE86");
        SetColor(r, "PrimaryLightColor", "PrimaryLightBrush", "#B07A3B");
        SetColor(r, "OnPrimaryColor", "OnPrimaryBrush", "#14110D");
        SetColor(r, "PrimarySoftColor", "PrimarySoftBrush", "#3A2B1A");
        SetColor(r, "AccentColor", "AccentBrush", "#E0B070");
        SetColor(r, "AccentDarkColor", "AccentDarkBrush", "#EBC58E");
        SetColor(r, "AccentInkColor", "AccentInkBrush", "#E0B070");
        SetColor(r, "ElectricCyanColor", "ElectricCyanBrush", "#D9A566");
        SetColor(r, "TextPrimaryColor", "TextPrimaryBrush", "#EEE8DA");
        SetColor(r, "TextSecondaryColor", "TextSecondaryBrush", "#C9C0AE");
        SetColor(r, "TextMutedColor", "TextMutedBrush", "#A39C8C");
        SetColor(r, "BorderColor", "BorderBrush", "#3A3024");
        SetColor(r, "BorderLightColor", "BorderLightBrush", "#2A2319");
        SetColor(r, "RuleStrongColor", "RuleStrongBrush", "#6E6455");
        SetColor(r, "StatusBarColor", "StatusBarBrush", "#0D0B08");
        SetColor(r, "StatusBarTextColor", "StatusBarTextBrush", "#BDB3A2");
        SetColor(r, "SidebarColor", "SidebarBrush", "#0D0B08");
        SetColor(r, "SidebarMidColor", "SidebarMidBrush", "#0D0B08");
        SetColor(r, "SidebarHoverColor", "SidebarHoverBrush", "#1D1710");
        SetColor(r, "SidebarTextColor", "SidebarTextBrush", "#E9DCC9");
        SetColor(r, "SidebarMutedColor", "SidebarMutedBrush", "#BDA98C");
        SetColor(r, "RowAltColor", "RowAltBrush", "#19150F");
        SetColor(r, "RowHoverColor", "RowHoverBrush", "#2A2318");
        SetColor(r, "SuccessColor", "SuccessBrush", "#6FBF8E");
        SetColor(r, "DangerColor", "DangerBrush", "#E8907F");
        SetColor(r, "WarningColor", "WarningBrush", "#E0B24F");
        SetColor(r, "InfoColor", "InfoBrush", "#86B2D6");
        SetColor(r, "SuccessSoftColor", "SuccessSoftBrush", "#1F2E24");
        SetColor(r, "DangerSoftColor", "DangerSoftBrush", "#3A2220");
        SetColor(r, "WarningSoftColor", "WarningSoftBrush", "#38301B");
        SetColor(r, "InfoSoftColor", "InfoSoftBrush", "#1E2B36");
        SetColor(r, "SuccessDarkColor", "SuccessDarkBrush", "#8BD0A6");
        SetColor(r, "DisabledBackgroundColor", "DisabledBackgroundBrush", "#26211A");
        SetColor(r, "DisabledForegroundColor", "DisabledForegroundBrush", "#7C7364");
        SetColor(r, "ScrollThumbColor", "ScrollThumbBrush", "#4A4030");
        UpdateSidebarGradient(r, "#0D0B08", "#0D0B08", "#0D0B08");   // ‏مصمَتٌ: لا تدرّج
    }

    /// <summary>«مِسطرة» الفاتحة — ورقٌ دافئ وحبرٌ داكن؛ البنّيّ البرونزيّ #6B4420 (حبر) و#B07A3B (ختم) من هويّة المالك وكما في المطبوعات.</summary>
    private static void ApplyLuxuryLight(ResourceDictionary r)
    {
        SetColor(r, "BackgroundColor", "BackgroundBrush", "#F7F3EA");
        SetColor(r, "SurfaceColor", "SurfaceBrush", "#FFFDF8");
        SetColor(r, "GlassSurfaceColor", "GlassSurfaceBrush", "#EFEADF");
        SetColor(r, "PrimaryColor", "PrimaryBrush", "#6B4420");
        SetColor(r, "PrimaryDarkColor", "PrimaryDarkBrush", "#4E3016");
        SetColor(r, "PrimaryLightColor", "PrimaryLightBrush", "#8B5A2B");
        SetColor(r, "OnPrimaryColor", "OnPrimaryBrush", "#FFFFFF");
        SetColor(r, "PrimarySoftColor", "PrimarySoftBrush", "#EFE3D2");
        SetColor(r, "AccentColor", "AccentBrush", "#B07A3B");
        SetColor(r, "AccentDarkColor", "AccentDarkBrush", "#8B5A2B");
        SetColor(r, "AccentInkColor", "AccentInkBrush", "#7A4F23");
        SetColor(r, "ElectricCyanColor", "ElectricCyanBrush", "#6B4420");
        SetColor(r, "TextPrimaryColor", "TextPrimaryBrush", "#141413");
        SetColor(r, "TextSecondaryColor", "TextSecondaryBrush", "#3E382F");
        SetColor(r, "TextMutedColor", "TextMutedBrush", "#5A5348");
        SetColor(r, "BorderColor", "BorderBrush", "#DDD6C6");
        SetColor(r, "BorderLightColor", "BorderLightBrush", "#ECE6D8");
        SetColor(r, "RuleStrongColor", "RuleStrongBrush", "#8F8676");
        SetColor(r, "StatusBarColor", "StatusBarBrush", "#ECE6D9");
        SetColor(r, "StatusBarTextColor", "StatusBarTextBrush", "#3E382F");
        SetColor(r, "SidebarColor", "SidebarBrush", "#2B1E12");
        SetColor(r, "SidebarMidColor", "SidebarMidBrush", "#2B1E12");
        SetColor(r, "SidebarHoverColor", "SidebarHoverBrush", "#3A2A1A");
        SetColor(r, "SidebarTextColor", "SidebarTextBrush", "#E9DCC9");
        SetColor(r, "SidebarMutedColor", "SidebarMutedBrush", "#BDA98C");
        SetColor(r, "RowAltColor", "RowAltBrush", "#F9F6EF");
        SetColor(r, "RowHoverColor", "RowHoverBrush", "#F0E6D6");
        SetColor(r, "SuccessColor", "SuccessBrush", "#2E6B45");
        SetColor(r, "DangerColor", "DangerBrush", "#B3261E");
        SetColor(r, "WarningColor", "WarningBrush", "#8C5A0B");
        SetColor(r, "InfoColor", "InfoBrush", "#2F5F86");
        SetColor(r, "SuccessSoftColor", "SuccessSoftBrush", "#E4EEDF");
        SetColor(r, "DangerSoftColor", "DangerSoftBrush", "#F6E1DC");
        SetColor(r, "WarningSoftColor", "WarningSoftBrush", "#F5E6C4");
        SetColor(r, "InfoSoftColor", "InfoSoftBrush", "#DFE9F0");
        SetColor(r, "SuccessDarkColor", "SuccessDarkBrush", "#25573A");
        SetColor(r, "DisabledBackgroundColor", "DisabledBackgroundBrush", "#EAE3D3");
        SetColor(r, "DisabledForegroundColor", "DisabledForegroundBrush", "#8A8171");
        SetColor(r, "ScrollThumbColor", "ScrollThumbBrush", "#CFC5B0");
        UpdateSidebarGradient(r, "#2B1E12", "#2B1E12", "#2B1E12");   // ‏مصمَتٌ: لا تدرّج
    }

    /// <summary>يحدّث لون الفرشاة المشتركة — كل العناصر المرتبطة تتحدّث فوراً.</summary>
    private static void SetColor(ResourceDictionary r, string colorKey, string brushKey, string hex) =>
        SetColor(r, colorKey, brushKey, ParseColor(hex));

    private static void SetColor(ResourceDictionary r, string colorKey, string brushKey, Color color)
    {
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

    private static void UpdateSidebarGradient(ResourceDictionary r, string top, string mid, string bottom) =>
        UpdateSidebarGradient(r, ParseColor(top), ParseColor(mid), ParseColor(bottom));

    private static void UpdateSidebarGradient(ResourceDictionary r, Color top, Color mid, Color bottom)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };
        brush.GradientStops.Add(new GradientStop(top, 0));
        brush.GradientStops.Add(new GradientStop(mid, 0.55));
        brush.GradientStops.Add(new GradientStop(bottom, 1));
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
