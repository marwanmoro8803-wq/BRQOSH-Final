using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

/// <summary>إلغاء إشعار مدين مرحّل — عكس القيد وإعادة المخزون المصروف.</summary>
public class DebitNoteCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long noteId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var note = await db.DebitNotes
                .SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, token);

            if (note.Status != DebitNoteStatus.Posted)
                throw new AccountingException("لا يُلغى إلا إشعار مدين مرحّل.");

            if (note.JournalEntryId is not long entryId)
                throw new AccountingException("الإشعار بلا قيد محاسبي مرتبط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            var sourceDoc = $"DebitNote:{note.NoteNumber}";
            var issueMovements = await db.StockMovements
                .Include(m => m.Lines)
                .Where(m => m.CompanyId == companyId
                            && m.SourceDocument == sourceDoc
                            && m.Status == StockMovementStatus.Posted
                            && m.Type == StockMovementType.Issue)
                .ToListAsync(token);

            if (issueMovements.Count > 0)
            {
                // (جاهزيّةُ الإطلاق · ح-٣١) ‏**يُعكَس الأصلُ — لا تُرحّل حركةٌ مضادّة.**
                //
                // ‏كان يُرحّل **وارِداً مقابلاً**، وهو **يُنشئ طبقةً جديدةً في ذيل
                // ‏طابور الوارد-أوّلاً** بدل أن يُرمّم الطبقةَ التي أُخِذ منها **بعينها**.
                // ‏**فالرصيدُ يعود والتكلفةُ لا تعود**، وتخرج الصرفةُ التاليةُ بتكلفةٍ
                // ‏ليست تكلفتَها.
                //
                // ‏**والمسارُ الصحيحُ قائمٌ وموثّق** (CEEF-229 · م0-٣)، **وستُّ خدماتِ
                // ‏إلغاءٍ تستعملُه — وهذه لم تكن منها**، كما لم يكن أمرُ الإنتاج (ح-٣٠).
                var inv = new InventoryService(db);
                foreach (var movement in issueMovements)
                {
                    await inv.ReverseDocumentMovementAsync(movement);
                    movement.MarkCancelled();
                }
            }

            note.MarkCancelled(cancellationDate);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء إشعار مدين",
                "DebitNote", note.NoteNumber ?? note.Id.ToString(), "تم عكس القيد والمخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "DebitNote", note.Id, note.NoteNumber ?? note.Id.ToString(),
                "إلغاء إشعار مدين"), token);
        }, ct);
    }
}
