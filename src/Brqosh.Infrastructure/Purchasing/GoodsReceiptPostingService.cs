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
public class GoodsReceiptPostingService(BrqoshDbContext db) : Brqosh.Application.Accounting.IGoodsReceiptPostingService
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

            var stockLines = new List<(long ProductId, decimal Qty, decimal UnitCost,
                string? BatchNumber, DateOnly? ExpiryDate)>();
            decimal accruedValue = 0m;
            var accruedByProduct = new List<(long ProductId, decimal Amount)>();
            foreach (var line in receipt.Lines)
            {
                if (line.ProductId is not long pid)
                    continue;

                // (CEEF-230 · ط1-١) ‏**التحويلُ هنا وحدَه** — عند حدود المستند.
                // ‏فما يدخل المخزونَ بوحدة الصنف أبداً، ولا يحمل رقمٌ أعمقَ من هذا الحدّ
                // ‏وحدةً مختلفة. والتكلفةُ تُقسَم بالمعامل نفسِه: **كرتونٌ بمئة ريالٍ
                // ‏اثنتا عشرة حبّةً بـ8.33** — وإلا صارت الحبّةُ بمئة والمخزونُ منتفخاً
                // ‏اثنَي عشر ضعفاً.
                var baseQty = await new UnitConversionService(db).ToProductUnitAsync(
                    receipt.CompanyId, products[pid], line.ReceivedQuantity, line.UnitCode, token);
                var baseCost = baseQty == 0m
                    ? line.UnitCost
                    : Math.Round(line.ReceivedQuantity * line.UnitCost / baseQty, 4,
                        MidpointRounding.AwayFromZero);

                stockLines.Add((pid, baseQty, baseCost, line.BatchNumber, line.ExpiryDate));

                // (CEEF-229 · م1-١٩) **القيدُ يُرشَّح بما هو مخزنيّ — والحركةُ لا تُرشَّح.**
                //
                // الدفترُ المساعد (`StockBalanceQuery`) يجمع الأصنافَ المخزنية وحدها، ومتوسطُ
                // التكلفة لا يُحسب لغيرها. فلو اشتُقّ المبلغُ من كلّ سطرٍ له صنف لَقُيّد
                // التزامٌ على خدمةٍ لا تُقفله الفاتورةُ أبداً — **رصيدٌ مفتوحٌ صامتٌ إلى الأبد**،
                // وكسرٌ دائمٌ للهوية هـ-٣.
                if (products.TryGetValue(pid, out var product) && product.IsStockable())
                {
                    var value = Math.Round(line.ReceivedQuantity * line.UnitCost, 2, MidpointRounding.AwayFromZero);
                    accruedValue += value;
                    accruedByProduct.Add((pid, value));
                }
            }

            var number = await DocumentNumberGenerator.NextAsync(db, 
                db.GoodsReceipts.Where(g => g.CompanyId == receipt.CompanyId).Select(g => g.ReceiptNumber),
                "GRN", receipt.ReceiptDate, receipt.CompanyId);

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
                await ItemAccountResolver.RerouteAsync(db, receipt.CompanyId, accrual, inventoryAccount,
                    accruedByProduct, a => a.Inventory, debitSide: true, token);
                accrual.Post(await new JournalEntryNumberGenerator(db)
                    .NextAsync(receipt.CompanyId, receipt.ReceiptDate, token));
                db.JournalEntries.Add(accrual);
            }

            receipt.MarkPosted(number, movementId);
            // (CEEF-230 · ط2-٠) ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
            // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
            // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
            // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
            if (receipt.Id == 0)
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
