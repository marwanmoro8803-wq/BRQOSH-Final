using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) <b>ما تحمله الفاتورةُ المصحَّحة من المرفوضة</b> — العميلُ والعملةُ وسعرُها والمستودعُ وخصمُ
/// المستند والسطور. <b>بياناتٌ لا فاتورة</b>: تُملأ بها شاشةُ الفاتورة الجديدة فيُعدّلها المستخدمُ ويُرحّلها بـ
/// <see cref="SalesInvoicePostingService"/> كأيّ فاتورة — فتأخذ رقماً جديداً ومستنداً إلكترونيّاً جديداً من المسار المشترك.
/// </summary>
public sealed record CorrectedInvoiceSeed(
    long CustomerId,
    string CurrencyCode,
    decimal ExchangeRate,
    long? WarehouseId,
    decimal DocumentDiscountAmount,
    IReadOnlyList<SalesInvoiceLine> Lines);

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) <b>المرفوضُ يُصحَّح ويُعاد إصداره</b> — وكان لا مسارَ له: مستندٌ «مرفوض» في سجلّ الفوترة
/// الإلكترونيّة ولا شيءَ يُفعل به.
///
/// <para><b>والمسار</b>: الفاتورةُ القياسيّةُ المرفوضةُ تُلغى إلغاءً داخليّاً (عكسُ قيدها، <b>بلا إشعارٍ دائنٍ إلكترونيّ</b> — لم تصدر
/// نظاماً)، والمبسّطةُ المرفوضةُ تُلغى بإشعارٍ دائنٍ كما كان لأنّها وصلت المشتري. ثمّ تُعاد بياناتُها
/// (<see cref="CorrectedInvoiceSeed"/>) لتُفتح بها شاشةُ فاتورةٍ جديدة.</para>
///
/// <para><b>ولماذا لا تُحفظ مسودّةٌ هنا</b>: لا شاشةَ في البرنامج تفتح مسودّةَ فاتورةٍ محفوظةً أو ترحّلها، فمسودّةٌ تُحفظ تبقى
/// عالقةً بلا مستندٍ إلكترونيّ. <b>والفاتورةُ لا تُبنى في طبقة البنية إلّا حيث تُرحَّل</b> (<c>ZatcaChannelCoverageGuardTests</c>).</para>
/// </summary>
public class RejectedInvoiceReissueService(BrqoshDbContext db)
{
    /// <summary>
    /// <b>يُلغي الفاتورةَ المرفوضة ويُعيد ما تُبنى به المصحَّحة</b> — والرفضُ يُسأل عنه <b>قبل</b> الإلغاء، فلا تُلغى فاتورةٌ صحيحةٌ
    /// بالخطأ. والإلغاءُ نفسُه بـ<see cref="SalesInvoiceCancellationService"/>: صلاحيّتُه وفترتُه المقفلةُ وعكسُ قيده ومخزونه.
    /// </summary>
    public async Task<CorrectedInvoiceSeed> CancelRejectedAsync(long companyId, long invoiceId, DateOnly on, string username,
        CancellationToken ct = default)
    {
        var original = await db.SalesInvoices.SingleOrDefaultAsync(i => i.CompanyId == companyId && i.Id == invoiceId, ct)
                       ?? throw new AccountingException("الفاتورة غير موجودة.");
        if (!await IsRejectedAsync(companyId, invoiceId, ct))
            throw new AccountingException("إعادة الإصدار للفواتير التي رفضتها الهيئة وحدها.");

        // ‏وفاتورةٌ أُلغيت سلفاً (أغلق المستخدمُ شاشةَ المصحَّحة دون ترحيل) تُعاد بياناتُها بلا إلغاءٍ ثانٍ — وإلّا ضاع طريقُ تصحيحها.
        if (original.Status != SalesInvoiceStatus.Cancelled)
            await new SalesInvoiceCancellationService(db).CancelAsync(companyId, invoiceId, on, username, ct);

        return new CorrectedInvoiceSeed(original.CustomerId, original.CurrencyCode, original.ExchangeRate,
            original.WarehouseId, original.DocumentDiscountAmount, original.Lines.ToList());
    }

    private Task<bool> IsRejectedAsync(long companyId, long invoiceId, CancellationToken ct) =>
        db.ZatcaEInvoiceDocuments.AnyAsync(d => d.CompanyId == companyId
            && d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoiceId
            && d.ComplianceStatus == ZatcaComplianceStatus.Rejected, ct);
}
