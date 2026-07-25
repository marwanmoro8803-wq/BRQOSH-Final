using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

public record ZatcaConfigurationStatus(
    bool IsEnabled,
    bool CertificatePresent,
    bool PrivateKeyPresent,
    bool ApiConfigured,
    bool ReadyForSubmission,
    IReadOnlyList<string> Warnings);

/// <summary>التحقق من جاهزية إعدادات ZATCA.</summary>
public class ZatcaConfigurationValidator(BrqoshDbContext db)
{
    public async Task<ZatcaConfigurationStatus> ValidateAsync(long companyId)
    {
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId);
        var warnings = new List<string>();

        if (settings is null || !settings.IsEnabled)
            return new ZatcaConfigurationStatus(false, false, false, false, false, ["ZATCA غير مفعّلة."]);

        var certOk = File.Exists(settings.CertificateFilePath);
        var keyOk = File.Exists(settings.PrivateKeyFilePath);
        var apiOk = !string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                    && !string.IsNullOrWhiteSpace(ZatcaSettingsSecrets.GetApiToken(settings));

        if (!certOk) warnings.Add("ملف الشهادة غير موجود.");
        if (!keyOk) warnings.Add("ملف المفتاح الخاص غير موجود.");
        if (!apiOk) warnings.Add("عنوان API أو رمز المصادقة غير مضبوط.");

        var company = await db.Companies.SingleAsync(c => c.Id == companyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            warnings.Add("الرقم الضريبي للمنشأة مطلوب.");

        var ready = certOk && keyOk && apiOk && !string.IsNullOrWhiteSpace(company.VatRegistrationNumber);
        return new ZatcaConfigurationStatus(settings.IsEnabled, certOk, keyOk, apiOk, ready, warnings);
    }
}

/// <summary>إرسال مستند ZATCA — في وضع المحاكاة يُقبل محلياً دون اتصال.</summary>
public class ZatcaSubmissionService(BrqoshDbContext db)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<ZatcaEInvoiceDocument> SubmitAsync(long documentId)
    {
        SecurityContext.EnsureCanPostAccounting();
        var doc = await db.ZatcaEInvoiceDocuments.SingleAsync(d => d.Id == documentId);
        var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == doc.CompanyId);

        if (doc.ComplianceStatus is ZatcaComplianceStatus.Reported or ZatcaComplianceStatus.Cleared
            or ZatcaComplianceStatus.Accepted)
            throw new AccountingException("المستند مُرسل مسبقاً.");

        var status = await new ZatcaConfigurationValidator(db).ValidateAsync(doc.CompanyId);
        if (!status.ReadyForSubmission && settings.Environment == ZatcaEnvironment.Production)
            throw new AccountingException("إعدادات ZATCA غير جاهزة للإرسال: " + string.Join(" ", status.Warnings));

        if (settings.Environment == ZatcaEnvironment.Simulation)
        {
            doc.MarkSubmitted(
                doc.TransmissionMode == ZatcaTransmissionMode.Reporting
                    ? ZatcaComplianceStatus.Reported
                    : ZatcaComplianceStatus.Cleared,
                """{"simulation":true,"message":"مقبول في بيئة المحاكاة"}""");
            await db.SaveChangesAsync();
            return doc;
        }

        var xmlPath = doc.SignedXmlFilePath ?? doc.XmlFilePath;
        var xmlB64 = Convert.ToBase64String(await File.ReadAllBytesAsync(xmlPath));
        var payload = JsonSerializer.Serialize(new
        {
            invoiceHash = doc.InvoiceHash,
            uuid = doc.InvoiceUuid,
            invoice = xmlB64,
        });

        var endpoint = doc.TransmissionMode == ZatcaTransmissionMode.Clearance
            ? "/invoices/clearance/single"
            : "/invoices/reporting/single";

        var request = new HttpRequestMessage(HttpMethod.Post, settings.ApiBaseUrl.TrimEnd('/') + endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = ParseAuth(ZatcaSettingsSecrets.GetApiToken(settings));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // (CEEF-206) واجهات الهيئة تشترط Accept-Version؛ غيابها يرفض الطلب رغم صحة المسار والحمولة.
        request.Headers.Add("Accept-Version", "V2");

        var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            doc.MarkRejected(body);
            await db.SaveChangesAsync();
            throw new AccountingException($"رفضت الهيئة المستند: {response.StatusCode}");
        }

        doc.MarkSubmitted(
            doc.TransmissionMode == ZatcaTransmissionMode.Reporting
                ? ZatcaComplianceStatus.Reported
                : ZatcaComplianceStatus.Cleared,
            body);
        await db.SaveChangesAsync();
        return doc;
    }

    private static AuthenticationHeaderValue ParseAuth(string token)
    {
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return new AuthenticationHeaderValue("Bearer", token[7..].Trim());
        if (token.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return AuthenticationHeaderValue.Parse(token);
        return new AuthenticationHeaderValue("Basic", token);
    }
}
