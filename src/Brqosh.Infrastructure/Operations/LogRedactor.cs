using System.Text.RegularExpressions;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (جاهزيّةُ الإنتاج · F) <b>ما يُكتب في سجلّ الأعطال ويُعرض في حوار الخطأ يمرّ من هنا أوّلاً.</b>
///
/// <para><b>ما كان:</b> <see cref="CrashLog"/> يكتب <c>Message</c> كلَّ استثناءٍ في السلسلة حرفاً بحرف، والحوارُ يعرضه.
/// ورسائلُ الاستثناء لا تملك المستودعُ نصَّها: مكتبةُ اتّصالٍ تقتبس سلسلةَ الاتّصال بكلمة مرورها، وردُّ HTTP يُلصق ترويسةَ
/// <c>Authorization</c>، واستثناءُ تحليلٍ يقتبس مقطعاً من مفتاح PEM أو من حمولةٍ فيها <c>binarySecurityToken</c> أو
/// <c>secret</c> الهيئة. <b>والسجلُّ يُرسَل إلى الدعم بالبريد</b> — فسرٌّ فيه سرٌّ خرج من الجهاز.</para>
///
/// <para><b>ما يُحجب:</b> قيمةُ كلّ مفتاحٍ سرّيِّ الاسم في صيغة <c>key=value</c> أو <c>"key":"value"</c>، ورمزا
/// <c>Bearer</c>/<c>Basic</c>، وكتلةُ مفتاحٍ خاصٍّ PEM كاملة. <b>ويبقى اسمُ المفتاح</b> — فالدعمُ يعرف أيَّ سرٍّ كان في الرسالة
/// دون أن يراه. ولا يُحجب غيرُ ذلك: رسالةٌ عربيّةٌ تقول للمستخدم ما وقع تبقى كما هي.</para>
///
/// <para><b>ولا يرمي أبداً</b> — يجري داخل معالج الاستثناء، وفشلُه يُعيد النصَّ محجوباً كلُّه لا الأصل.</para>
/// </summary>
public static class LogRedactor
{
    /// <summary>ما يحلّ محلّ السرّ.</summary>
    public const string Mask = "***";

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(250);

    // ‏أسماءُ المفاتيح السرّيّة — بعد «user» أو «client» أو فاصلٍ اختياريّ.
    private const string SecretKey =
        @"(?:user[ _-]?)?password|pwd|passwd|pass[ _-]?phrase|(?:client[ _-]?)?secret|api[ _-]?key|access[ _-]?token|refresh[ _-]?token|auth[ _-]?token|token|binary[ _-]?security[ _-]?token|private[ _-]?key|otp|totp[ _-]?secret";

    // ‏"key": "value" — صيغةُ JSON.
    private static readonly Regex JsonPair = new(
        "(\"(?:" + SecretKey + ")\"\\s*:\\s*\")([^\"]*)(\")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Budget);

    // ‏key=value حتى الفاصلة المنقوطة أو المسافة أو علامة الاقتباس — صيغةُ سلسلة الاتّصال والاستعلام.
    private static readonly Regex KeyValue = new(
        @"(?<![\w-])(" + SecretKey + @")(\s*[=:]\s*)(?!\*\*\*)('[^']*'|[^;&,\s""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Budget);

    private static readonly Regex AuthScheme = new(
        @"\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Budget);

    private static readonly Regex PemBlock = new(
        @"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)",
        RegexOptions.CultureInvariant, Budget);

    /// <summary>يُعيد النصَّ بعد حجب الأسرار. <c>null</c> يبقى <c>null</c>.</summary>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            var result = PemBlock.Replace(text, "-----PRIVATE KEY " + Mask + "-----");
            result = JsonPair.Replace(result, m => m.Groups[1].Value + Mask + m.Groups[3].Value);
            result = KeyValue.Replace(result, m => m.Groups[1].Value + m.Groups[2].Value + Mask);
            result = AuthScheme.Replace(result, m => m.Groups[1].Value + " " + Mask);
            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            // ‏نصٌّ مَرَضيٌّ يستنفد الميزانية: لا يُكتب الأصلُ بحال.
            return Mask;
        }
    }
}
