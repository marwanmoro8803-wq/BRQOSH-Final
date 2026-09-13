using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// ‏(م-٥ · ن-٤٨) <b>توأمُ <c>NumericInput</c> في البنية التحتيّة — لنصٍّ يأتي من ملفّ
/// لا من حقلٍ يكتبه إنسان.</b>
///
/// <para><b>ولمَ لزم توأمٌ أصلاً:</b> بوّابةُ <c>NumericInputIsTheOnlyParserTests</c>
/// (م-٣ · ن-٢٩) سمَّت هذا العطلَ بعينِه — «<c>1,5</c> تُقرأ <b>15</b> بسبب
/// <c>AllowThousands</c>» — <b>ثمّ رسمت نطاقَها بيدها</b>: «سطحُ المكتب وحدَه … أمّا
/// الواجهةُ البرمجيّة فمدخلاتُها JSON مُنمَّطة». <b>وملفُّ CSV صنفٌ ثالثٌ لم يخطر لأحد</b>:
/// لا نصٌّ يُكتب في حقل، ولا JSON مُنمَّط. <b>فالعطلُ نجا في جملةِ النطاق نفسِها.</b></para>
///
/// <para><b>والقاعدةُ هنا لا تُحصي المنازل العشريّة</b>: <c>BankAccount.SetCurrency</c>
/// يقبل أيَّ ثلاثةِ حروف، والمنتجُ يشحن ملفّاتِ ضريبةٍ للبحرين والكويت وعُمان
/// (<c>VatJurisdictionProfile.cs:19-21</c>) — <b>وعملاتُها ثلاثُ منازل</b>. فأيُّ برهانٍ
/// من نوع «المنزلةُ الثالثةُ غيرُ ممثَّلة، إذن الفاصلةُ للآلاف» <b>يسقط عند أوّل حسابٍ
/// كويتيّ</b>، ويُعيد العطلَ من باب الإصلاح. <b>فالقاعدةُ أبسطُ وتُرى بالنظر:</b>
/// <c>لا يُحوَّل إلى رقمٍ أيُّ حقلٍ فيه فاصلة</c> — وما فيه التباسٌ يُرفَض باسمه.</para>
/// </summary>
public static class CsvAmountText
{
    /// <summary>‏الصيغةُ المقبولة: عددٌ صحيحٌ أو عشريٌّ بنقطة، بأيّ عددٍ من المنازل.</summary>
    private static readonly Regex Shape = new(@"^[+-]?\d+(?:\.\d+)?$", RegexOptions.Compiled);

    /// <summary>
    /// ‏خليّةُ مبلغٍ في ملفّ — <b>والفارغةُ تبقى صفراً كما كانت</b>.
    ///
    /// <para><b>وذلك قرارٌ لا إغفال:</b> كشوفُ البنوك تحمل سطورَ رصيدٍ افتتاحيٍّ ومجاميعَ
    /// بخلايا مبالغَ فارغة. وجعلُها خطأً يُحوّل ملفّاً من ١٢٠٠ سطرٍ إلى <b>رفضٍ كامل</b>،
    /// وهو انحدارٌ أسوأُ من العطل المُصلَح. <b>وأنّ الصفرَ يُسقط السطرَ صامتاً عطلٌ قائمٌ
    /// يُسجَّل بنداً مستقلّاً</b> — ولا يُصلَح من داخل ن-٤٨ بتغيير شكل النتيجة.</para>
    /// </summary>
    public static decimal ParseCell(string raw, int rowNumber, string column) =>
        string.IsNullOrWhiteSpace(raw) ? 0m : Parse(raw, rowNumber, column);

    /// <summary>‏نصٌّ لا يُقرأ رقماً — يُرفَض باسمِه وبموضعِه وبالصيغة المقبولة.</summary>
    public static decimal Parse(string raw, int rowNumber, string column)
    {
        var text = Normalize(raw);
        if (text.Length == 0)
            throw Refuse(raw, rowNumber, column, "‏الخليّةُ فارغة");

        var negative = false;
        if (text.Length > 2 && text[0] == '(' && text[^1] == ')')
        {
            // ‏السالبُ بالقوسين عرفٌ محاسبيٌّ واحدٌ في كلّ لغة، فيُعالَج صراحةً.
            // ‏**ولا يُطلَب بـ`AllowParentheses`**: ذلك العلمُ لا يُطلَب وحدَه، ويجرّ معه
            // ‏`AllowThousands` و`AllowExponent` — وهما من أدخل «1,5» ⟵ 15 و«1e3» ⟵ 1000.
            negative = true;
            text = text[1..^1].Trim();
        }

        if (!Shape.IsMatch(text))
            throw Refuse(raw, rowNumber, column, Why(text));

        // ‏علمان لا غير. كلُّ ما عداهما هو العطل.
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
            throw Refuse(raw, rowNumber, column, "‏الرقمُ خارجَ المدى");

        return negative ? -value : value;
    }

    /// <summary>‏هل النصُّ عددٌ صحيحٌ عارٍ؟ — تُستعمل لكشف حقلٍ شقّته فاصلةٌ غيرُ مُقتبَسة.</summary>
    public static bool IsBareInteger(string raw)
    {
        var t = Normalize(raw);
        return t.Length > 0 && t.Length <= 4 && t.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// ‏هل النصُّ مجموعةُ آلافٍ تالية؟ <b>ثلاثُ خاناتٍ بالضبط</b> يتبعها كسرٌ اختياريّ —
    /// وهي بصمةُ النصف الثاني من مبلغٍ شُقّ، لا بصمةُ رقمِ مرجعٍ بنكيّ.
    /// </summary>
    public static bool LooksLikeASplitThousandsGroup(string raw)
    {
        var t = Normalize(raw);
        return Regex.IsMatch(t, @"^\d{3}(?:\.\d+)?$");
    }

    /// <summary>
    /// ‏توحيدٌ قبل الفحص: تُحذف علاماتُ الاتّجاه والمسافاتُ غيرُ الفاصلة، وتُحوَّل
    /// الأرقامُ العربيّةُ والفارسيّة، والفاصلةُ العشريّةُ العربيّة <c>٫</c> إلى نقطة.
    ///
    /// <para><b>وهذا ليس تزيّناً</b>: كشفٌ يُكتب بأرقامٍ عربيّةٍ كان يُرفَض جملةً —
    /// <b>وهو عكسُ العطل الذي بُني <c>NumericInput</c> لإصلاحه</b>.</para>
    /// </summary>
    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var b = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            switch (ch)
            {
                case '‎' or '‏' or '؜' or '‪' or '‫' or '‬'
                     or '‭' or '‮' or '⁦' or '⁧' or '⁨' or '⁩':
                    continue;                       // ‏علاماتُ اتّجاهٍ غيرُ مرئيّة
                case ' ' or ' ' or ' ':
                    b.Append(' '); break;           // ‏مسافاتٌ غيرُ فاصلة
                case '٫': b.Append('.'); break;   // ‏الفاصلةُ العشريّةُ العربيّة
                case '٬': b.Append(','); break;   // ‏فاصلةُ الآلاف العربيّة
                default:
                    if (ch is >= '٠' and <= '٩') b.Append((char)('0' + (ch - '٠')));
                    else if (ch is >= '۰' and <= '۹') b.Append((char)('0' + (ch - '۰')));
                    else b.Append(ch);
                    break;
            }
        }

        return b.ToString().Trim();
    }

    private static string Why(string text) =>
        text.Contains(',', StringComparison.Ordinal)
            ? "‏فيه فاصلة، والملفُّ لا يقول أهي عشريّةٌ أم للآلاف"
            : text.Contains('e', StringComparison.OrdinalIgnoreCase)
                ? "‏صيغةٌ أُسّيّة"
                : "‏ليس رقماً";

    private static AccountingException Refuse(string raw, int rowNumber, string column, string why) =>
        new($"‏مبلغٌ غير مقبول في السطر {rowNumber} · العمود «{column}»: «{raw}» — {why}. "
            + "‏الصيغةُ المقبولة رقمٌ بنقطةٍ عشريّةٍ بلا فواصلِ آلاف، مثل 1234.56 أو -50 أو (500).");
}
