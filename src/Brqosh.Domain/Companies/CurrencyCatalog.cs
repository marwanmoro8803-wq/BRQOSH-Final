namespace Brqosh.Domain.Companies;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · العملة) <b>العملات المعروفة (ISO 4217) وما يُكتب بجوار المبلغ.</b>
///
/// <para><b>ما كان:</b> عملةُ المنشأة حقلُ نصٍّ يقبل أيَّ ثلاثة أحرف («ABC» مقبولة)، والشاشاتُ تكتب «ر.س» في الشيفرة
/// أياً كانت العملة المختارة — فمنشأةٌ بالدولار كانت ترى مبالغَها بالريال. <b>والآن</b> القائمةُ معروفةٌ تُرفض خارجَها،
/// ويُشتقّ الرمزُ المكتوبُ من العملة نفسِها.</para>
/// </summary>
public static partial class CurrencyCatalog
{
    /// <summary>رموزُ ISO 4217 الجاريةُ — ما ليس فيها لا يُقبل عملةً لمنشأة.</summary>
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN", "BHD", "BIF",
        "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHF", "CLP", "CNY", "COP", "CRC",
        "CUP", "CVE", "CZK", "DJF", "DKK", "DOP", "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "GBP", "GEL", "GHS",
        "GIP", "GMD", "GNF", "GTQ", "GYD", "HKD", "HNL", "HTG", "HUF", "IDR", "ILS", "INR", "IQD", "IRR", "ISK", "JMD",
        "JOD", "JPY", "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR", "LRD", "LSL",
        "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MYR", "MZN", "NAD",
        "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG", "QAR", "RON", "RSD",
        "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SOS", "SRD", "SSP", "STN", "SVC", "SYP",
        "SZL", "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS", "VES",
        "VND", "VUV", "WST", "XAF", "XCD", "XOF", "XPF", "YER", "ZAR", "ZMW", "ZWG",
    };

    /// <summary>الرمزُ المكتوبُ بالعربية بجوار المبلغ — وما لا رمزَ مختصراً له يُكتب برمزه (ISO).</summary>
    private static readonly Dictionary<string, string> ArabicShort = new(StringComparer.Ordinal)
    {
        ["SAR"] = "ر.س", ["AED"] = "د.إ", ["KWD"] = "د.ك", ["BHD"] = "د.ب", ["QAR"] = "ر.ق", ["OMR"] = "ر.ع",
        ["EGP"] = "ج.م", ["JOD"] = "د.أ", ["IQD"] = "د.ع", ["LBP"] = "ل.ل", ["SYP"] = "ل.س", ["YER"] = "ر.ي",
        ["LYD"] = "د.ل", ["TND"] = "د.ت", ["DZD"] = "د.ج", ["MAD"] = "د.م", ["SDG"] = "ج.س",
        ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£",
    };

    /// <summary>الأسماءُ المعروضةُ في قائمة الاختيار — العملاتُ الأكثرُ استعمالاً أولاً.</summary>
    public static IReadOnlyList<string> Codes { get; } =
        new[] { "SAR", "AED", "KWD", "BHD", "QAR", "OMR", "EGP", "JOD", "USD", "EUR", "GBP" }
            .Concat(Known.Except(["SAR", "AED", "KWD", "BHD", "QAR", "OMR", "EGP", "JOD", "USD", "EUR", "GBP"])
                .OrderBy(c => c, StringComparer.Ordinal))
            .ToList();

    /// <summary>رمزُ الدولة (ISO 3166) الممثِّلة للعملة — فارغٌ لما ليس في الكتالوج. اليورو «EU».</summary>
    public static string CountryCode(string? code) =>
        code is not null && Countries.TryGetValue(code.Trim().ToUpperInvariant(), out var country) ? country : "";

    public static bool IsKnown(string? code) =>
        code is not null && Known.Contains(code.Trim().ToUpperInvariant());

    /// <summary>
    /// ما يُكتب بجوار المبلغ: بالعربية رمزُها المختصر (ر.س)، وبالإنجليزية رمزُ ISO (SAR).
    /// عملةٌ فارغةٌ أو مجهولة ⟵ الريال، وهو أصلُ المنشآت.
    /// </summary>
    public static string Short(string? code, bool english)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        if (normalized.Length == 0)
            normalized = "SAR";
        if (english)
            return normalized;
        return ArabicShort.TryGetValue(normalized, out var symbol) ? symbol : normalized;
    }
}
