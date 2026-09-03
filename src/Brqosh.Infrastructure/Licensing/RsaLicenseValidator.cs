using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>حمولة الترخيص الموقّعة — لا يوجد سر داخل التطبيق.</summary>
public record LicensePayload(
    string CompanyName,
    string MachineId,
    string Tier,
    DateTime ExpiresUtc,
    string[] Features);

public record SignedLicense(LicensePayload Payload, string SignatureBase64);

/// <summary>
/// (CEEF-236 · ت-١) <b>حكمُ التحقّق — سببٌ مسمّىً لا «غير صالح».</b>
///
/// <para>كانت نافذةُ التفعيل تقول لكلّ إخفاقٍ «ترخيص غير صالح لهذا الجهاز أو اسم المنشأة» —
/// <b>وحين كان السببُ غيابَ مفتاح الثقة في التركيب</b> أرسلت العميلَ يُراجع اسمَ منشأته.
/// ورسالةٌ تُشخّص سبباً غيرَ السبب أسوأُ من الصمت: تُضيّع وقتَ الطرفَين على ما ليس عطلاً.</para>
/// </summary>
public enum LicenseVerdict
{
    Valid = 0,
    /// <summary>ليس JSON ترخيصٍ أصلاً — لُصق نصٌّ ناقصٌ أو مُحرَّف.</summary>
    Malformed,
    /// <summary>لا مفتاحَ ثقةٍ يُستورد — عطلُ تركيبٍ لا عطلُ ترخيص.</summary>
    NoTrustAnchor,
    CompanyMismatch,
    MachineMismatch,
    Expired,
    /// <summary>التوقيعُ ليس بمفتاح المورِّد — ترخيصٌ مُختلَقٌ أو صادرٌ بمفتاحٍ آخر.</summary>
    BadSignature,
}

/// <summary>
/// تحقّق RSA-SHA256. المفتاحُ العامّ <b>مُضمَّنٌ في التجميعة</b> (<see cref="EmbeddedLicenseKeys"/>)،
/// ويُستبدل في الاختبارات وحدها عبر <see cref="PublicKeyPemOverride"/>.
/// </summary>
public static class RsaLicenseValidator
{
    /// <summary>للاختبارات فقط — يستبدل المفتاح العام المُضمَّن. <c>internal</c>: لا يبلغه مستخدم.</summary>
    internal static string? PublicKeyPemOverride { get; set; }

    private static string? ResolvePublicKeyPem() => EmbeddedLicenseKeys.ResolvePublicKeyPem();

    /// <summary>هل يملك هذا التركيبُ مفتاحَ ثقةٍ يُستورد فعلاً — لا مجرّد نصّ.</summary>
    public static bool HasTrustAnchor() => TrustAnchorFingerprint() is not null;

    /// <summary>
    /// بصمةُ مفتاح الثقة الفعّال (SHA-256 على SubjectPublicKeyInfo، ستّ عشريّ) — للدعم:
    /// يُقارنها المورِّدُ ببصمة مفتاحه فيعرف أيَّ مفتاحٍ يثق به هذا البناء.
    /// </summary>
    public static string? TrustAnchorFingerprint()
    {
        var pem = ResolvePublicKeyPem();
        if (string.IsNullOrWhiteSpace(pem))
            return null;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static string Sign(LicensePayload payload, string privateKeyPem)
    {
        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return Convert.ToBase64String(rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    public static bool TryValidate(string companyName, string machineId, string licenseJson, out LicensePayload? payload)
        => Validate(companyName, machineId, licenseJson, out payload) == LicenseVerdict.Valid;

    /// <summary>يُعيد <see cref="LicenseVerdict.Valid"/> مع الحمولة، أو أوّلَ سببٍ للرفض بالترتيب: الشكل، المنشأة، الجهاز، الصلاحية، مفتاح الثقة، التوقيع.</summary>
    public static LicenseVerdict Validate(string companyName, string machineId, string licenseJson, out LicensePayload? payload)
    {
        payload = null;

        string payloadRaw;
        string signatureB64;
        LicensePayload inner;
        try
        {
            using var doc = JsonDocument.Parse(licenseJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out var payloadEl)
                || !root.TryGetProperty("signature", out var sigEl))
                return LicenseVerdict.Malformed;

            // ‏يُوقَّع النصُّ الخامُّ لعنصر الحمولة كما ورد — لا إعادةَ تسلسلٍ قد تُغيّر ترتيباً أو مسافة.
            payloadRaw = payloadEl.GetRawText();
            signatureB64 = sigEl.GetString() ?? "";
            inner = JsonSerializer.Deserialize<LicensePayload>(payloadRaw) ?? throw new JsonException("payload null");
        }
        catch (JsonException)
        {
            return LicenseVerdict.Malformed;
        }
        catch (NotSupportedException)
        {
            return LicenseVerdict.Malformed;
        }
        catch (InvalidOperationException)
        {
            // (CEEF-236) ‏`GetString()` على عنصرٍ ليس نصّاً (‏`"signature": 12`‎) يرمي هذا لا
            // ‏`JsonException`. **ونصٌّ يلصقه المستخدم لا يجوز أن يُسقط نافذةً بمكدّس** — هو مُشوَّه،
            // ‏وهذا حكمُه.
            return LicenseVerdict.Malformed;
        }

        if (inner.CompanyName is null || inner.MachineId is null)
            return LicenseVerdict.Malformed;

        if (!string.Equals(inner.CompanyName.Trim(), companyName.Trim(), StringComparison.Ordinal))
            return LicenseVerdict.CompanyMismatch;

        if (!string.Equals(inner.MachineId.Trim(), machineId.Trim(), StringComparison.OrdinalIgnoreCase))
            return LicenseVerdict.MachineMismatch;

        if (inner.ExpiresUtc < DateTime.UtcNow)
            return LicenseVerdict.Expired;

        var publicPem = ResolvePublicKeyPem();
        if (string.IsNullOrWhiteSpace(publicPem))
            return LicenseVerdict.NoTrustAnchor;

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureB64);
        }
        catch (FormatException)
        {
            return LicenseVerdict.BadSignature;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicPem);
            if (!rsa.VerifyData(Encoding.UTF8.GetBytes(payloadRaw), signature,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return LicenseVerdict.BadSignature;
        }
        catch (ArgumentException)
        {
            // ‏نصٌّ ليس PEM مفتاحٍ — عطلُ تركيب، لا يُنسَب إلى الترخيص.
            return LicenseVerdict.NoTrustAnchor;
        }
        catch (CryptographicException)
        {
            return LicenseVerdict.NoTrustAnchor;
        }

        payload = inner;
        return LicenseVerdict.Valid;
    }

    public static string CreateSignedLicenseJson(LicensePayload payload, string privateKeyPem)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var sig = Sign(payload, privateKeyPem);
        return JsonSerializer.Serialize(new { payload = JsonSerializer.Deserialize<object>(payloadJson), signature = sig });
    }
}
