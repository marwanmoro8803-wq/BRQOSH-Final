using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// توزيع تكلفة إضافية (شحن/رسوم جمركية) على طبقات/متوسط تكلفة أصناف استلام.
/// القيد: مدين مخزون / دائن صندوق أو دائنون.
/// </summary>
public class LandedCostAllocationService(BrqoshDbContext db)
{
    public async Task AllocateAsync(
        long companyId, DateOnly costDate, string description, decimal totalAdditionalCost,
        IReadOnlyList<(long ProductId, long WarehouseId, decimal QtyShare)> shares,
        string username, bool creditCash = true, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        if (totalAdditionalCost <= 0)
            throw new AccountingException("التكلفة الإضافية يجب أن تكون موجبة.");
        if (shares.Count == 0)
            throw new AccountingException("حدّد أصنافاً لتوزيع التكلفة.");

        var shareSum = shares.Sum(s => s.QtyShare);
        if (shareSum <= 0)
            throw new AccountingException("مجموع حصص التوزيع يجب أن يكون موجباً.");

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, costDate);

            var method = await new InventoryCostingService(db).GetMethodAsync(companyId, token);
            decimal allocated = 0;
            var byProduct = new List<(long ProductId, decimal Amount)>();
            for (var i = 0; i < shares.Count; i++)
            {
                var share = shares[i];
                var amount = i == shares.Count - 1
                    ? totalAdditionalCost - allocated
                    : Math.Round(totalAdditionalCost * (share.QtyShare / shareSum), 2, MidpointRounding.AwayFromZero);
                allocated += amount;
                byProduct.Add((share.ProductId, amount));

                if (method == InventoryCostingMethod.Fifo)
                {
                    var layers = await db.InventoryCostLayers
                        .Where(l => l.CompanyId == companyId
                                    && l.ProductId == share.ProductId
                                    && l.WarehouseId == share.WarehouseId
                                    && l.RemainingQuantity > 0)
                        .OrderByDescending(l => l.ReceivedDate)
                        .ThenByDescending(l => l.Id)
                        .ToListAsync(token);
                    if (layers.Count == 0)
                        throw new AccountingException($"لا توجد طبقات FIFO للصنف {share.ProductId}.");
                    layers[0].AbsorbLandedCost(amount);
                    await new InventoryCostingService(db).RecalculateWacFromLayersAsync(companyId, share.ProductId, token);
                }
                else
                {
                    var product = await db.Products.SingleAsync(p => p.Id == share.ProductId && p.CompanyId == companyId, token);
                    var onHand = await new InventoryService(db).GetCompanyOnHandAsync(companyId, share.ProductId);
                    if (onHand <= 0)
                        throw new AccountingException($"لا رصيد للصنف {product.NameAr} لتوزيع التكلفة.");
                    var addPerUnit = Math.Round(amount / onHand, 4, MidpointRounding.AwayFromZero);
                    product.SetWeightedAverageCost(product.WeightedAverageCost + addPerUnit);
                }
            }

            var inventory = await db.Accounts.SingleAsync(a =>
                a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.Inventory, token);
            // ‏**التكلفةُ الإضافيّةُ غيرُ المدفوعة مصروفٌ مستحقّ
            // ‏لا ذمّةُ مورِّد.**
            //
            // ‏كانت تُدائن `2101` — **حسابَ مراقبة الموردين** — **بلا مورِّدٍ البتّة**.
            // ‏والهويّةُ هـ-٢ تقضي: `مجموعُ أرصدة الموردين == رصيدُ 2101`. فالدفترُ يزيد
            // ‏**والدفترُ المساعدُ لا يستطيع أن يزيد** — إذ لا طرفَ يُنسب إليه المبلغ —
            // ‏**فتنكسر الهويّةُ كسراً دائماً** لا يُصلحه شيءٌ بعده.
            //
            // ‏**ولا يشكو الميزان**: القيدُ متوازن. ويظهر العطلُ في **مطابقة كشف المورّد**
            // ‏وحدَها: إجماليُّ الذمم في الميزانية أعلى من مجموع ما على الموردين،
            // ‏**والفرقُ بلا اسمٍ يُسأل عنه**.
            //
            // ‏**والصوابُ أنّ شحناً أو جمركاً لم تَرِد فاتورتُه بعدُ التزامٌ مستحقٌّ لا
            // ‏ذمّةُ مورِّد**: يُدائن `2104` «مصروفات مستحقة» — وهو في الدليل منذ نشأته
            // ‏**ولا يكتبه أحد** — فإذا وردت الفاتورةُ نُقل إلى ذمّة صاحبها بمستندها.
            var creditAccount = creditCash
                ? await db.Accounts.SingleAsync(a =>
                    a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand, token)
                : await db.Accounts.SingleAsync(a =>
                    a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccruedExpenses, token);

            var je = JournalEntry.CreateDraft(companyId, costDate, $"تكلفة إضافية — {description}",
                sourceDocument: $"LandedCost:{costDate:yyyyMMdd}");
            je.AddLine(inventory, totalAdditionalCost, 0m, description);
            je.AddLine(creditAccount, 0m, totalAdditionalCost, "مقابل تكلفة إضافية");
            await ItemAccountResolver.RerouteAsync(db, companyId, je, inventory, byProduct, a => a.Inventory, debitSide: true, token);
            var num = await new JournalEntryNumberGenerator(db).NextAsync(companyId, costDate, token);
            je.Post(num);
            db.JournalEntries.Add(je);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "توزيع تكلفة إضافية",
                "Inventory", string.Join(",", shares.Select(s => s.ProductId)), $"{totalAdditionalCost:N2}");
        }, ct);
    }
}
