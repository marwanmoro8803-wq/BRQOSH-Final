using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) <b>المرفوضُ يُصحَّح ويُعاد إصداره</b> — وكان لا مسارَ له: مستندٌ «مرفوض» في سجلّ الفوترة
/// الإلكترونيّة ولا شيءَ يُفعل به.
///
/// <para><b>والمسار</b>: الفاتورةُ القياسيّةُ المرفوضةُ تُلغى إلغاءً داخليّاً (عكسُ قيدها، <b>بلا إشعارٍ دائنٍ إلكترونيّ</b> — لم تصدر
/// نظاماً)، ثمّ <b>مسودّةٌ مصحَّحةٌ</b> بسطورها وعميلها وعملتها وخصمها وفرعها ومستودعها يُعدّلها المستخدمُ ويُرحّلها فتأخذ رقماً جديداً
/// ومستنداً جديداً. والمبسّطةُ المرفوضةُ تُلغى بإشعارٍ دائنٍ كما كان، ثمّ تُنسخ كذلك.</para>
/// </summary>
public class RejectedInvoiceReissueService(BrqoshDbContext db)
{
    /// <summary>
    /// <b>يُلغي الفاتورةَ المرفوضة ثمّ يُنشئ مسودّتَها المصحَّحة</b> — والرفضُ يُسأل عنه <b>قبل</b> الإلغاء، فلا تُلغى فاتورةٌ صحيحةٌ بالخطأ.
    /// </summary>
    public async Task<long> CancelAndCreateCorrectedDraftAsync(long companyId, long invoiceId, DateOnly on, string username,
        CancellationToken ct = default)
    {
        if (!await IsRejectedAsync(companyId, invoiceId, ct))
            throw new AccountingException("إعادة الإصدار للفواتير التي رفضتها الهيئة وحدها.");

        await new SalesInvoiceCancellationService(db).CancelAsync(companyId, invoiceId, on, username, ct);
        return await CreateCorrectedDraftAsync(companyId, invoiceId, on, ct);
    }

    private Task<bool> IsRejectedAsync(long companyId, long invoiceId, CancellationToken ct) =>
        db.ZatcaEInvoiceDocuments.AnyAsync(d => d.CompanyId == companyId
            && d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoiceId
            && d.ComplianceStatus == ZatcaComplianceStatus.Rejected, ct);

    /// <summary>مسودّةُ فاتورةٍ مصحَّحة عن فاتورةٍ أُلغيت بعد رفض الهيئة — تُعاد مُعرَّفاً لتُفتح وتُعدَّل.</summary>
    public async Task<long> CreateCorrectedDraftAsync(long companyId, long cancelledInvoiceId, DateOnly invoiceDate,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        var original = await db.SalesInvoices.SingleOrDefaultAsync(i => i.CompanyId == companyId && i.Id == cancelledInvoiceId, ct)
                       ?? throw new AccountingException("الفاتورة غير موجودة.");
        if (original.Status != SalesInvoiceStatus.Cancelled)
            throw new AccountingException("تُصحَّح الفاتورة المرفوضة بعد إلغائها — ألغها أولاً.");
        if (!await IsRejectedAsync(companyId, cancelledInvoiceId, ct))
            throw new AccountingException("إعادة الإصدار للفواتير التي رفضتها الهيئة وحدها.");

        var draft = SalesInvoice.CreateDraft(companyId, original.CustomerId, invoiceDate);
        foreach (var l in original.Lines)
            draft.AddLine(l.Description, l.Quantity, l.UnitPrice, l.ProductId, l.VatCategory,
                l.TaxExemptionReasonCode, l.TaxExemptionReason, l.BatchNumber, l.DiscountPercent, l.UnitCode);
        if (original.CurrencyCode != "SAR")
            draft.SetCurrency(original.CurrencyCode, original.ExchangeRate);
        if (original.DocumentDiscountAmount > 0m)
            draft.ApplyDocumentDiscount(original.DocumentDiscountAmount);
        if (original.BranchId is long branchId)
            draft.AssignBranch(branchId);
        draft.SetWarehouse(original.WarehouseId);

        db.SalesInvoices.Add(draft);
        await db.SaveChangesAsync(ct);
        return draft.Id;
    }
}
