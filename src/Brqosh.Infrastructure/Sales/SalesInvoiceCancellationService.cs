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

/// <summary>إلغاء فاتورة مبيعات مرحّلة — عكس القيد وإرجاع المخزون.</summary>
public class SalesInvoiceCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long invoiceId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var invoice = await db.SalesInvoices
                .SingleAsync(i => i.CompanyId == companyId && i.Id == invoiceId, token);

            if (invoice.Status != SalesInvoiceStatus.Posted)
                throw new AccountingException("لا يُلغى إلا فاتورة مرحّلة.");

            if (invoice.JournalEntryId is not long entryId)
                throw new AccountingException("الفاتورة بلا قيد محاسبي مرتبط.");

            var hasPostedCreditNotes = await db.CreditNotes.AnyAsync(n =>
                n.CompanyId == companyId && n.OriginalInvoiceId == invoiceId
                && n.Status == CreditNoteStatus.Posted, token);
            if (hasPostedCreditNotes)
                throw new AccountingException(
                    "لا يُلغى فاتورة لها إشعار دائن مرحّل — عكس الإشعار أولاً أو استخدم إشعار دائن لتصحيح الفاتورة.");

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            var sourceDoc = $"SalesInvoice:{invoice.InvoiceNumber}";
            var issueMovements = await db.StockMovements
                .Where(m => m.CompanyId == companyId && m.SourceDocument == sourceDoc && m.Status == StockMovementStatus.Posted)
                .ToListAsync(token);

            if (issueMovements.Count > 0)
            {
                var inv = new InventoryService(db);
                foreach (var movement in issueMovements)
                {
                    var returnLines = movement.Lines
                        .Select(l => (l.ProductId, l.Quantity, l.UnitCost))
                        .ToList();
                    await inv.PostDocumentMovementAsync(companyId, movement.WarehouseId,
                        StockMovementType.SalesReturn, cancellationDate,
                        $"إرجاع مخزون — إلغاء فاتورة {invoice.InvoiceNumber}",
                        sourceDoc, returnLines);
                }
            }

            invoice.MarkCancelled();
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء فاتورة مبيعات",
                "SalesInvoice", invoice.InvoiceNumber ?? invoice.Id.ToString(), "تم عكس القيد وإرجاع المخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "SalesInvoice", invoice.Id, invoice.InvoiceNumber ?? invoice.Id.ToString(),
                "إلغاء فاتورة مبيعات"), token);

            if (settings?.IsEnabled == true)
            {
                try
                {
                    var zatcaCancellation = await new ZatcaEInvoiceGenerator(db)
                        .TryGenerateCancellationForSalesInvoiceAsync(invoiceId);
                    if (zatcaCancellation is not null)
                        await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, zatcaCancellation, token);
                }
                catch (AccountingException)
                {
                    throw;
                }
            }
        }, ct);
    }
}
