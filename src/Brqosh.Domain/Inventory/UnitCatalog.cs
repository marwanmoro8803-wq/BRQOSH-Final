using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Inventory;

/// <summary>قالبُ وحدةٍ يُبذر لكلّ منشأة.</summary>
public record UnitSeed(string Code, string NameAr, string UnCode, string? BaseCode = null,
    decimal Factor = 1m, params string[] Aliases);

/// <summary>
/// <b>قالبُ الوحدات المبذور — وجسرٌ إلى النصّ الحرّ القديم.</b>
///
/// <para><b>ولماذا المرادفات:</b> كلُّ صنفٍ قائمٍ في كلّ قاعدةِ عميلٍ يحمل وحدتَه
/// <b>نصّاً عربياً حرّاً</b>. وترحيلُ ذلك برمزٍ يحتاج جدولَ ترجمة، وإلا فقدت الأصنافُ
/// القائمةُ وحداتِها في يوم الترقية. فالمرادفاتُ هي ذلك الجسر: «كجم» و«كيلو» و«كيلوجرام»
/// كلُّها <c>KG</c>.</para>
///
/// <para><b>وما لا يُعرَف يبقى كما هو</b> ويُبلَّغ للهيئة <c>PCE</c> — <b>ويُعلَن أنّه
/// افتراضٌ لا معرفة</b>. فالمنشأةُ تُصحّحه بربط الصنف بوحدةٍ من القالب.</para>
/// </summary>
public static class UnitCatalog
{
    /// <summary>رمزُ UN/ECE حين يتعذّر التعرّف — والقياسُ يُعلن عددَ ما تعذّر.</summary>
    public const string FallbackUnCode = "PCE";

    public static IReadOnlyList<UnitSeed> Template { get; } =
    [
        // ── أسرةُ العدّ ──
        new("PCE", "قطعة", "PCE", null, 1m, "قطعة", "حبة", "حبّة", "عدد", "وحدة", "pcs", "piece"),
        new("DOZ", "دزينة", "DZN", "PCE", 12m, "دزينة", "درزن"),
        new("BOX", "علبة", "XBX", "PCE", 12m, "علبة", "علبه", "صندوق", "كرتون", "كرتونة", "box", "carton"),
        new("PKT", "عبوة", "XPK", "PCE", 1m, "عبوة", "باكيت", "طرد"),

        // ── أسرةُ الكتلة ──
        new("KG", "كيلوجرام", "KGM", null, 1m, "كجم", "كيلو", "كيلوجرام", "كيلوغرام", "kg"),
        new("G", "جرام", "GRM", "KG", 0.001m, "جم", "جرام", "غرام", "g"),
        new("TON", "طن", "TNE", "KG", 1000m, "طن", "ton"),

        // ── أسرةُ الحجم ──
        new("L", "لتر", "LTR", null, 1m, "لتر", "لترات", "l", "liter"),
        new("ML", "مليلتر", "MLT", "L", 0.001m, "مل", "مليلتر"),

        // ── أسرةُ الطول والمساحة والحجم الهندسيّ ──
        new("M", "متر", "MTR", null, 1m, "متر", "م", "m"),
        new("CM", "سنتيمتر", "CMT", "M", 0.01m, "سم", "سنتيمتر"),
        new("M2", "متر مربع", "MTK", null, 1m, "م2", "م²", "متر مربع", "متر مربّع"),
        new("M3", "متر مكعب", "MTQ", null, 1m, "م3", "م³", "متر مكعب", "متر مكعّب"),

        // ── أسرةُ الوقت — والخدماتُ تُفوتَر بها ──
        new("HUR", "ساعة", "HUR", null, 1m, "ساعة", "ساعه", "hour"),
        new("DAY", "يوم", "DAY", null, 1m, "يوم", "أيام"),
        new("MON", "شهر", "MON", null, 1m, "شهر", "شهور", "أشهر"),
    ];

    /// <summary>
    /// يُطابق نصّاً حرّاً برمزِ وحدة — بالرمز أوّلاً ثمّ بالاسم ثمّ بالمرادفات.
    ///
    /// <para><b>والمطابقةُ تتجاهل التشكيلَ والمسافاتِ الزائدة</b>، لأنّ النصَّ الحرّ
    /// كُتب بيدِ مستخدمٍ لا بيدِ مبرمج.</para>
    /// </summary>
    public static UnitSeed? Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var needle = Normalize(text);
        foreach (var unit in Template)
        {
            if (Normalize(unit.Code) == needle || Normalize(unit.NameAr) == needle) return unit;
            if (unit.Aliases.Any(a => Normalize(a) == needle)) return unit;
        }

        return null;
    }

    /// <summary>رمزُ UN/ECE لنصٍّ حرّ — و<see cref="FallbackUnCode"/> حين يتعذّر التعرّف.</summary>
    public static string UnCodeFor(string? text) => Match(text)?.UnCode ?? FallbackUnCode;

    /// <summary>
    /// معاملُ التحويل من وحدةٍ إلى أخرى داخل الأسرة نفسِها.
    ///
    /// <para><b>ويرمي عبر الأسر</b>: الكيلوجرامُ لا يصير متراً. وتحويلٌ صامتٌ بين أسرتين
    /// يُنتج رصيداً خاطئاً لا يكشفه شيء — <b>فالرمي هنا حمايةٌ لا تشدّد</b>.</para>
    /// </summary>
    public static decimal ConversionFactor(UnitSeed from, UnitSeed to)
    {
        var fromBase = from.BaseCode ?? from.Code;
        var toBase = to.BaseCode ?? to.Code;

        if (fromBase != toBase)
            throw new AccountingException(
                $"لا تحويلَ بين «{from.NameAr}» و«{to.NameAr}» — وحدتان من أسرتين مختلفتين.");

        return from.Factor / to.Factor;
    }

    private static string Normalize(string text)
    {
        Span<char> buffer = stackalloc char[text.Length];
        var length = 0;

        foreach (var ch in text)
        {
            // ‏التشكيلُ يُسقَط، والمسافاتُ تُوحَّد، والعربيةُ تُطبَّع في صورةٍ واحدة.
            if (ch is >= 'ً' and <= 'ْ') continue;
            if (char.IsWhiteSpace(ch)) continue;

            buffer[length++] = ch switch
            {
                'أ' or 'إ' or 'آ' => 'ا',
                'ة' => 'ه',
                'ى' => 'ي',
                _ => char.ToLowerInvariant(ch),
            };
        }

        return new string(buffer[..length]);
    }
}
