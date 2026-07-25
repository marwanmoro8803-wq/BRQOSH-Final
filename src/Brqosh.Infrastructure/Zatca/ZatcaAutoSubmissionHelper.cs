using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>إرسال تلقائي لمستند ZATCA بعد التوليد — محاكاة أو جاهز للإبلاغ/الاعتماد.</summary>
public static class ZatcaAutoSubmissionHelper
{
    public static async Task<ZatcaEInvoiceDocument?> TrySubmitAsync(
        BrqoshDbContext db, long companyId, ZatcaDocumentType docType, long sourceDocumentId,
        CancellationToken ct = default)
    {
        var doc = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.DocumentType == docType &&
            d.SourceDocumentId == sourceDocumentId, ct);

        if (doc is null)
            return null;

        return await TrySubmitDocumentAsync(db, companyId, doc, ct);
    }

    public static async Task<ZatcaEInvoiceDocument?> TrySubmitDocumentAsync(
        BrqoshDbContext db, long companyId, ZatcaEInvoiceDocument doc, CancellationToken ct = default)
    {
        try
        {
            var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == companyId, ct);
            if (settings.Environment == ZatcaEnvironment.Simulation ||
                doc.ComplianceStatus is ZatcaComplianceStatus.ReadyToReport
                    or ZatcaComplianceStatus.ReadyToClear)
            {
                return await new ZatcaSubmissionService(db).SubmitAsync(doc.Id);
            }
        }
        catch (Exception)
        {
            // لا نُوقف ترحيل المستند المحاسبي بسبب فشل ZATCA.
            //
            // (CEEF-210) كان الالتقاط مقصوراً على AccountingException، بينما SubmitAsync
            // ينفّذ نداءً شبكياً يرمي HttpRequestException/TaskCanceledException عند الانقطاع
            // أو المهلة — فتتسرب بعد نجاح الـcommit وتُظهر للمستخدم «فشل ترحيل الفاتورة»
            // رغم أنها رُحّلت فعلاً، وإعادة المحاولة ترمي «الفاتورة مرحّلة».
            // المستند يبقى بحالته (ReadyToReport/ReadyToClear) فيلتقطه إرسال لاحق.
        }

        return doc;
    }
}
