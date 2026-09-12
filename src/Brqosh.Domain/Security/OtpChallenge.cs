namespace Brqosh.Domain.Security;

/// <summary>تحدي رمز لمرة واحدة — استرجاع كلمة المرور أو تسجيل حساب جديد.</summary>
public class OtpChallenge
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Username { get; private set; }
    public string PhoneE164 { get; private set; }
    public string CodeHash { get; private set; }
    public OtpPurpose Purpose { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsConsumed { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// (م-٤ · ن-٢٨) <b>عددُ المحاولات الخاطئة — وكان لا يوجد.</b>
    ///
    /// <para><b>العطل:</b> مواضعُ التحقّق الثلاثةُ كانت تقرأ التحدّي وترمي عند عدم
    /// المطابقة <b>بلا أن تمسّ الصفّ</b>. فالرمزُ من ستّة أرقامٍ يبقى قابلاً للتخمين
    /// <b>بلا حدٍّ طوالَ عشر دقائق</b> — والمليونُ احتمالٍ ليست حاجزاً حين لا يُعدّ أحدٌ
    /// المحاولات.</para>
    ///
    /// <para><b>وحدُّ هذا العدّاد مُعلَنٌ لا مسكوتٌ عنه:</b> لا رمزَ تزامنٍ على هذا الكيان،
    /// فمحاولتان خاطئتان <b>متزامنتان تماماً</b> من عمليّتين قد تُسجَّلان واحدةً.
    /// والمستدعي الإنتاجيُّ الوحيد حوارُ WPF يُسلسل نفسَه بعلَم <c>_busy</c>، فالنافذةُ
    /// نظريّةٌ على المسار القائم — <b>وتُذكر لأنّ من يفتح مساراً ثانياً غداً يحتاج أن
    /// يعرفها</b>.</para>
    /// </summary>
    public int FailedAttempts { get; private set; }

    private OtpChallenge()
    {
        Username = null!;
        PhoneE164 = null!;
        CodeHash = null!;
    }

    public static OtpChallenge Create(
        long companyId, string username, string phoneE164, string codeHash,
        OtpPurpose purpose, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("اسم المستخدم مطلوب.");
        if (string.IsNullOrWhiteSpace(phoneE164))
            throw new ArgumentException("رقم الجوال مطلوب.");
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("هاش الرمز مطلوب.");

        return new OtpChallenge
        {
            CompanyId = companyId,
            Username = username.Trim().ToLowerInvariant(),
            PhoneE164 = phoneE164.Trim(),
            CodeHash = codeHash,
            Purpose = purpose,
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>الحدُّ الأقصى للمحاولات الخاطئة قبل أن يُحرق التحدّي.</summary>
    public const int MaxFailedAttempts = 5;

    public bool IsExpired(DateTime? utcNow = null) =>
        (utcNow ?? DateTime.UtcNow) >= ExpiresAtUtc;

    public void Consume() => IsConsumed = true;

    /// <summary>
    /// (م-٤ · ن-٢٨) يُسجّل محاولةً خاطئة — <b>ويحرق التحدّي عند بلوغ الحدّ</b>.
    ///
    /// <para><b>والاستهلاكُ عند النفاد لا مجرّدُ الرفض:</b> عدّادٌ يُحصي ولا يُغلق يُبطئ
    /// المهاجمَ ولا يوقفه. وبالاستهلاك يُجبَر على طلبِ رمزٍ جديد — وذلك مسارٌ يمرّ
    /// بمهلةِ إعادةِ إرسالٍ ويُرسل رسالةً إلى صاحب الحساب، <b>فيصير الهجومُ مرئيّاً
    /// ومكلفاً معاً</b>.</para>
    /// </summary>
    public void RecordFailedAttempt()
    {
        FailedAttempts++;
        if (FailedAttempts >= MaxFailedAttempts) IsConsumed = true;
    }
}
