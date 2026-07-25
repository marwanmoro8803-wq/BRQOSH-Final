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

/// <summary>تحقق RSA-SHA256 — المفتاح العام مُضمَّن أو قابل للاستبدال في الاختبارات.</summary>
public static class RsaLicenseValidator
{
    /// <summary>للاختبارات فقط — يستبدل المفتاح العام المُضمَّن.</summary>
    internal static string? PublicKeyPemOverride { get; set; }

    private static string? ResolvePublicKeyPem() => EmbeddedLicenseKeys.ResolvePublicKeyPem();

    public static string Sign(LicensePayload payload, string privateKeyPem)
    {
        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return Convert.ToBase64String(rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    public static bool TryValidate(string companyName, string machineId, string licenseJson, out LicensePayload? payload)
    {
        payload = null;
        try
        {
            var doc = JsonDocument.Parse(licenseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("payload", out var payloadEl) ||
                !root.TryGetProperty("signature", out var sigEl))
                return false;

            var inner = JsonSerializer.Deserialize<LicensePayload>(payloadEl.GetRawText());
            if (inner is null) return false;

            if (!string.Equals(inner.CompanyName.Trim(), companyName.Trim(), StringComparison.Ordinal) ||
                !string.Equals(inner.MachineId.Trim(), machineId.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            if (inner.ExpiresUtc < DateTime.UtcNow)
                return false;

            var data = Encoding.UTF8.GetBytes(payloadEl.GetRawText());
            var sig = Convert.FromBase64String(sigEl.GetString() ?? "");
            var publicPem = ResolvePublicKeyPem();
            if (string.IsNullOrWhiteSpace(publicPem))
                return false;

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicPem);
            if (!rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return false;

            payload = inner;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string CreateSignedLicenseJson(LicensePayload payload, string privateKeyPem)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var sig = Sign(payload, privateKeyPem);
        return JsonSerializer.Serialize(new { payload = JsonSerializer.Deserialize<object>(payloadJson), signature = sig });
    }
}
