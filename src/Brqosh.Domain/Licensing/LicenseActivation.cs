using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Licensing;

/// <summary>
/// ‏(Phase 7) <b>رمزُ تفعيلٍ استُهلك على هذا التركيب</b> — سجلُّ الترخيص في القاعدة. الـ<see cref="Nonce"/> فريد، فرمزٌ واحدٌ لا يُستعمل مرّتين؛
/// ‏ولا يُعدَّل سطرٌ بعد كتابته. ولا سعرَ هنا — الأسعارُ شأنُ المورّد خارج البرنامج.
/// </summary>
public class LicenseActivation
{
    public long Id { get; private set; }
    public string Nonce { get; private set; } = null!;
    public string DeviceHash { get; private set; } = null!;
    public string Edition { get; private set; } = null!;
    public int? MaxCompanies { get; private set; }
    public DateTime? ExpiresUtc { get; private set; }
    public bool IsTrial { get; private set; }
    /// <summary>‏بصمةُ SHA-256 لنصّ الرمز — يُعرف بها الرمزُ دون حفظه.</summary>
    public string CodeSha256 { get; private set; } = null!;
    public DateTime IssuedUtc { get; private set; }
    public DateTime ActivatedAtUtc { get; private set; }

    private LicenseActivation()
    {
    }

    public static LicenseActivation Record(string nonce, string deviceHash, string edition, int? maxCompanies,
        DateTime? expiresUtc, bool isTrial, string codeSha256, DateTime issuedUtc, DateTime activatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(deviceHash) || string.IsNullOrWhiteSpace(codeSha256))
            throw new AccountingException("رمزُ التفعيل ناقص.");
        if (maxCompanies is < 1)
            throw new AccountingException("حدُّ المنشآت في رمز التفعيل يجب أن يكون ≥ 1.");
        return new LicenseActivation
        {
            Nonce = nonce.Trim(),
            DeviceHash = deviceHash.Trim(),
            Edition = (edition ?? "").Trim().ToLowerInvariant(),
            MaxCompanies = maxCompanies,
            ExpiresUtc = expiresUtc,
            IsTrial = isTrial,
            CodeSha256 = codeSha256.Trim(),
            IssuedUtc = issuedUtc,
            ActivatedAtUtc = activatedAtUtc,
        };
    }
}
