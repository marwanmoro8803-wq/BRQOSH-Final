using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Zatca;

public record ZatcaCsidResult(
    string RequestId,
    string BinarySecurityToken,
    string Secret,
    string CertificatePemPath,
    string PrivateKeyPemPath);

/// <summary>
/// تسجيل CSID لدى ZATCA — توليد مفتاح/CSR ثم طلب شهادة الامتثال والإنتاج.
/// مرجع: بوابة المطورين https://zatca.gov.sa
/// </summary>
public class ZatcaCsidOnboardingService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    public ZatcaKeyMaterial GenerateKeyPairAndCsr(
        long companyId, string organizationName, string vatNumber, string solutionName = "BRQOSH-ERP")
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length != 15)
            throw new AccountingException("الرقم الضريبي (15 رقماً) مطلوب لتوليد CSR.");

        var dir = GetCertDirectory(companyId);
        Directory.CreateDirectory(dir);

        // ZATCA + ZatcaXmlSigner يتطلبان EC P-256 (secp256r1) — ZATCA-001
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyPath = Path.Combine(dir, "private.key");
        File.WriteAllText(privateKeyPath, ecdsa.ExportECPrivateKeyPem());

        var serial = $"1-{solutionName}|2-1.0|3-{Guid.NewGuid()}";
        var subject = new X500DistinguishedName(
            $"CN={SanitizeDn(organizationName)}, OU=Riyadh Branch, O={SanitizeDn(organizationName)}, C=SA");

        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509Extension(new Oid("2.5.29.15"), new byte[] { 0x03, 0x02, 0x07, 0x80 }, critical: false));

        var csrDer = request.CreateSigningRequest();
        var csrPem = PemEncode("CERTIFICATE REQUEST", csrDer);
        var csrPath = Path.Combine(dir, "request.csr");
        File.WriteAllText(csrPath, csrPem);

        return new ZatcaKeyMaterial(
            privateKeyPath,
            csrPath,
            Convert.ToBase64String(csrDer),
            csrPem);
    }

    public async Task<ZatcaCsidResult> RequestComplianceCsidAsync(
        string apiBaseUrl, string otp, string csrBase64, long companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(otp))
            throw new AccountingException("رمز OTP من بوابة فاتورة مطلوب.");
        if (string.IsNullOrWhiteSpace(csrBase64))
            throw new AccountingException("CSR غير موجود — ولّد المفتاح أولاً.");

        var url = apiBaseUrl.TrimEnd('/') + "/compliance";
        var payload = JsonSerializer.Serialize(new { csr = csrBase64 });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("OTP", otp.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await ParseCsidResponseAsync(await Http.SendAsync(request, ct), companyId, ct);
    }

    public async Task<ZatcaCsidResult> RequestProductionCsidAsync(
        string apiBaseUrl, string complianceRequestId, string binarySecurityToken, string secret,
        long companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(complianceRequestId))
            throw new AccountingException("معرّف طلب الامتثال مطلوب.");

        var url = apiBaseUrl.TrimEnd('/') + "/production/csids";
        var payload = JsonSerializer.Serialize(new { compliance_request_id = complianceRequestId });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = BasicAuth(binarySecurityToken, secret);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await ParseCsidResponseAsync(await Http.SendAsync(request, ct), companyId, ct);
    }

    private async Task<ZatcaCsidResult> ParseCsidResponseAsync(
        HttpResponseMessage response, long companyId, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountingException($"رفضت الهيئة الطلب ({(int)response.StatusCode}): {Truncate(body, 400)}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            throw new AccountingException("أخطاء من الهيئة: " + errors.ToString());

        var requestId = root.TryGetProperty("requestID", out var rid)
            ? rid.ValueKind == JsonValueKind.Number ? rid.GetInt64().ToString() : rid.GetString() ?? ""
            : "";

        if (!root.TryGetProperty("binarySecurityToken", out var tokenEl))
            throw new AccountingException("استجابة غير متوقعة — لا توجد شهادة.");

        var token = tokenEl.GetString() ?? throw new AccountingException("شهادة CSID فارغة.");
        var secret = root.TryGetProperty("secret", out var sec) ? sec.GetString() ?? "" : "";

        var dir = GetCertDirectory(companyId);
        Directory.CreateDirectory(dir);

        var certDer = Convert.FromBase64String(token);
        var certPem = PemEncode("CERTIFICATE", certDer);
        var certPath = Path.Combine(dir, "csid.crt.pem");
        File.WriteAllText(certPath, certPem);

        var privateKeyPath = Path.Combine(dir, "private.key");
        if (!File.Exists(privateKeyPath))
            throw new AccountingException("ملف المفتاح الخاص غير موجود — أعد توليد CSR.");

        return new ZatcaCsidResult(requestId, token, secret, certPath, privateKeyPath);
    }

    public static string BuildApiToken(string binarySecurityToken, string secret) =>
        $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{binarySecurityToken}:{secret}"))}";

    private static AuthenticationHeaderValue BasicAuth(string user, string password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

    private static string GetCertDirectory(long companyId) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BRQOSH", "zatca", companyId.ToString(), "csid");

    private static string SanitizeDn(string value) =>
        (value ?? "").Replace(",", " ").Replace("=", " ").Trim();

    private static string PemEncode(string label, byte[] der)
    {
        var b64 = Convert.ToBase64String(der);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        for (var i = 0; i < b64.Length; i += 64)
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

public record ZatcaKeyMaterial(
    string PrivateKeyPath,
    string CsrPath,
    string CsrBase64,
    string CsrPem);
