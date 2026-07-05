using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>
/// ترحيل فاتورة المبيعات: رقم تسلسلي INV-{سنة}-{تسلسل}، قيد آلي على الحسابات النظامية،
/// وحفظ الفاتورة والقيد في معاملة واحدة.
/// </summary>
public class SalesInvoicePostingService(BrqoshDbContext db)
{
    public async Task<SalesInvoice> PostAsync(SalesInvoice invoice)
    {
        var posting = new JournalPostingService(db);
        await posting.EnsurePeriodOpenAsync(invoice.CompanyId, invoice.InvoiceDate);

        var receivable = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
        var revenue = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.SalesRevenue);
        var vatOutput = await SystemAccountAsync(invoice.CompanyId, SaudiChartOfAccounts.SystemCodes.VatOutput);

        var invoiceNumber = await NextInvoiceNumberAsync(invoice.CompanyId, invoice.InvoiceDate);
        var entry = invoice.Post(invoiceNumber, receivable, revenue, vatOutput);

        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(invoice.CompanyId, invoice.InvoiceDate);
        entry.Post(entryNumber);

        db.JournalEntries.Add(entry);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.LinkJournalEntry(entry.Id);
        await db.SaveChangesAsync();
        return invoice;
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code)
        ?? throw new AccountingException($"الحساب النظامي {code} غير موجود — دليل الحسابات غير مكتمل.");

    private async Task<string> NextInvoiceNumberAsync(long companyId, DateOnly date)
    {
        var prefix = $"INV-{date.Year}-";
        var last = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.InvoiceNumber != null && i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last is not null && int.TryParse(last[prefix.Length..], out var seq))
            next = seq + 1;
        return $"{prefix}{next:D6}";
    }
}
