using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// محرك التكلفة: WAC أو FIFO حسب إعداد المنشأة.
/// عند FIFO: الاستلام يُنشئ طبقة؛ الصرف يستهلك أقدم الطبقات.
/// يُحدَّث WeightedAverageCost دائماً كمرجع عرض حتى في وضع FIFO.
/// </summary>
public class InventoryCostingService(BrqoshDbContext db)
{
    public async Task<InventoryCostingMethod> GetMethodAsync(long companyId, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        return company.CostingMethod;
    }

    public async Task OnReceiptAsync(
        long companyId, long productId, long warehouseId, DateOnly date,
        decimal qty, decimal unitCost, string sourceDocument, CancellationToken ct = default)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId && p.CompanyId == companyId, ct);
        if (!product.IsStockable()) return;

        var method = await GetMethodAsync(companyId, ct);
        if (method == InventoryCostingMethod.Fifo)
        {
            db.InventoryCostLayers.Add(InventoryCostLayer.Create(
                companyId, productId, warehouseId, date, qty, unitCost, sourceDocument));
        }

        await new InventoryService(db).ApplyReceiptCostAsync(companyId, productId, qty, unitCost);
    }

    /// <summary>
    /// يستهلك تكلفة كمية صادرة ويعيد التكلفة الفعلية للوحدة المستخدمة في القيد.
    /// في FIFO يحدّث الطبقات؛ في WAC يقرأ المتوسط الحالي فقط.
    /// </summary>
    public async Task<(decimal TotalCost, decimal UnitCost)> ConsumeForIssueAsync(
        long companyId, long productId, long warehouseId, decimal qty, CancellationToken ct = default)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId && p.CompanyId == companyId, ct);
        if (!product.IsStockable())
            return (0m, 0m);

        var method = await GetMethodAsync(companyId, ct);
        if (method == InventoryCostingMethod.WeightedAverage)
        {
            await db.Entry(product).ReloadAsync(ct);
            var total = Math.Round(qty * product.WeightedAverageCost, 2, MidpointRounding.AwayFromZero);
            return (total, product.WeightedAverageCost);
        }

        return await ConsumeFifoAsync(companyId, productId, warehouseId, qty, ct);
    }

    public async Task OnSalesReturnAsync(
        long companyId, long productId, long warehouseId, DateOnly date,
        decimal qty, decimal unitCost, string sourceDocument, CancellationToken ct = default)
    {
        // المرتجع يُعامل كاستلام جديد (طبقة جديدة أو تحديث WAC)
        await OnReceiptAsync(companyId, productId, warehouseId, date, qty, unitCost, sourceDocument, ct);
    }

    /// <summary>يعيد بناء متوسط التكلفة من الطبقات المتبقية (بعد عكس FIFO).</summary>
    public async Task RecalculateWacFromLayersAsync(long companyId, long productId, CancellationToken ct = default)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId && p.CompanyId == companyId, ct);
        if (!product.IsStockable()) return;

        var layers = await db.InventoryCostLayers
            .Where(l => l.CompanyId == companyId && l.ProductId == productId && l.RemainingQuantity > 0)
            .ToListAsync(ct);

        if (layers.Count == 0)
        {
            product.SetWeightedAverageCost(0m);
            return;
        }

        var totalQty = layers.Sum(l => l.RemainingQuantity);
        var totalValue = layers.Sum(l => l.RemainingQuantity * l.UnitCost);
        var wac = totalQty == 0
            ? 0m
            : Math.Round(totalValue / totalQty, 4, MidpointRounding.AwayFromZero);
        product.SetWeightedAverageCost(wac);
    }

    private async Task<(decimal TotalCost, decimal UnitCost)> ConsumeFifoAsync(
        long companyId, long productId, long warehouseId, decimal qty, CancellationToken ct)
    {
        var layers = await db.InventoryCostLayers
            .Where(l => l.CompanyId == companyId
                        && l.ProductId == productId
                        && l.WarehouseId == warehouseId
                        && l.RemainingQuantity > 0)
            .OrderBy(l => l.ReceivedDate)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);

        // إن نقصت طبقات المستودع، استهلك من أي مستودع بنفس الترتيب الزمني
        if (layers.Sum(l => l.RemainingQuantity) < qty)
        {
            layers = await db.InventoryCostLayers
                .Where(l => l.CompanyId == companyId
                            && l.ProductId == productId
                            && l.RemainingQuantity > 0)
                .OrderBy(l => l.ReceivedDate)
                .ThenBy(l => l.Id)
                .ToListAsync(ct);
        }

        var available = layers.Sum(l => l.RemainingQuantity);
        if (available + 0.0001m < qty)
        {
            var p = await db.Products.SingleAsync(x => x.Id == productId, ct);
            throw new AccountingException(
                $"طبقات FIFO للصنف '{p.NameAr}' غير كافية — المتبقي {available:N4}، المطلوب {qty:N4}.");
        }

        decimal remaining = qty;
        decimal totalCost = 0m;
        foreach (var layer in layers)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, layer.RemainingQuantity);
            totalCost += layer.Consume(take);
            remaining = Math.Round(remaining - take, 4, MidpointRounding.AwayFromZero);
        }

        var unit = qty == 0 ? 0m : Math.Round(totalCost / qty, 4, MidpointRounding.AwayFromZero);

        // حافظ على WAC متسقاً مع الطبقات المتبقية
        await RecalculateWacFromLayersAsync(companyId, productId, ct);
        return (totalCost, unit);
    }
}
