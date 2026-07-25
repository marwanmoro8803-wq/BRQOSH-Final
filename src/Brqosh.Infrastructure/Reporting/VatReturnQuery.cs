using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// إقرار ضريبة القيمة المضافة عن فترة: المبيعات وضريبة المخرجات،
/// المشتريات وضريبة المدخلات القابلة للخصم، وصافي الضريبة (مستحقة أو مستردة)،
/// ومطابقة تقريبية مع رصيد حساب 2107 (إن وُجدت حركة).
///
/// (O-21، CEEF-219) الإقرار يعكس **وقائع الفترة** لا حالة المستند اليوم.
/// كان يُرشّح على Status == Posted فقط: فاتورة صدرت في الربع الأول وأُلغيت في الثاني
/// تصبح Cancelled فتختفي من إعادة تشغيل إقرار الربع الأول — بأرقام أقلّ مما قُدِّم
/// للهيئة فعلاً، ومخالفة لقيدها الباقي في الأستاذ لتلك الفترة. وهو خطر فروقات
/// عند الفحص الضريبي.
///
/// الصواب محاسبياً: إقرار فترة الإصدار يبقى كما قُدِّم، وأثر الإلغاء يظهر في فترة
/// الإلغاء كتسوية سالبة — تماماً كما يفعل إشعار الدائن.
/// </summary>
public record VatReturnResult(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal SalesSubtotal,
    decimal OutputVat,
    decimal ProgressBillingsSubtotal,
    decimal ProgressBillingsVat,
    decimal CreditNotesSubtotal,
    decimal CreditNotesVat,
    decimal PurchasesSubtotal,
    decimal DeductibleInputVat,
    decimal DebitNotesSubtotal,
    decimal DebitNotesVat,
    decimal DebitNotesDeductibleVat,
    decimal GlVatNetPayableBalance = 0m,
    // تسويات إلغاء مستندات صدرت في فترات سابقة وأُلغيت داخل هذه الفترة (CEEF-219).
    decimal CancelledOutputVat = 0m,
    decimal CancelledInputVat = 0m)
{
    public decimal NetOutputVat => OutputVat + ProgressBillingsVat - CreditNotesVat - CancelledOutputVat;
    public decimal NetDeductibleInputVat => DeductibleInputVat - DebitNotesDeductibleVat - CancelledInputVat;
    public decimal NetVatPayable => NetOutputVat - NetDeductibleInputVat;

    /// <summary>فرق بين صافي الإقرار ورصيد GL 2107 — إن كان كبيراً يحتاج تسوية.</summary>
    public decimal GlVariance => NetVatPayable - GlVatNetPayableBalance;
}

public class VatReturnQuery(BrqoshDbContext db)
{
    public async Task<VatReturnResult> ExecuteAsync(long companyId, DateOnly from, DateOnly to)
    {
        var sales = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && (i.Status == SalesInvoiceStatus.Posted
                            || (i.Status == SalesInvoiceStatus.Cancelled && i.CancelledAt > to))
                        && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync();

        var progressBillings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && (b.Status == ProgressBillingStatus.Posted
                            || (b.Status == ProgressBillingStatus.Cancelled && b.CancelledAt > to))
                        && b.BillingDate >= from && b.BillingDate <= to)
            .ToListAsync();

        var creditNotes = await db.CreditNotes
            .Where(n => n.CompanyId == companyId && (n.Status == CreditNoteStatus.Posted
                            || (n.Status == CreditNoteStatus.Cancelled && n.CancelledAt > to))
                        && n.NoteDate >= from && n.NoteDate <= to)
            .ToListAsync();

        var purchases = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && (p.Status == PurchaseInvoiceStatus.Posted
                            || (p.Status == PurchaseInvoiceStatus.Cancelled && p.CancelledAt > to))
                        && p.InvoiceDate >= from && p.InvoiceDate <= to)
            .ToListAsync();

        var debitNotes = await db.DebitNotes
            .Where(n => n.CompanyId == companyId && (n.Status == DebitNoteStatus.Posted
                            || (n.Status == DebitNoteStatus.Cancelled && n.CancelledAt > to))
                        && n.NoteDate >= from && n.NoteDate <= to)
            .ToListAsync();

        var deductiblePurchaseIds = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && p.VatDeductible)
            .Select(p => p.Id)
            .ToHashSetAsync();

        // (CEEF-219) الشقّ الثاني: مستندات صدرت **قبل** هذه الفترة وأُلغيت **داخلها**.
        // أثر الإلغاء ينتمي لفترة الإلغاء — كما لو صدر إشعار دائن. بدون هذا الشقّ
        // تختفي الضريبة من كل الفترات: لا من فترة الإصدار (بقيت كما قُدِّمت) ولا من غيرها.
        //
        // `VatAmount` خاصية محسوبة غير مُعيَّنة في المخطّط، فلا تُترجَم إلى SQL —
        // نُحضر الصفوف ثمّ نجمع في الذاكرة، كما تفعل الاستعلامات أعلاه.
        var cancelledSales = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Cancelled
                        && i.CancelledAt >= from && i.CancelledAt <= to && i.InvoiceDate < from)
            .ToListAsync();

        var cancelledProgressBillings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Cancelled
                        && b.CancelledAt >= from && b.CancelledAt <= to && b.BillingDate < from)
            .ToListAsync();

        var cancelledCreditNotes = await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.Status == CreditNoteStatus.Cancelled
                        && n.CancelledAt >= from && n.CancelledAt <= to && n.NoteDate < from)
            .ToListAsync();

        var cancelledPurchases = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && p.Status == PurchaseInvoiceStatus.Cancelled
                        && p.CancelledAt >= from && p.CancelledAt <= to && p.InvoiceDate < from
                        && p.VatDeductible)
            .ToListAsync();

        var cancelledOutputVat =
            cancelledSales.Sum(i => i.VatAmount)
          + cancelledProgressBillings.Sum(b => b.VatAmount)
          // إلغاء إشعار دائن يعكس خصماً سابقاً — فيزيد ضريبة المخرجات لا ينقصها.
          - cancelledCreditNotes.Sum(n => n.VatAmount);

        var cancelledInputVat = cancelledPurchases.Sum(p => p.VatAmount);

        var glBalance = await ReadVatNetPayableGlAsync(companyId, to);

        return new VatReturnResult(
            from, to,
            SalesSubtotal: sales.Sum(i => i.Subtotal),
            OutputVat: sales.Sum(i => i.VatAmount),
            ProgressBillingsSubtotal: progressBillings.Sum(b => b.TaxableAmount),
            ProgressBillingsVat: progressBillings.Sum(b => b.VatAmount),
            CreditNotesSubtotal: creditNotes.Sum(n => n.Subtotal),
            CreditNotesVat: creditNotes.Sum(n => n.VatAmount),
            PurchasesSubtotal: purchases.Sum(p => p.Subtotal),
            DeductibleInputVat: purchases.Where(p => p.VatDeductible).Sum(p => p.VatAmount),
            DebitNotesSubtotal: debitNotes.Sum(n => n.Subtotal),
            DebitNotesVat: debitNotes.Sum(n => n.VatAmount),
            DebitNotesDeductibleVat: debitNotes
                .Where(n => deductiblePurchaseIds.Contains(n.OriginalPurchaseInvoiceId))
                .Sum(n => n.VatAmount),
            GlVatNetPayableBalance: glBalance,
            CancelledOutputVat: cancelledOutputVat,
            CancelledInputVat: cancelledInputVat);
    }

    private async Task<decimal> ReadVatNetPayableGlAsync(long companyId, DateOnly asOf)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a =>
            a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatNetPayable);
        if (account is null)
            return 0m;

        var rows = await new TrialBalanceQuery(db).ExecuteAsync(
            companyId, new DateOnly(1900, 1, 1), asOf);
        var row = rows.FirstOrDefault(r => r.AccountCode == account.Code);
        if (row is null)
            return 0m;

        // التزام — رصيد دائن موجب
        return row.BalanceCredit - row.BalanceDebit;
    }
}
