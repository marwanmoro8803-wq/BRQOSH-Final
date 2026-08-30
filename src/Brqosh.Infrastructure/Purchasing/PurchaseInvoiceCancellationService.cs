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

/// <summary>إلغاء فاتورة مشتريات مرحّلة — عكس القيد وعكس إدخال المخزون.</summary>
public class PurchaseInvoiceCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long invoiceId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var invoice = await db.PurchaseInvoices
                .SingleAsync(i => i.CompanyId == companyId && i.Id == invoiceId, token);

            if (invoice.Status != PurchaseInvoiceStatus.Posted)
                throw new AccountingException("لا يُلغى إلا فاتورة مرحّلة.");

            if (invoice.JournalEntryId is not long entryId)
                throw new AccountingException("الفاتورة بلا قيد محاسبي مرتبط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            var sourceDoc = $"PurchaseInvoice:{invoice.InvoiceNumber}";
            var receiptMovements = await db.StockMovements
                .Include(m => m.Lines)
                .Where(m => m.CompanyId == companyId && m.SourceDocument == sourceDoc && m.Status == StockMovementStatus.Posted)
                .ToListAsync(token);

            if (receiptMovements.Count > 0)
            {
                var inv = new InventoryService(db);
                foreach (var movement in receiptMovements)
                {
                    // (CEEF-229 · م0-٣ج) **يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة.**
                    //
                    // الحركةُ الصادرة عبر `PostDocumentMovementAsync` **تُنقص الرصيدَ ولا
                    // تمسّ طبقةً** — فالتكلفةُ مسؤوليةُ المستدعي بالتصميم، وهذه الخدمةُ
                    // كانت تستدعي `ReverseReceiptCostAsync` (حسابُ متوسطٍ لا طبقات).
                    //
                    // فتبقى طبقةُ الشراء الملغى كاملةً وينشقّ المجموعُ عن الرصيد. وقيس:
                    // **الطبقات 250 والرصيد 200**. وهو نمطُ م0-٣ وم0-٣ب في جانب الشراء.
                    await inv.ReverseDocumentMovementAsync(movement);
                    movement.MarkCancelled();
                }
            }

            invoice.MarkCancelled(cancellationDate);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء فاتورة مشتريات",
                "PurchaseInvoice", invoice.InvoiceNumber ?? invoice.Id.ToString(), "تم عكس القيد والمخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber ?? invoice.Id.ToString(),
                "إلغاء فاتورة مشتريات"), token);
        }, ct);
    }
}
