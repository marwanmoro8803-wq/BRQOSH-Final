using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>عكس قيد مرحّل — الطريقة الوحيدة لتصحيح قيد دون حذف.</summary>
public class JournalReversalService(BrqoshDbContext db)
{
    public Task<JournalEntry> ReverseAsync(
        long companyId, long entryId, DateOnly reversalDate, string username, CancellationToken ct = default)
        => ReverseAsync(companyId, entryId, reversalDate, username, fromSourceDocument: false, ct);

    public async Task<JournalEntry> ReverseAsync(
        long companyId, long entryId, DateOnly reversalDate, string username,
        bool fromSourceDocument, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            if (fromSourceDocument)
                SecurityContext.EnsureCanCancelOrReverse();
            else
                SecurityContext.EnsureCanPostAccounting();

            username = SecurityContext.ResolveUsername(username);

            var original = await db.JournalEntries
                .SingleAsync(e => e.Id == entryId && e.CompanyId == companyId, token);

            if (original.Status != JournalEntryStatus.Posted)
                throw new AccountingException("لا يُعكس إلا قيد مرحّل.");

            if (!fromSourceDocument && original.SourceDocument is { Length: > 0 })
            {
                throw new AccountingException(
                    "هذا القيد مرتبط بمستند نظام — يُصحَّح من المستند المصدر (فاتورة، سند، حركة مخزون...) وليس بالعكس المباشر.");
            }

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, reversalDate);

            var reversal = original.CreateReversal(reversalDate);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(companyId, reversalDate);
            reversal.Post(entryNumber);

            db.JournalEntries.Add(reversal);
            await db.SaveChangesAsync(token);

            original.LinkReversalEntry(reversal.Id);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "عكس قيد",
                "JournalEntry", original.EntryNumber ?? original.Id.ToString(),
                $"قيد عكسي: {reversal.EntryNumber}");

            return reversal;
        }, ct);
}
