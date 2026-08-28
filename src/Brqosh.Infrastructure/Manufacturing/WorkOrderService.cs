using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Manufacturing;

public class WorkOrderService(BrqoshDbContext db)
{
    public async Task<WorkOrder> ReleaseAsync(long companyId, long workOrderId, CancellationToken ct = default) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            // (CEEF-229 · م2-١) جارتاها في هذا الملف محروستان (CompleteAsync بـ EnsureCanPost
            // و CancelAsync بـ EnsureCanCancelOrReverse) وهي وحدها سائبة — رغم أنها **تستهلك
            // رقماً من متسلسلة المستندات**، وفجوةُ الترقيم أثرٌ لا يُردم.
            SecurityContext.EnsureCanPost();

            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            var number = await DocumentNumberGenerator.NextAsync(
                db.WorkOrders.Where(w => w.CompanyId == companyId).Select(w => w.OrderNumber),
                "WO", order.OrderDate.Year);
            order.Release(number);
            await db.SaveChangesAsync(token);
            return order;
        }, ct);

    public async Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal producedQuantity,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == order.BomId, token);
            var inv = new InventoryService(db);
            var warehouse = await inv.GetDefaultWarehouseAsync(companyId);

            var scale = producedQuantity / bom.OutputQuantity;
            var issueLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();
            decimal materialCost = 0;
            var costing = new InventoryCostingService(db);

            foreach (var line in bom.Lines)
            {
                var qty = Math.Round(line.Quantity * scale, 4, MidpointRounding.AwayFromZero);
                var (totalCost, componentUnitCost) = await costing.ConsumeForIssueAsync(
                    companyId, line.ComponentProductId, warehouse.Id, qty, token);
                materialCost += totalCost;
                issueLines.Add((line.ComponentProductId, qty, componentUnitCost));
            }

            var unitCost = producedQuantity > 0
                ? Math.Round(materialCost / producedQuantity, 4, MidpointRounding.AwayFromZero)
                : 0m;

            await inv.PostDocumentMovementAsync(companyId, warehouse.Id, StockMovementType.Issue,
                order.OrderDate, $"صرف مواد — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}", issueLines);

            await inv.PostDocumentMovementAsync(companyId, warehouse.Id, StockMovementType.Receipt,
                order.OrderDate, $"إنتاج منتج — أمر {order.OrderNumber}", $"WorkOrder:{order.Id}",
                [(bom.FinishedProductId, producedQuantity, unitCost)]);

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, order.OrderDate);

            var wip = await AccountAsync(companyId, "1113", token);
            var inventory = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.Inventory, token);
            var entry = JournalEntry.CreateDraft(companyId, order.OrderDate,
                $"إكمال أمر إنتاج {order.OrderNumber}", $"WorkOrderComplete:{order.Id}");
            entry.AddLine(wip, materialCost, 0m, "تحميل WIP");
            entry.AddLine(inventory, 0m, materialCost, "صرف مواد للإنتاج");
            var entry2 = JournalEntry.CreateDraft(companyId, order.OrderDate,
                $"استلام منتج تام — {order.OrderNumber}", $"WorkOrderReceipt:{order.Id}");
            entry2.AddLine(inventory, materialCost, 0m, "منتج تام");
            entry2.AddLine(wip, 0m, materialCost, "إغلاق WIP");

            var num1 = await new JournalEntryNumberGenerator(db).NextAsync(companyId, order.OrderDate);
            entry.Post(num1);
            db.JournalEntries.Add(entry);

            var num2 = await new JournalEntryNumberGenerator(db).NextAsync(companyId, order.OrderDate);
            entry2.Post(num2);
            db.JournalEntries.Add(entry2);
            await db.SaveChangesAsync(token);

            order.Complete(producedQuantity);
            order.LinkCompletionJournal(entry2.Id);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "إكمال أمر إنتاج",
                "WorkOrder", order.OrderNumber ?? order.Id.ToString(), $"كمية: {producedQuantity:N2}");

            return order;
        }, ct);
    }

    public async Task CancelAsync(long companyId, long workOrderId, string username,
        DateOnly? cancellationDate = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, token);
            var cancelDate = cancellationDate ?? DateOnly.FromDateTime(DateTime.Today);

            if (order.Status == WorkOrderStatus.Completed)
            {
                await ReverseCompletedWorkOrderAsync(companyId, order, cancelDate, username, token);
                order.MarkCancelledAfterCompletion();
            }
            else
            {
                order.Cancel();
            }

            await db.SaveChangesAsync(token);
            await new AuditService(db).LogAsync(companyId, username, "إلغاء أمر إنتاج",
                "WorkOrder", order.OrderNumber ?? order.Id.ToString(),
                order.Status == WorkOrderStatus.Cancelled ? "تم الإلغاء مع عكس المخزون/القيود إن لزم" : "تم الإلغاء");
        }, ct);
    }

    private async Task ReverseCompletedWorkOrderAsync(
        long companyId, WorkOrder order, DateOnly cancelDate, string username, CancellationToken token)
    {
        await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, cancelDate);

        var sourcePrefix = $"WorkOrder:{order.Id}";
        var entryKeys = new[]
        {
            $"WorkOrderComplete:{order.Id}",
            $"WorkOrderReceipt:{order.Id}",
        };

        foreach (var key in entryKeys)
        {
            var entry = await db.JournalEntries.FirstOrDefaultAsync(e =>
                e.CompanyId == companyId
                && e.SourceDocument == key
                && e.Status == JournalEntryStatus.Posted, token);
            if (entry is null) continue;

            await new JournalReversalService(db).ReverseAsync(
                companyId, entry.Id, cancelDate, username, fromSourceDocument: true, token);
        }

        var movements = await db.StockMovements
            .Where(m => m.CompanyId == companyId
                        && m.SourceDocument == sourcePrefix
                        && m.Status == StockMovementStatus.Posted)
            .ToListAsync(token);

        if (movements.Count == 0)
            return;

        var inv = new InventoryService(db);
        foreach (var movement in movements.OrderByDescending(m => m.Id))
        {
            var lines = movement.Lines.Select(l => (l.ProductId, l.Quantity, l.UnitCost)).ToList();
            if (movement.Type == StockMovementType.Issue)
            {
                await inv.PostDocumentMovementAsync(companyId, movement.WarehouseId,
                    StockMovementType.Receipt, cancelDate,
                    $"عكس صرف مواد — إلغاء أمر {order.OrderNumber}",
                    sourcePrefix, lines);
            }
            else if (movement.Type == StockMovementType.Receipt)
            {
                await inv.PostDocumentMovementAsync(companyId, movement.WarehouseId,
                    StockMovementType.Issue, cancelDate,
                    $"عكس إنتاج — إلغاء أمر {order.OrderNumber}",
                    sourcePrefix, lines);
            }
        }
    }

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
