using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public record CustomerBalance(long CustomerId, decimal Invoiced, decimal Credited, decimal Received)
{
    /// <summary>الرصيد المدين المستحق على العميل.</summary>
    public decimal Balance => Invoiced - Credited - Received;
}

public record CustomerStatementLine(DateOnly Date, string DocumentNumber, string DocumentType,
    decimal Debit, decimal Credit, decimal RunningBalance);

/// <summary>
/// دفتر مساعد للعملاء: الرصيد وكشف الحساب يُبنيان من المستندات المرحّلة
/// (فواتير − إشعارات دائنة − سندات قبض) — التجميع في قاعدة البيانات لا في الذاكرة.
/// </summary>
public class CustomerBalanceQuery(BrqoshDbContext db)
{
    public async Task<CustomerBalance> ForCustomerAsync(long companyId, long customerId)
    {
        var invoiced = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.CustomerId == customerId && i.Status == SalesInvoiceStatus.Posted)
            .ToListAsync();
        var credited = await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.CustomerId == customerId && n.Status == CreditNoteStatus.Posted)
            .ToListAsync();
        var received = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.CustomerId == customerId && v.Status == ReceiptVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        return new CustomerBalance(customerId,
            invoiced.Sum(i => i.GrandTotal),
            credited.Sum(n => n.GrandTotal),
            received);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> AllBalancesAsync(long companyId)
    {
        var invoiced = (await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted)
            .ToListAsync())
            .GroupBy(i => i.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.GrandTotal));

        var credited = (await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.Status == CreditNoteStatus.Posted)
            .ToListAsync())
            .GroupBy(n => n.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(n => n.GrandTotal));

        var received = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.Status == ReceiptVoucherStatus.Posted)
            .GroupBy(v => v.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(v => v.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var all = invoiced.Keys.Union(credited.Keys).Union(received.Keys);
        return all.ToDictionary(id => id,
            id => invoiced.GetValueOrDefault(id) - credited.GetValueOrDefault(id) - received.GetValueOrDefault(id));
    }

    /// <summary>كشف حساب العميل زمنياً مع رصيد متحرك.</summary>
    public async Task<IReadOnlyList<CustomerStatementLine>> StatementAsync(long companyId, long customerId)
    {
        var rows = new List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)>();

        foreach (var i in await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.CustomerId == customerId && i.Status == SalesInvoiceStatus.Posted)
            .ToListAsync())
            rows.Add((i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مبيعات", i.GrandTotal, 0m));

        foreach (var n in await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.CustomerId == customerId && n.Status == CreditNoteStatus.Posted)
            .ToListAsync())
            rows.Add((n.NoteDate, n.NoteNumber ?? "", "إشعار دائن", 0m, n.GrandTotal));

        foreach (var v in await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.CustomerId == customerId && v.Status == ReceiptVoucherStatus.Posted)
            .ToListAsync())
            rows.Add((v.VoucherDate, v.VoucherNumber ?? "", "سند قبض", 0m, v.Amount));

        var running = 0m;
        return rows
            .OrderBy(r => r.Date).ThenBy(r => r.Number)
            .Select(r =>
            {
                running += r.Debit - r.Credit;
                return new CustomerStatementLine(r.Date, r.Number, r.Type, r.Debit, r.Credit, running);
            })
            .ToList();
    }
}
