namespace Brqosh.Desktop.Ui;

/// <summary>
/// قواعدُ تخطيط الإطار الرئيسيّ التي تُقاس بلا نافذة.
/// </summary>
public static class ShellLayout
{
    /// <summary>عرضُ الشريط الجانبيّ ولوحة المساعد (كما في <c>MainWindow.xaml</c>).</summary>
    public const double SidebarWidth = 272;

    public const double AiPanelWidth = 300;

    /// <summary>أدنى عرضٍ للصفحة نفسِها نقبل أن يتركه المساعدُ مفتوحاً: دونه تُقصّ أشرطةُ الأدوات والجداول.</summary>
    public const double MinPageWidthWithAssistant = 900;

    /// <summary>
    /// هل يتّسع عرضُ النافذة لِلَوحة المساعد من غير أن يضيق مكانُ الصفحة؟
    /// على ١٣٦٦ فأقلّ تُطوى اللوحةُ تلقائيّاً (تبقى الصفحةُ ١٠٩٤ بدل ٧٩٤)، ويفتحها المستخدمُ بزرّها متى أراد.
    /// </summary>
    public static bool AiPanelFits(double windowWidth) =>
        windowWidth - SidebarWidth - AiPanelWidth >= MinPageWidthWithAssistant;
}
