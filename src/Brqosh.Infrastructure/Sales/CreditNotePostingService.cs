using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>
/// ترحيل إشعار الدائن: يتحقق أن قيمته لا تتجاوز رصيد الفاتورة الأصلية
/// (الفاتورة ناقص إشعارات سابقة)، ثم يولّد القيد ويحفظ الكل في معاملة واحدة.
/// </summary>
public class CreditNotePostingService(BrqoshDbContext db)
{
    public async Task<CreditNote> PostAsync(CreditNote note)
    {
        var posting = new JournalPostingService(db);
        await posting.EnsurePeriodOpenAsync(note.CompanyId, note.NoteDate);

        var invoice = await db.SalesInvoices
            .FirstOrDefaultAsync(i => i.Id == note.OriginalInvoiceId && i.CompanyId == note.CompanyId)
            ?? throw new AccountingException("الفاتورة الأصلية غير موجودة.");
        if (invoice.Status != SalesInvoiceStatus.Posted)
            throw new AccountingException("لا يصدر إشعار دائن إلا على فاتورة مرحّلة.");
        if (invoice.CustomerId != note.CustomerId)
            throw new AccountingException("عميل الإشعار لا يطابق عميل الفاتورة الأصلية.");

        var previousCredits = await db.CreditNotes
            .Where(n => n.CompanyId == note.CompanyId
                        && n.OriginalInvoiceId == note.OriginalInvoiceId
                        && n.Status == CreditNoteStatus.Posted)
            .ToListAsync();
        var remaining = invoice.GrandTotal - previousCredits.Sum(n => n.GrandTotal);
        if (note.GrandTotal > remaining)
            throw new AccountingException(
                $"قيمة الإشعار {note.GrandTotal:N2} تتجاوز المتبقي من الفاتورة {remaining:N2}.");

        var salesReturns = await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.SalesReturns);
        var vatOutput = await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.VatOutput);
        var receivable = await SystemAccountAsync(note.CompanyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

        var noteNumber = await DocumentNumberGenerator.NextAsync(
            db.CreditNotes.Where(n => n.CompanyId == note.CompanyId).Select(n => n.NoteNumber),
            "CN", note.NoteDate.Year);

        var entry = note.Post(noteNumber, salesReturns, vatOutput, receivable);
        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(note.CompanyId, note.NoteDate);
        entry.Post(entryNumber);

        db.JournalEntries.Add(entry);
        db.CreditNotes.Add(note);
        await db.SaveChangesAsync();

        note.LinkJournalEntry(entry.Id);
        await db.SaveChangesAsync();
        return note;
    }

    private async Task<Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
