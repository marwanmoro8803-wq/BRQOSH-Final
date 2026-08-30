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

/// <summary>إلغاء إذن استلام مرحّل — عكس المخزون وطبقات التكلفة (لا قيد AP لأن GRN بلا GL).</summary>
public class GoodsReceiptCancellationService(BrqoshDbContext db)
{
    public async Task CancelAsync(long companyId, long receiptId, DateOnly cancellationDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var receipt = await db.GoodsReceipts
                .SingleAsync(r => r.CompanyId == companyId && r.Id == receiptId, token);

            if (receipt.Status != GoodsReceiptStatus.Posted)
                throw new AccountingException("لا يُلغى إلا إذن استلام مرحّل.");

            // منع الإلغاء إن وُجدت فاتورة مشتريات مربوطة بأمر الشراء وما زالت مرحّلة
            var hasOpenPi = await db.PurchaseInvoices.AnyAsync(i =>
                i.CompanyId == companyId
                && i.PurchaseOrderId == receipt.PurchaseOrderId
                && i.Status == PurchaseInvoiceStatus.Posted, token);
            if (hasOpenPi)
                throw new AccountingException(
                    "لا يُلغى الاستلام لأن فاتورة مشتريات مرحّلة مرتبطة بأمر الشراء. ألغِ الفاتورة أولاً.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancellationDate);

            // (CEEF-229 · م1-١٩) **يُعكَس قيدُ الاستحقاق** — وإلا بقي التزامُ «بضاعة مستلمة
            // غير مفوترة» خالداً على الميزانية عن بضاعةٍ أُعيدت إلى المورد.
            //
            // **ويُرسى بالمعرّف لا بالمصدر:** عكسُ القيد ينسخ `SourceDocument` نفسَه، وهذه
            // الخدمةُ تستعمل السلسلةَ عينَها لحركتها المخزنية — فالبحثُ بالمصدر ملتبسٌ ببنائه.
            if (receipt.JournalEntryId is long accrualEntryId)
            {
                if (cancellationDate < receipt.ReceiptDate)
                    throw new AccountingException(
                        $"تاريخ الإلغاء ({cancellationDate:yyyy-MM-dd}) قبل تاريخ الاستلام "
                        + $"({receipt.ReceiptDate:yyyy-MM-dd}) — ولا يُعكَس قيدٌ بتاريخٍ يسبقه.");

                await new JournalReversalService(db).ReverseAsync(
                    companyId, accrualEntryId, cancellationDate, username, fromSourceDocument: true, token);
            }

            var sourceDoc = $"GoodsReceipt:{receipt.ReceiptNumber}";
            var movements = await db.StockMovements
                .Include(m => m.Lines)
                .Where(m => m.CompanyId == companyId
                            && m.SourceDocument == sourceDoc
                            && m.Status == StockMovementStatus.Posted)
                .ToListAsync(token);

            if (movements.Count == 0 && receipt.StockMovementId is long mid)
            {
                var byId = await db.StockMovements.Include(m => m.Lines)
                    .SingleOrDefaultAsync(m => m.Id == mid && m.CompanyId == companyId, token);
                if (byId is not null && byId.Status == StockMovementStatus.Posted)
                    movements.Add(byId);
            }

            if (movements.Count > 0)
            {
                var inv = new InventoryService(db);
                foreach (var movement in movements)
                {
                    var outLines = movement.Lines
                        .Select(l => (l.ProductId, l.Quantity, l.UnitCost))
                        .ToList();
                    await inv.PostDocumentMovementAsync(companyId, movement.WarehouseId,
                        StockMovementType.Issue, cancellationDate,
                        $"عكس مخزون — إلغاء استلام {receipt.ReceiptNumber}",
                        sourceDoc, outLines);
                    foreach (var line in outLines)
                        await inv.ReverseReceiptCostAsync(companyId, line.ProductId, line.Quantity, line.UnitCost);
                }
            }

            receipt.MarkCancelled();
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إلغاء إذن استلام",
                "GoodsReceipt", receipt.ReceiptNumber ?? receipt.Id.ToString(), "تم عكس المخزون");

            await DomainEventHub.PublishAsync(new DocumentCancelledEvent(
                companyId, "GoodsReceipt", receipt.Id, receipt.ReceiptNumber ?? receipt.Id.ToString(),
                "إلغاء إذن استلام"), token);
        }, ct);
    }
}
