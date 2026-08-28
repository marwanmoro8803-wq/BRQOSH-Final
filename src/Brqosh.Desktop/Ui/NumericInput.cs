using System.Globalization;
using System.Text;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-228 · A-2) تحليل المبالغ والكميات المكتوبة بيد المستخدم.
///
/// <para><b>لماذا لا يكفي <c>decimal.TryParse(text, NumberStyles.Number, InvariantCulture, …)</c>:</b>
/// العَلَم <c>AllowThousands</c> يقبل فاصلة المجموعات <b>ولا يتحقّق من حجم المجموعة</b>.
/// مُقاس فعلياً على .NET 9:</para>
/// <code>
///   "1,5"     ← 15         (عشرة أضعاف، بصمت)
///   "1234,5"  ← 12345
///   "12,34"   ← 1234
///   "1,5,5"   ← 155
/// </code>
/// <para>ومَن يكتب الفاصلة العشرية بالطريقة الأوروبية (<c>1,5</c> يقصد واحداً ونصفاً) يُرحَّل له
/// شيكٌ بخمسة عشر. لا رسالة خطأ ولا تلميح — الحقل «صالح».</para>
///
/// <para><b>وعطلٌ في الاتجاه المعاكس:</b> الأرقام العربية-الهندية (<c>٥٠٠</c>) تفشل في التحليل
/// كليّاً على <c>InvariantCulture</c>، فمستخدمٌ يكتب بلوحة مفاتيح عربية يرى «مبلغ غير صالح»
/// على رقمٍ صحيح كتبه بلغته. وكذلك الفاصلة العشرية العربية <c>٫</c> (U+066B).</para>
///
/// <para>فالتحليل هنا: تطبيعٌ أوّلاً (أرقام · فواصل · محارف الاتجاه الملتصقة باللصق من
/// مستند عربي)، ثم تحقّقٌ صريح من <b>بنية</b> المجموعات، ثم تحليلٌ بلا <c>AllowThousands</c>.</para>
/// </summary>
public static class NumericInput
{
    /// <summary>عدد صحيح، أو مجموعات ثلاثية سليمة، يتبعه كسرٌ عشريّ اختياريّ.</summary>
    private static readonly System.Text.RegularExpressions.Regex Shape = new(
        @"^[+-]?(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d+)?$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// يحاول قراءة مبلغ/كمية. يقبل الأرقام العربية والفواصل العربية والمجموعات الثلاثية
    /// السليمة، ويرفض كل فاصلةٍ في موضع لا يصحّ فيه أن تكون فاصلة مجموعات.
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = Normalize(text);
        if (normalized.Length == 0 || !Shape.IsMatch(normalized))
            return false;

        // بعد التحقق من البنية تُحذف الفواصل ويُحلَّل بلا AllowThousands إطلاقاً —
        // فلا يبقى للعَلَم المتساهل أثر.
        return decimal.TryParse(
            normalized.Replace(",", string.Empty),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>مثل <see cref="TryParse"/> مع اشتراط أن تكون القيمة موجبة تماماً.</summary>
    public static bool TryParsePositive(string? text, out decimal value) =>
        TryParse(text, out value) && value > 0m;

    /// <summary>مثل <see cref="TryParse"/> مع اشتراط ألا تكون القيمة سالبة.</summary>
    public static bool TryParseNonNegative(string? text, out decimal value) =>
        TryParse(text, out value) && value >= 0m;

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Trim())
        {
            switch (ch)
            {
                // محارف الاتجاه والتنسيق التي تلتصق بالنصّ عند اللصق من مستند عربي
                // أو من Excel بواجهة RTL — غير مرئية، وتُفشل التحليل بلا سبب ظاهر.
                case '‎': // LRM
                case '‏': // RLM
                case '؜': // ALM
                case '⁦': // LRI
                case '⁧': // RLI
                case '⁨': // FSI
                case '⁩': // PDI
                case '​': // ZWSP
                case ' ':
                case ' ': // NBSP
                case ' ': // NNBSP
                    continue;

                case '٫': sb.Append('.'); continue; // الفاصلة العشرية العربية ٫
                case '٬': sb.Append(','); continue; // فاصلة الآلاف العربية ٬
                case '−': sb.Append('-'); continue; // علامة الطرح الرياضية −

                default:
                    if (ch >= '٠' && ch <= '٩')        // ٠-٩ عربية-هندية
                        sb.Append((char)('0' + (ch - '٠')));
                    else if (ch >= '۰' && ch <= '۹')   // ۰-۹ فارسية
                        sb.Append((char)('0' + (ch - '۰')));
                    else
                        sb.Append(ch);
                    continue;
            }
        }

        return sb.ToString();
    }
}
