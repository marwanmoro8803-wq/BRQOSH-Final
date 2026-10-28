using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// <b>رجوعُ ساعة الجهاز يُكشف — وكانت الصلاحيةُ تُقاس بها وحدَها.</b>
///
/// <para><b>ما كان:</b> <c>if (inner.ExpiresUtc &lt; DateTime.UtcNow)</c> والموضعُ الوحيدُ
/// الذي يُستدعى منه هو الإقلاع. <b>فمن أرجع ساعةَ جهازه مدّد ترخيصَه بلا حدّ</b> — وصار
/// هذا حمّالاً بعد أن أُغلق ت-١ وصار التفعيلُ إلزامياً في الإنتاج، أي بعد أن صار الترخيصُ
/// هو ما يفصل بين عميلٍ يدفع وعميلٍ لا يدفع.</para>
///
/// <para><b>وعلامةُ الماء العليا:</b> يُحفَظ أحدثُ وقتٍ رآه التطبيقُ قطّ، محميّاً بـDPAPI
/// فلا يُحرَّر بمحرّرِ نصوص. وساعةٌ تعود إلى ما قبله <b>رجوعٌ مؤكّد</b> — لا اجتهاد.</para>
///
/// <para><b>ولا يُوقَف العملُ عند الشكّ:</b> الرجوعُ يُبلَّغ ولا يُغلق التطبيق. فساعةٌ
/// تُصحَّح بعد بطاريّةِ لوحةٍ فارغة تصرف عميلاً صادقاً عن دفاتره، <b>والضررُ من إغلاقٍ
/// خاطئ أكبرُ من الضرر من تمديدٍ يُكتشف</b>. والقرارُ للمالك لا لهذه الطبقة.</para>
/// </summary>
public static class ClockRollbackDetector
{
    /// <summary>هامشٌ يبتلع فروقَ المزامنة العادية — لا يُبلَّغ رجوعٌ دونه.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromHours(6);

    private static string MarkPath => Path.Combine(BrqoshDataRoot.Path(), "clock-mark.dat");

    public sealed record Result(bool RolledBack, DateTime? HighWaterUtc, TimeSpan? Backwards);

    /// <summary>
    /// يقرأ العلامةَ، ويقارنها بالوقت الحاليّ، ثمّ <b>يرفعها إن تقدّم الوقت</b>.
    /// </summary>
    public static Result Observe(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var previous = ReadMark();

        if (previous is { } mark && now + Tolerance < mark)
        {
            // ‏**ولا تُرفَع العلامةُ هنا**: رفعُها على ساعةٍ راجعة يمحو الدليل، فيصير
            // ‏الرجوعُ الثاني غيرَ مكشوف.
            return new Result(true, mark, mark - now);
        }

        if (previous is null || now > previous)
            WriteMark(now);

        return new Result(false, previous is null || now > previous ? now : previous, null);
    }

    internal static DateTime? ReadMark()
    {
        try
        {
            if (!File.Exists(MarkPath)) return null;
            var plain = LocalSecretProtector.Unprotect(File.ReadAllText(MarkPath).Trim());
            return DateTime.TryParse(plain, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.ToUniversalTime()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or FormatException or System.Security.Cryptography.CryptographicException)
        {
            // ‏علامةٌ لا تُقرأ ليست دليلَ رجوع — تُعامَل كأنّها غير موجودة وتُكتب من جديد.
            return null;
        }
    }

    internal static void WriteMark(DateTime utc)
    {
        try
        {
            Directory.CreateDirectory(BrqoshDataRoot.Path());
            File.WriteAllText(MarkPath,
                LocalSecretProtector.Protect(utc.ToUniversalTime().ToString("O")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ‏تعذّرُ الكتابة لا يُسقط الإقلاع — الكشفُ ميزةٌ لا شرطُ تشغيل.
        }
    }

    /// <summary>للاختبار: يمحو العلامة. <b>ويُعلن تعذّرَ المحو</b> بدل ابتلاعه.</summary>
    internal static bool ResetForTests()
    {
        try
        {
            if (File.Exists(MarkPath)) File.Delete(MarkPath);
            return true;
        }
        catch (IOException)
        {
            // ‏ملفٌّ مقفلٌ يجعل الاختبارَ التالي يقرأ علامةً قديمة — فيُبلَّغ لا يُبتلع.
            return false;
        }
    }
}
