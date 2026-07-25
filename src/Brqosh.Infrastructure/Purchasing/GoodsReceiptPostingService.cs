using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

/// <summary>ترحيل إذن استلام بضاعة من أمر شراء معتمد — يحرّك المخزون فقط (بدون AP).</summary>
public class GoodsReceiptPostingService(BrqoshDbContext db)
{
    public async Task<GoodsReceipt> PostAsync(GoodsReceipt receipt, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await FinancialPostingSupport.ValidateAsync(
                db, receipt.CompanyId, receipt.ReceiptDate, PostingDocumentKind.GoodsReceipt, ct: token);

            if (receipt.BranchId is null)
                receipt.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(receipt.CompanyId, BranchSessionContext.PreferredBranchId, token));

            var po = await db.PurchaseOrders
                .SingleAsync(p => p.Id == receipt.PurchaseOrderId && p.CompanyId == receipt.CompanyId, token);
            if (po.Status != PurchaseOrderStatus.Approved)
                throw new AccountingException("لا يُستلم إلا على أمر شراء معتمد.");
            if (po.SupplierId != receipt.SupplierId)
                throw new AccountingException("مورد الإذن لا يطابق أمر الشراء.");

            var inv = new InventoryService(db);
            var stockLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();
            foreach (var line in receipt.Lines)
            {
                if (line.ProductId is long pid)
                    stockLines.Add((pid, line.ReceivedQuantity, line.UnitCost));
            }

            var number = await DocumentNumberGenerator.NextAsync(
                db.GoodsReceipts.Where(g => g.CompanyId == receipt.CompanyId).Select(g => g.ReceiptNumber),
                "GRN", receipt.ReceiptDate.Year);

            long? movementId = null;
            if (stockLines.Count > 0)
            {
                var movement = await inv.PostDocumentMovementAsync(
                    receipt.CompanyId, receipt.WarehouseId, StockMovementType.Receipt,
                    receipt.ReceiptDate, $"استلام بضاعة {number}", $"GoodsReceipt:{number}", stockLines);
                await db.SaveChangesAsync(token);
                movementId = movement.Id;
            }

            receipt.MarkPosted(number, movementId);
            db.GoodsReceipts.Add(receipt);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, receipt.CompanyId, "GoodsReceipt", number,
                reason: "ترحيل إذن استلام بضاعة", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                receipt.CompanyId, "GoodsReceipt", receipt.Id, number), token);

            return receipt;
        }, ct);
    }
}
