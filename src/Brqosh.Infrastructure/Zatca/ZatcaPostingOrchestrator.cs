using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// تنسيق ZATCA مع الترحيل المحاسبي:
/// - المحاكاة: توليد + إرسال داخل نفس معاملة DB (ذرّية مع GL).
/// - الإنتاج: توليد داخل المعاملة · إرسال HTTP بعد الـ commit فقط.
/// </summary>
public static class ZatcaPostingOrchestrator
{
    public static async Task<ZatcaEInvoiceDocument?> GenerateInsideTransactionAsync(
        BrqoshDbContext db,
        long companyId,
        Func<Task<ZatcaEInvoiceDocument?>> generate,
        CancellationToken ct = default)
    {
        var doc = await generate();

        if (doc is null)
            return null;

        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (settings is { IsEnabled: true, Environment: ZatcaEnvironment.Simulation })
            await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, doc, ct);

        return doc;
    }

    /// <summary>بعد نجاح commit — إرسال بيئة الإنتاج/جاهزية الإبلاغ فقط (ليس المحاكاة).</summary>
    public static async Task<ZatcaEInvoiceDocument?> SubmitAfterCommitAsync(
        BrqoshDbContext db,
        long companyId,
        ZatcaDocumentType docType,
        long sourceDocumentId,
        CancellationToken ct = default)
    {
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (settings is null || !settings.IsEnabled)
            return null;

        if (settings.Environment == ZatcaEnvironment.Simulation)
            return await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                d.CompanyId == companyId &&
                d.DocumentType == docType &&
                d.SourceDocumentId == sourceDocumentId, ct);

        return await ZatcaAutoSubmissionHelper.TrySubmitAsync(
            db, companyId, docType, sourceDocumentId, ct);
    }
}
