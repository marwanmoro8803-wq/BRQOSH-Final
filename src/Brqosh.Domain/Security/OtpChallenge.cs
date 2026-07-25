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

    public bool IsExpired(DateTime? utcNow = null) =>
        (utcNow ?? DateTime.UtcNow) >= ExpiresAtUtc;

    public void Consume() => IsConsumed = true;
}
