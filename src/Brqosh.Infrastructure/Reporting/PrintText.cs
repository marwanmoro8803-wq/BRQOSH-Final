using System.Globalization;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// عزل النصّ اللاتينيّ/الرقميّ داخل فقرة عربية.
///
/// **العلّة المقيسة:** الكود يكتب <c>التاريخ: 2026-03-14</c> والمطبوع يعرض
/// <c>التاريخ: 14-03-2026</c> — خوارزمية ثنائية الاتجاه تُعيد ترتيب مقاطع الرقم
/// حين تقع داخل فقرة اتجاهها من اليمين. والتذييل في **الوثيقة نفسها** يعرض
/// <c>2026-07-25</c> صحيحاً لأنّه يبدأ بحرف لاتينيّ فيصير اتجاه الفقرة LTR.
/// أي: صيغتا تاريخ متناقضتان في ورقة واحدة، والقارئ يظنّ الأولى dd-MM-yyyy.
///
/// **العلاج:** محرفا العزل U+2066 (LRI) و U+2069 (PDI) حول المقطع. وهما مصمَّمان
/// لهذا بالضبط: يُثبِّتان اتجاه ما بينهما دون التأثير على بقية الفقرة، بخلاف
/// العلامات القديمة LRM التي تُصلح موضعاً وتكسر آخر.
/// </summary>
public static class PrintText
{
    private const char LeftToRightIsolate = '⁦';
    private const char PopDirectionalIsolate = '⁩';

    /// <summary>يعزل مقطعاً لاتينياً/رقمياً فلا تُعيد ثنائية الاتجاه ترتيبه.</summary>
    public static string Ltr(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : LeftToRightIsolate + value + PopDirectionalIsolate;

    /// <summary>تاريخ ISO معزول — يبقى yyyy-MM-dd كما كُتب.</summary>
    public static string Date(DateOnly value) =>
        Ltr(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <inheritdoc cref="Date(DateOnly)"/>
    public static string Date(DateTime value) =>
        Ltr(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>طابع زمنيّ معزول (تذييل الصفحات).</summary>
    public static string Timestamp(DateTime value) =>
        Ltr(value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

    /// <summary>مبلغٌ بمنزلتين وفاصل آلاف — معزولٌ فلا يعكسه المتن العربيّ، وثابتٌ مهما كانت ثقافةُ الجهاز.</summary>
    public static string Money(decimal value) =>
        Ltr(value.ToString("N2", CultureInfo.InvariantCulture));

    /// <summary>رقم مستند أو مرجع لاتينيّ (INV-2026-000001) — يُعزل كذلك.</summary>
    public static string Code(string? value) => Ltr(value);
}
