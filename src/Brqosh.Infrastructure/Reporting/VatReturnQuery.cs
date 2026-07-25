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
    decimal GlVatNetPayableBalance = 0m)
{
    public decimal NetOutputVat => OutputVat + ProgressBillingsVat - CreditNotesVat;
    public decimal NetDeductibleInputVat => DeductibleInputVat - DebitNotesDeductibleVat;
    public decimal NetVatPayable => NetOutputVat - NetDeductibleInputVat;

    /// <summary>فرق بين صافي الإقرار ورصيد GL 2107 — إن كان كبيراً يحتاج تسوية.</summary>
    public decimal GlVariance => NetVatPayable - GlVatNetPayableBalance;
}

public class VatReturnQuery(BrqoshDbContext db)
{
    public async Task<VatReturnResult> ExecuteAsync(long companyId, DateOnly from, DateOnly to)
    {
        var sales = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted
                        && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync();

        var progressBillings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Posted
                        && b.BillingDate >= from && b.BillingDate <= to)
            .ToListAsync();

        var creditNotes = await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.Status == CreditNoteStatus.Posted
                        && n.NoteDate >= from && n.NoteDate <= to)
            .ToListAsync();

        var purchases = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && p.Status == PurchaseInvoiceStatus.Posted
                        && p.InvoiceDate >= from && p.InvoiceDate <= to)
            .ToListAsync();

        var debitNotes = await db.DebitNotes
            .Where(n => n.CompanyId == companyId && n.Status == DebitNoteStatus.Posted
                        && n.NoteDate >= from && n.NoteDate <= to)
            .ToListAsync();

        var deductiblePurchaseIds = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && p.VatDeductible)
            .Select(p => p.Id)
            .ToHashSetAsync();

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
            GlVatNetPayableBalance: glBalance);
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
