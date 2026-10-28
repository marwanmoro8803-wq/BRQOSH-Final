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
                // المسار التلقائيّ لا يُعيد فحص الدور — الإذن مُنح عند
                // الترحيل الذي استدعاه. انظر الشرح على الدالة نفسها.
                return await new ZatcaSubmissionService(db).SubmitAsFollowUpToPostingAsync(doc.Id);
            }
        }
        catch (Exception ex)
        {
            // لا نُوقف ترحيل المستند المحاسبي بسبب فشل ZATCA.
            //
            // كان الالتقاط مقصوراً على AccountingException، بينما SubmitAsync
            // ينفّذ نداءً شبكياً يرمي HttpRequestException/TaskCanceledException عند الانقطاع
            // أو المهلة — فتتسرب بعد نجاح الـcommit وتُظهر للمستخدم «فشل ترحيل الفاتورة»
            // رغم أنها رُحّلت فعلاً، وإعادة المحاولة ترمي «الفاتورة مرحّلة».
            // المستند يبقى بحالته (ReadyToReport/ReadyToClear) فيلتقطه إرسال لاحق.
            //
            // **لكنّ الابتلاع الصامت كان يُخفي أكثر من انقطاع الشبكة.**
            // رفضُ صلاحيةٍ كان يسقط هنا بلا أثر، فتبدو البيعة ناجحة تماماً بينما فاتورتها لن
            // تصل الهيئة أبداً — والفرق بين «الشبكة مقطوعة الآن» و«هذا المستخدم لا يستطيع
            // الإرسال أبداً» هو الفرق بين تأخيرٍ وخللٍ دائم. فصار السبب **يُسجَّل على المستند**:
            // الترحيل لا يتوقف، والحالة تبقى قابلة لإعادة المحاولة، ولا يضيع الخبر.
            doc.RecordSubmissionFailure(Describe(ex));
            await db.SaveChangesAsync(ct);
        }

        return doc;
    }

    /// <summary>وصفٌ موجز للسبب يصلح للعرض والتشخيص — بلا آثار مكدّس ولا أسرار اتصال.</summary>
    private static string Describe(Exception ex) => ex switch
    {
        AccountingException => ex.Message,
        HttpRequestException => "تعذّر الاتصال بخادم الهيئة: " + ex.Message,
        TaskCanceledException => "انتهت مهلة الاتصال بخادم الهيئة.",
        _ => ex.GetType().Name + ": " + ex.Message,
    };
}
