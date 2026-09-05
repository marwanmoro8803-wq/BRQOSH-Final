using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brqosh.VendorIssuer;

/// <summary>حمولةُ الترخيص — الترتيبُ الموضعيُّ هنا هو ترتيبُ الحقول في JSON، فلا يُبدَّل.</summary>
public record LicensePayload(
    string CompanyName,
    string MachineId,
    string Tier,
    DateTime ExpiresUtc,
    string[] Features);

/// <summary>حكمُ التحقّق — بأسماء المنتج نفسِها وترتيبِه نفسِه.</summary>
public enum LicenseVerdict
{
    Valid = 0,
    Malformed,
    NoTrustAnchor,
    CompanyMismatch,
    MachineMismatch,
    Expired,
    BadSignature,
}

/// <summary>
/// <b>قلبُ الإصدار — التوقيعُ على البايتات التي تُكتب، لا على بايتاتٍ مشابهة.</b>
///
/// <para>المنتجُ يتحقّق من التوقيع على <b>النصّ الخامّ لعنصر <c>payload</c> كما ورد في الملفّ</b>
/// (<c>JsonElement.GetRawText()</c>) — لا على صيغةٍ قانونيّةٍ يُعاد بناؤها. فالمُصدِّرُ الذي
/// يوقّع تسلسلاً ثمّ يكتب تسلسلاً آخرَ (بمسافاتٍ، أو بترتيبٍ مختلف، أو بتنسيقٍ جميل)
/// <b>يُنتج تراخيصَ ترفضها كلُّ آلة</b> وشيفرتُه تبدو سليمة.</para>
///
/// <para>ولذلك يُبنى الغلافُ هنا <b>بالوصل النصّيّ</b> لا بإعادة تسلسل: النصُّ الذي وُقِّع هو
/// عينُ النصّ الذي كُتب، ولا مجالَ لافتراقهما. ويقيس <see cref="SelfTest"/> هذه الهُويّة صراحةً.</para>
/// </summary>
public static class LicenseCore
{
    /// <summary>بصمةُ مفتاحٍ عامّ: SHA-256 على SubjectPublicKeyInfo، ستّ عشريّ بأحرفٍ كبيرة.</summary>
    public static string Fingerprint(RSA rsa) =>
        Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));

    public static string FingerprintOfPem(string pem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return Fingerprint(rsa);
    }

    /// <summary>تسلسلُ الحمولة — بلا خياراتٍ، مطابقاً لما يفعله المنتج.</summary>
    public static string SerializePayload(LicensePayload payload) => JsonSerializer.Serialize(payload);

    /// <summary>
    /// يبني الغلافَ الموقَّع. <paramref name="payloadRaw"/> يُعيد النصَّ الذي وُقِّع بالضبط —
    /// كي يقيس المتّصلُ الهُويّةَ بدل أن يفترضها.
    /// </summary>
    public static string BuildSignedEnvelope(LicensePayload payload, RSA privateKey, out string payloadRaw)
    {
        payloadRaw = SerializePayload(payload);
        var signature = Convert.ToBase64String(
            privateKey.SignData(Encoding.UTF8.GetBytes(payloadRaw), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        // ‏وصلٌ نصّيّ لا إعادةُ تسلسل — النصُّ الموقَّعُ هو المكتوب حرفاً بحرف.
        // ‏واسما الحقلين صغيرا الأحرف: المنتجُ يبحث عنهما بحسّاسيّةٍ للحالة.
        return "{\"payload\":" + payloadRaw + ",\"signature\":" + JsonSerializer.Serialize(signature) + "}";
    }

    /// <summary>
    /// نسخةُ حكم المنتج (<c>RsaLicenseValidator.Validate</c>) بترتيبها نفسِه:
    /// الشكل ← المنشأة ← الجهاز ← الصلاحية ← مرساةُ الثقة ← التوقيع.
    /// </summary>
    public static LicenseVerdict Validate(string companyName, string machineId, string envelopeJson,
        string trustAnchorPem, out LicensePayload? payload)
    {
        payload = null;

        string payloadRaw;
        string signatureB64;
        LicensePayload inner;
        try
        {
            using var doc = JsonDocument.Parse(envelopeJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out var payloadEl)
                || !root.TryGetProperty("signature", out var sigEl))
                return LicenseVerdict.Malformed;

            payloadRaw = payloadEl.GetRawText();
            signatureB64 = sigEl.GetString() ?? "";
            inner = JsonSerializer.Deserialize<LicensePayload>(payloadRaw) ?? throw new JsonException("payload null");
        }
        // ‏`GetString()` على عنصرٍ ليس نصّاً يرمي `InvalidOperationException`، وتاريخٌ لا يُقرأ
        // ‏يرمي `FormatException` — وكلاهما «مُشوَّه» لا انهيار.
        catch (Exception ex) when (ex is JsonException or NotSupportedException
                                      or InvalidOperationException or FormatException or ArgumentException)
        {
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

        if (string.IsNullOrWhiteSpace(trustAnchorPem))
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
            rsa.ImportFromPem(trustAnchorPem);
            if (!rsa.VerifyData(Encoding.UTF8.GetBytes(payloadRaw), signature,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return LicenseVerdict.BadSignature;
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            return LicenseVerdict.NoTrustAnchor;
        }

        payload = inner;
        return LicenseVerdict.Valid;
    }

    /// <summary>ملفُّ <c>license.json</c> جاهزاً للوضع في جذر بيانات العميل.</summary>
    public static string BuildLicenseStoreFile(string companyName, string machineId, string envelopeJson) =>
        JsonSerializer.Serialize(
            new { CompanyName = companyName, MachineId = machineId, SignedLicenseJson = envelopeJson },
            new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// <b>الأحرفُ الخفيّةُ التي تُسقط الترخيصَ بلا أثرٍ مرئيّ.</b> مقارنةُ اسم المنشأة
    /// <c>Ordinal</c>: مسافةٌ غيرُ فاصلةٍ أو علامةُ اتجاهٍ منسوخةٌ من مستندٍ تجعل اسمَين
    /// يبدوان سواءً مختلفَين، والرسالةُ تقول «صادرٌ لاسم منشأةٍ آخر».
    /// </summary>
    public static string? DescribeHiddenCharacters(string text)
    {
        var found = new List<string>();
        foreach (var ch in text)
        {
            // ‏بترميزٍ هارب لا بالمحرف نفسِه: محرفٌ غيرُ مرئيٍّ في شيفرةٍ تصطاد المحارفَ
            // ‏غيرَ المرئيّة يُصلحه أوّلُ مُنسِّقٍ بلا أن يلحظه أحد.
            var suspicious = ch switch
            {
                '\u00A0' => "مسافة غير فاصلة (U+00A0)",
                '\u200B' => "مسافة صفريّة (U+200B)",
                '\u200E' => "علامة اتجاه يسار-يمين (U+200E)",
                '\u200F' => "علامة اتجاه يمين-يسار (U+200F)",
                '\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E' => "علامة تضمين اتجاهيّ",
                '\u0640' => "تطويل (U+0640)",
                '\uFEFF' => "علامة ترتيب بايتات (U+FEFF)",
                '\t' => "جدولة (Tab)",
                _ => null,
            };
            if (suspicious is not null && !found.Contains(suspicious))
                found.Add(suspicious);
        }

        if (text.Length != text.Trim().Length)
            found.Insert(0, "مسافات في الطرفين (تُقتطع تلقائياً)");

        return found.Count == 0 ? null : string.Join(" · ", found);
    }

    /// <summary>معرّفُ الجهاز: ستّ عشريٌّ بستّة عشر محرفاً — يُنبَّه على ما خالف.</summary>
    public static bool LooksLikeMachineId(string value) =>
        value.Length == 16 && value.All(Uri.IsHexDigit);

    /// <summary>
    /// تنسيقُ تاريخٍ للعرض. <b>بثقافةٍ ثابتةٍ قصداً</b>: على ويندوز عربيّ (ar-SA) التقويمُ
    /// الافتراضيُّ أمُّ القرى، فيقرأ المشغّلُ سنةً هجريّةً ويظنّ الترخيصَ منتهياً.
    /// </summary>
    public static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string DefaultPrivateKeyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BRQOSH", "vendor-signing", "license-private.pem");
}
