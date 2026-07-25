using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>إلغاء إشعار دائن مرحّل — عكس القيد وعكس إرجاع المخزون.</summary>
public class CreditNoteCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long noteId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var note = await db.CreditNotes
                .SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, token);

            if (note.Status != CreditNoteStatus.Posted)
                throw new AccountingException("لا يُلغى إلا إشعار دائن مرحّل.");

            if (note.JournalEntryId is not long entryId)
                throw new AccountingException("الإشعار بلا قيد محاسبي مرتبط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            var sourceDoc = $"CreditNote:{note.NoteNumber}";
            var returnMovements = await db.StockMovements
                .Include(m => m.Lines)
                .Where(m => m.CompanyId == companyId && m.SourceDocument == sourceDoc && m.Status == StockMovementStatus.Posted)
                .ToListAsync(token);

            if (returnMovements.Count > 0)
            {
                var inv = new InventoryService(db);
                foreach (var movement in returnMovements)
                {
                    var issueLines = movement.Lines
                        .Select(l => (l.ProductId, l.Quantity, l.UnitCost))
                        .ToList();
                    await inv.PostDocumentMovementAsync(companyId, movement.WarehouseId,
                        StockMovementType.Issue, cancellationDate,
                        $"عكس مخزون — إلغاء إشعار دائن {note.NoteNumber}",
                        sourceDoc, issueLines);
                }
            }

            note.MarkCancelled(cancellationDate);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء إشعار دائن",
                "CreditNote", note.NoteNumber ?? note.Id.ToString(), "تم عكس القيد والمخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "CreditNote", note.Id, note.NoteNumber ?? note.Id.ToString(),
                "إلغاء إشعار دائن"), token);
        }, ct);

        if (settings?.IsEnabled == true)
        {
            try
            {
                var zatcaDoc = await new ZatcaEInvoiceGenerator(db)
                    .TryGenerateCancellationForCreditNoteAsync(noteId);
                if (zatcaDoc is not null)
                    await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, zatcaDoc, ct);
            }
            catch (AccountingException) { /* لا نُوقف الإلغاء */ }
        }
    }
}
