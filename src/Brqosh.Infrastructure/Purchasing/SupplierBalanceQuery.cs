using Brqosh.Infrastructure.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

public record SupplierBalance(long SupplierId, decimal Invoiced, decimal Paid)
{
    public decimal Balance => Invoiced - Paid;
}

public record SupplierStatementLine(DateOnly Date, string DocumentNumber, string DocumentType,
    decimal Debit, decimal Credit, decimal RunningBalance);

/// <summary>
/// دفتر مساعد للموردين: الرصيد الدائن = فواتير المشتريات − سندات الصرف − شيكات صادرة − محتجزات باطن.
/// </summary>
public class SupplierBalanceQuery(BrqoshDbContext db)
{
    private static readonly ChequeStatus[] IssuedChequePaymentStatuses =
    [
        ChequeStatus.InPortfolio,
        ChequeStatus.Deposited,
        ChequeStatus.Cleared,
    ];

    public async Task<SupplierBalance> ForSupplierAsync(long companyId, long supplierId)
    {
        var invoiced = await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.SupplierId == supplierId && i.Status == PurchaseInvoiceStatus.Posted)
            .SumGrandTotalAsync();

        // (CEEF-229 · م0-٨) إشعارات المدين تخفّض المستحقّ للمورد.
        // كانت غائبة عن هذا الاستعلام كلياً بينما DebitNotePostingService يُمدِّن 2101 فعلاً،
        // فيفترق الدفتر المساعد عن الأستاذ بقيمة كل مرتجع مشتريات — ويُدفع للمورد زائداً.
        // والنمط كان قائماً على الجهة المقابلة: CustomerBalanceQuery يطرح الإشعارات الدائنة.
        var debited = await DebitNotesForSupplierAsync(companyId, supplierId);

        var paid = await PaymentsForSupplierAsync(companyId, supplierId);
        return new SupplierBalance(supplierId, invoiced - debited, paid);
    }

    private async Task<decimal> DebitNotesForSupplierAsync(long companyId, long supplierId) =>
        (await db.DebitNotes
            .Where(n => n.CompanyId == companyId && n.SupplierId == supplierId
                        && n.Status == DebitNoteStatus.Posted)
            .Select(n => new { n.Subtotal, n.VatAmount })
            .ToListAsync())
        .Sum(n => n.Subtotal + n.VatAmount);

    public async Task<IReadOnlyDictionary<long, decimal>> AllBalancesAsync(long companyId)
    {
        var invoiced = await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.Status == PurchaseInvoiceStatus.Posted)
            .GroupBy(i => i.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(i => i.Subtotal + i.VatAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total);

        var paidVouchers = await db.PaymentVouchers
            .Where(v => v.CompanyId == companyId && v.Status == PaymentVoucherStatus.Posted)
            .GroupBy(v => v.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(v => v.Amount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total);

        var chequePaid = await db.Cheques
            .Where(c => c.CompanyId == companyId && c.SupplierId != null
                        && c.Type == ChequeType.Issued
                        && IssuedChequePaymentStatuses.Contains(c.Status))
            .GroupBy(c => c.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total);

        var retentionPaid = await new SubcontractSupplierRetentionQuery(db).AllWithheldBySupplierAsync(companyId);

        // (CEEF-229 · م0-٨) إشعارات المدين — انظر التعليق في ForSupplierAsync.
        var debited = (await db.DebitNotes
                .Where(n => n.CompanyId == companyId && n.Status == DebitNoteStatus.Posted)
                .Select(n => new { n.SupplierId, n.Subtotal, n.VatAmount })
                .ToListAsync())
            .GroupBy(n => n.SupplierId)
            .ToDictionary(g => g.Key, g => g.Sum(n => n.Subtotal + n.VatAmount));

        var all = invoiced.Keys
            .Union(paidVouchers.Keys)
            .Union(chequePaid.Keys)
            .Union(retentionPaid.Keys)
            .Union(debited.Keys);

        return all.ToDictionary(id =>
            id,
            id => invoiced.GetValueOrDefault(id)
                  - debited.GetValueOrDefault(id)
                  - paidVouchers.GetValueOrDefault(id)
                  - chequePaid.GetValueOrDefault(id)
                  - retentionPaid.GetValueOrDefault(id));
    }

    public async Task<IReadOnlyList<SupplierStatementLine>> StatementAsync(long companyId, long supplierId)
    {
        var rows = new List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)>();

        foreach (var i in await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.SupplierId == supplierId && i.Status == PurchaseInvoiceStatus.Posted)
            .ToListAsync())
            rows.Add((i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مشتريات", 0m, i.GrandTotal()));

        foreach (var i in await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.SupplierId == supplierId && i.Status == PurchaseInvoiceStatus.Cancelled)
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مشتريات",
                0m, i.GrandTotal(), i.JournalEntryId);
        }

        foreach (var v in await db.PaymentVouchers
            .Where(v => v.CompanyId == companyId && v.SupplierId == supplierId
                        && (v.Status == PaymentVoucherStatus.Posted || v.Status == PaymentVoucherStatus.Reversed))
            .ToListAsync())
        {
            rows.Add((v.VoucherDate, v.VoucherNumber ?? "", "سند صرف", v.Amount, 0m));
            if (v.Status == PaymentVoucherStatus.Reversed && v.JournalEntryId is long jeId)
            {
                var original = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == jeId);
                if (original?.ReversalEntryId is long revId)
                {
                    var reversal = await db.JournalEntries.SingleAsync(e => e.Id == revId);
                    rows.Add((reversal.EntryDate, v.VoucherNumber ?? "", "عكس سند صرف", 0m, v.Amount));
                }
            }
        }

        foreach (var c in await db.Cheques
            .Where(c => c.CompanyId == companyId && c.SupplierId == supplierId && c.Type == ChequeType.Issued)
            .ToListAsync())
        {
            if (IssuedChequePaymentStatuses.Contains(c.Status))
                rows.Add((c.ChequeDate, c.ChequeNumber, "شيك صادر", c.Amount, 0m));
            else if (c.Status == ChequeStatus.Cancelled)
            {
                rows.Add((c.ChequeDate, c.ChequeNumber, "شيك صادر", c.Amount, 0m));
                rows.Add((c.ChequeDate, c.ChequeNumber, "إلغاء شيك", 0m, c.Amount));
            }
        }

        foreach (var w in await new SubcontractSupplierRetentionQuery(db).StatementLinesForSupplierAsync(companyId, supplierId))
            rows.Add((w.Date, w.AgreementNumber, "محتجز باطن", w.Amount, 0m));

        var released = await new SubcontractSupplierRetentionQuery(db).ReleasedForSupplierAsync(companyId, supplierId);
        if (released > 0)
            rows.Add((DateOnly.FromDateTime(DateTime.Today), "RET-RELEASE", "إفراج محتجز باطن", 0m, released));

        var running = 0m;
        return rows
            .OrderBy(r => r.Date).ThenBy(r => r.Number)
            .Select(r =>
            {
                running += r.Credit - r.Debit;
                return new SupplierStatementLine(r.Date, r.Number, r.Type, r.Debit, r.Credit, running);
            })
            .ToList();
    }

    private async Task<decimal> PaymentsForSupplierAsync(long companyId, long supplierId)
    {
        var paid = await db.PaymentVouchers
            .Where(v => v.CompanyId == companyId && v.SupplierId == supplierId && v.Status == PaymentVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        paid += await db.Cheques
            .Where(c => c.CompanyId == companyId && c.SupplierId == supplierId
                        && c.Type == ChequeType.Issued
                        && IssuedChequePaymentStatuses.Contains(c.Status))
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        paid += await new SubcontractSupplierRetentionQuery(db).WithheldForSupplierAsync(companyId, supplierId);
        return paid;
    }

    private async Task AppendCancellationRowAsync(
        List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)> rows,
        DateOnly docDate, string number, string typeLabel,
        decimal debit, decimal credit, long? journalEntryId)
    {
        rows.Add((docDate, number, typeLabel, debit, credit));
        if (journalEntryId is not long jeId) return;

        var original = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == jeId);
        if (original?.ReversalEntryId is not long revId) return;

        var reversal = await db.JournalEntries.SingleAsync(e => e.Id == revId);
        rows.Add((reversal.EntryDate, number, $"إلغاء {typeLabel}", credit, debit));
    }
}
