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
            var products = await inv.LoadProductsForLinesAsync(
                receipt.CompanyId, receipt.Lines.Select(l => l.ProductId));

            var stockLines = new List<(long ProductId, decimal Qty, decimal UnitCost)>();
            decimal accruedValue = 0m;
            foreach (var line in receipt.Lines)
            {
                if (line.ProductId is not long pid)
                    continue;

                stockLines.Add((pid, line.ReceivedQuantity, line.UnitCost));

                // (CEEF-229 · م1-١٩) **القيدُ يُرشَّح بما هو مخزنيّ — والحركةُ لا تُرشَّح.**
                //
                // الدفترُ المساعد (`StockBalanceQuery`) يجمع الأصنافَ المخزنية وحدها، ومتوسطُ
                // التكلفة لا يُحسب لغيرها. فلو اشتُقّ المبلغُ من كلّ سطرٍ له صنف لَقُيّد
                // التزامٌ على خدمةٍ لا تُقفله الفاتورةُ أبداً — **رصيدٌ مفتوحٌ صامتٌ إلى الأبد**،
                // وكسرٌ دائمٌ للهوية هـ-٣.
                if (products.TryGetValue(pid, out var product) && product.IsStockable())
                    accruedValue += Math.Round(
                        line.ReceivedQuantity * line.UnitCost, 2, MidpointRounding.AwayFromZero);
            }

            var number = await DocumentNumberGenerator.NextAsync(db, 
                db.GoodsReceipts.Where(g => g.CompanyId == receipt.CompanyId).Select(g => g.ReceiptNumber),
                "GRN", receipt.ReceiptDate.Year, receipt.CompanyId);

            long? movementId = null;
            if (stockLines.Count > 0)
            {
                var movement = await inv.PostDocumentMovementAsync(
                    receipt.CompanyId, receipt.WarehouseId, StockMovementType.Receipt,
                    receipt.ReceiptDate, $"استلام بضاعة {number}", $"GoodsReceipt:{number}", stockLines);
                await db.SaveChangesAsync(token);
                movementId = movement.Id;
            }

            // (CEEF-229 · م1-١٩ / I-5) **استحقاقُ الاستلام — والقيدُ الذي لم يكن.**
            //
            // كان الاستلامُ عمليةَ دفترٍ مساعدٍ خالصة: يُحرّك المخزون ويضبط متوسطَ التكلفة
            // ولا يكتب قيداً. فبضاعةٌ تُستلم في 28 ديسمبر وتُفوتَر في 5 يناير **تغيب عن
            // ميزانية ديسمبر أصلاً والتزاماً معاً** — والقوائمُ تُقدَّم ناقصةً بقيمتها.
            //
            // ولا يكشفه ميزانُ المراجعة: **العدمُ متوازنٌ تماماً.**
            JournalEntry? accrual = null;
            if (accruedValue > 0)
            {
                var inventoryAccount = await SystemAccountAsync(
                    receipt.CompanyId, SaudiChartOfAccounts.SystemCodes.Inventory);
                var grniAccount = await PurchaseAccountProvisioning
                    .GoodsReceivedNotInvoicedAsync(db, receipt.CompanyId, token);

                accrual = JournalEntry.CreateDraft(
                    receipt.CompanyId, receipt.ReceiptDate, $"استلام بضاعة {number}",
                    $"GoodsReceipt:{number}");
                accrual.AddLine(inventoryAccount, accruedValue, 0m, "بضاعة مستلمة");
                accrual.AddLine(grniAccount, 0m, accruedValue, "استحقاق بضاعة غير مفوترة");
                accrual.Post(await new JournalEntryNumberGenerator(db)
                    .NextAsync(receipt.CompanyId, receipt.ReceiptDate, token));
                db.JournalEntries.Add(accrual);
            }

            receipt.MarkPosted(number, movementId);
            db.GoodsReceipts.Add(receipt);
            await db.SaveChangesAsync(token);

            if (accrual is not null)
            {
                receipt.LinkJournalEntry(accrual.Id);
                await db.SaveChangesAsync(token);
            }

            await FinancialPostingSupport.AuditPostAsync(
                db, receipt.CompanyId, "GoodsReceipt", number,
                reason: "ترحيل إذن استلام بضاعة", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                receipt.CompanyId, "GoodsReceipt", receipt.Id, number), token);

            return receipt;
        }, ct);
    }

    private async Task<Brqosh.Domain.Accounting.Account> SystemAccountAsync(long companyId, string code) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code);
}
