namespace Brqosh.Desktop.Ui;

/// <summary>
/// (ui-ux-i18n-printing#9) <b>تسمياتُ الحالات التي تُبنى في طبقة النطاق — بلغة الواجهة.</b>
///
/// <para>العقودُ والمشاريعُ والمستخلصاتُ وعقودُ الباطن وأوامرُ الإنتاج والشيكات تُسمّي حالاتِها
/// في <c>Brqosh.Domain</c> (<c>StatusLabel</c>) بالعربيّة — والنطاقُ لا يعرف لغةَ الواجهة ولا ينبغي.
/// فكانت الشاشاتُ الستُّ تعرضها عربيّةً بعد التبديل. والترجمةُ هنا، في الطبقة التي تعرف اللغة،
/// <b>ونصٌّ لا يعرفه الجدولُ يمرّ كما هو</b> فلا يختفي شيء.</para>
/// </summary>
public static class DomainLabels
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["مسودة"] = "Draft",
        ["نشط"] = "Active",
        ["مكتمل"] = "Completed",
        ["ملغاة"] = "Cancelled",
        ["تخطيط"] = "Planning",
        ["متوقف"] = "On hold",
        ["مرحّل"] = "Posted",
        ["صادر"] = "Released",
        ["في الحافظة"] = "In portfolio",
        ["مُودَع"] = "Deposited",
        ["مُحصَّل"] = "Cleared",
        ["مرتجع"] = "Bounced",
    };

    /// <summary>تسميةُ النطاق بلغة الواجهة.</summary>
    public static string Show(string domainLabel) =>
        LocaleManager.IsEnglish && English.TryGetValue(domainLabel, out var en) ? en : domainLabel;

    /// <summary>كلُّ تسمياتٍ يعرفها الجدول — للاختبار.</summary>
    internal static IReadOnlyCollection<string> Known => English.Keys;
}
