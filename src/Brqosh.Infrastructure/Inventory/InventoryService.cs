using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// محرك المخزون: أرصدة مجمّعة، متوسط مرجح، وحركات — يُحدَّث الرصيد داخل معاملة الترحيل.
/// </summary>
public class InventoryService(BrqoshDbContext db)
{
    public async Task<Warehouse> GetDefaultWarehouseAsync(long companyId)
    {
        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsDefault && w.IsActive);
        warehouse ??= await db.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsActive);
        if (warehouse is null)
        {
            warehouse = Warehouse.Create(companyId, "WH-001", "المستودع الرئيسي", isDefault: true);
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync();
        }
        return warehouse;
    }

    public async Task<decimal> GetOnHandAsync(long companyId, long productId, long warehouseId)
    {
        var balance = await FindBalanceAsync(companyId, warehouseId, productId);
        return balance?.QuantityOnHand ?? 0m;
    }

    public async Task<decimal> GetCompanyOnHandAsync(long companyId, long productId)
    {
        var dbBalances = await db.InventoryBalances
            .Where(b => b.CompanyId == companyId && b.ProductId == productId)
            .ToListAsync();
        var dbById = dbBalances.ToDictionary(b => b.Id);
        var total = dbBalances.Sum(b => b.QuantityOnHand);

        foreach (var local in db.InventoryBalances.Local
                     .Where(b => b.CompanyId == companyId && b.ProductId == productId))
        {
            if (local.Id == 0)
                total += local.QuantityOnHand;
            else if (dbById.TryGetValue(local.Id, out var dbBal))
                total += local.QuantityOnHand - dbBal.QuantityOnHand;
        }

        return total;
    }

    /// <summary>عكس أثر إدخال مخزون على متوسط التكلفة المرجح عند الإلغاء أو الصرف العكسي.</summary>
    public async Task ReverseReceiptCostAsync(long companyId, long productId, decimal qtyOut, decimal unitCost)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId && p.CompanyId == companyId);
        if (!product.IsStockable() || qtyOut <= 0) return;

        var oldQty = await GetCompanyOnHandAsync(companyId, productId);
        var newQty = oldQty;
        if (newQty <= 0)
        {
            product.SetWeightedAverageCost(0m);
            return;
        }

        var oldCost = product.WeightedAverageCost;
        var wac = Math.Round(
            (oldQty * oldCost - qtyOut * unitCost) / newQty, 4, MidpointRounding.AwayFromZero);
        if (wac < 0) wac = 0;
        product.SetWeightedAverageCost(wac);
    }

    /// <summary>تسجيل حركة مخزنية من مستند — يُفترض تنفيذها داخل معاملة الترحيل.</summary>
    public async Task<StockMovement> PostDocumentMovementAsync(long companyId, long warehouseId,
        StockMovementType type, DateOnly date, string description, string sourceDocument,
        IEnumerable<(long ProductId, decimal Qty, decimal UnitCost)> lines)
    {
        var costing = new InventoryCostingService(db);
        var movement = StockMovement.CreateDraft(companyId, date, type, warehouseId, description, sourceDocument: sourceDocument);
        foreach (var (productId, qty, cost) in lines)
            movement.AddLine(productId, qty, cost);

        if (movement.IsInbound)
        {
            foreach (var line in movement.Lines)
            {
                await costing.OnReceiptAsync(companyId, line.ProductId, warehouseId, date,
                    line.Quantity, line.UnitCost, sourceDocument);
                await ApplyBalanceDeltaAsync(companyId, warehouseId, line.ProductId, line.Quantity);
            }
        }
        else if (movement.IsOutbound)
        {
            foreach (var line in movement.Lines)
            {
                var onHand = await GetOnHandAsync(companyId, line.ProductId, warehouseId);
                if (line.Quantity > onHand)
                {
                    var p = await db.Products.SingleAsync(x => x.Id == line.ProductId);
                    throw new AccountingException($"رصيد '{p.NameAr}' غير كافٍ — المتوفر {onHand:N2}.");
                }
                // التحقق من كفاية الطبقات FIFO (إن وُجدت) — التكلفة تُستهلك من المستدعي عبر CostingService
                await ApplyBalanceDeltaAsync(companyId, warehouseId, line.ProductId, -line.Quantity);
            }
        }

        var number = await DocumentNumberGenerator.NextAsync(
            db.StockMovements.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber),
            "SM", date.Year,
            db.StockMovements.Local.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber));
        movement.MarkPosted(number);
        db.StockMovements.Add(movement);
        return movement;
    }

    public async Task<StockMovement> TransferAsync(long companyId, long fromWarehouseId, long toWarehouseId,
        DateOnly date, string description, IEnumerable<(long ProductId, decimal Qty)> lines)
    {
        var movement = StockMovement.CreateDraft(companyId, date, StockMovementType.Transfer,
            fromWarehouseId, description, toWarehouseId);

        foreach (var (productId, qty) in lines)
        {
            var onHand = await GetOnHandAsync(companyId, productId, fromWarehouseId);
            if (qty > onHand)
            {
                var product = await db.Products.SingleAsync(p => p.Id == productId);
                throw new AccountingException(
                    $"رصيد '{product.NameAr}' غير كافٍ للتحويل — المتوفر {onHand:N2}.");
            }
            var productEntity = await db.Products.SingleAsync(p => p.Id == productId);
            movement.AddLine(productId, qty, productEntity.WeightedAverageCost);
            await ApplyBalanceDeltaAsync(companyId, fromWarehouseId, productId, -qty);
            await ApplyBalanceDeltaAsync(companyId, toWarehouseId, productId, qty);
        }

        var number = await DocumentNumberGenerator.NextAsync(
            db.StockMovements.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber),
            "SM", date.Year,
            db.StockMovements.Local.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber));
        movement.MarkPosted(number);
        db.StockMovements.Add(movement);
        return movement;
    }

    public async Task<decimal> CalculateIssueCostAsync(long companyId, IEnumerable<(long ProductId, decimal Qty)> lines)
    {
        decimal total = 0;
        foreach (var (productId, qty) in lines)
        {
            var product = await db.Products.SingleAsync(p => p.Id == productId);
            if (!product.IsStockable()) continue;
            total += Math.Round(qty * product.WeightedAverageCost, 2, MidpointRounding.AwayFromZero);
        }
        return total;
    }

    public async Task<IReadOnlyDictionary<long, Product>> LoadProductsForLinesAsync(
        long companyId, IEnumerable<long?> productIds)
    {
        var ids = productIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, Product>();
        return await db.Products
            .Where(p => p.CompanyId == companyId && ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
    }

    public async Task ApplyReceiptCostAsync(long companyId, long productId, decimal qtyIn, decimal unitCost)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId && p.CompanyId == companyId);
        if (!product.IsStockable()) return;

        var oldQty = await GetCompanyOnHandAsync(companyId, productId);
        var oldCost = product.WeightedAverageCost;
        var newQty = oldQty + qtyIn;

        var wac = newQty == 0
            ? unitCost
            : Math.Round((oldQty * oldCost + qtyIn * unitCost) / newQty, 4, MidpointRounding.AwayFromZero);

        product.SetWeightedAverageCost(wac);
    }

    public async Task RebuildBalancesFromMovementsAsync(long companyId, CancellationToken ct = default)
    {
        var movements = await db.StockMovements
            .Include(m => m.Lines)
            .Where(m => m.CompanyId == companyId && m.Status == StockMovementStatus.Posted)
            .ToListAsync(ct);

        var existing = await db.InventoryBalances
            .Where(b => b.CompanyId == companyId)
            .ToListAsync(ct);
        db.InventoryBalances.RemoveRange(existing);

        var totals = new Dictionary<(long WarehouseId, long ProductId), decimal>();
        foreach (var m in movements)
        {
            foreach (var line in m.Lines)
            {
                if (m.IsInbound)
                    Add(totals, m.WarehouseId, line.ProductId, line.Quantity);
                if (m.IsOutbound)
                    Add(totals, m.WarehouseId, line.ProductId, -line.Quantity);
                if (m.Type == StockMovementType.Transfer)
                {
                    Add(totals, m.WarehouseId, line.ProductId, -line.Quantity);
                    if (m.ToWarehouseId is long toWh)
                        Add(totals, toWh, line.ProductId, line.Quantity);
                }
            }
        }

        foreach (var ((warehouseId, productId), qty) in totals)
        {
            if (qty == 0) continue;
            db.InventoryBalances.Add(InventoryBalance.Create(companyId, warehouseId, productId, qty));
        }

        await db.SaveChangesAsync(ct);
        return;

        static void Add(Dictionary<(long, long), decimal> map, long wh, long productId, decimal delta)
        {
            var key = (wh, productId);
            map[key] = map.GetValueOrDefault(key) + delta;
        }
    }

    public async Task ApplyMovementBalancesAsync(StockMovement movement)
    {
        foreach (var line in movement.Lines)
        {
            if (movement.IsInbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId, line.Quantity);
            else if (movement.IsOutbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId, -line.Quantity);
            else if (movement.Type == StockMovementType.Transfer)
            {
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId, -line.Quantity);
                if (movement.ToWarehouseId is long toWh)
                    await ApplyBalanceDeltaAsync(movement.CompanyId, toWh, line.ProductId, line.Quantity);
            }
        }
    }

    public async Task ReverseMovementBalancesAsync(StockMovement movement)
    {
        foreach (var line in movement.Lines)
        {
            if (movement.IsInbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId, -line.Quantity);
            else if (movement.IsOutbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId, line.Quantity);
            else if (movement.Type == StockMovementType.Transfer)
            {
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId, line.Quantity);
                if (movement.ToWarehouseId is long toWh)
                    await ApplyBalanceDeltaAsync(movement.CompanyId, toWh, line.ProductId, -line.Quantity);
            }
        }
    }

    private async Task ApplyBalanceDeltaAsync(long companyId, long warehouseId, long productId, decimal delta)
    {
        var balance = await FindBalanceAsync(companyId, warehouseId, productId);

        if (balance is null)
        {
            if (delta < 0)
            {
                var p = await db.Products.SingleAsync(x => x.Id == productId);
                throw new AccountingException($"رصيد '{p.NameAr}' غير كافٍ.");
            }
            balance = InventoryBalance.Create(companyId, warehouseId, productId, 0);
            db.InventoryBalances.Add(balance);
        }

        if (balance.QuantityOnHand + delta < 0)
        {
            var p = await db.Products.SingleAsync(x => x.Id == productId);
            throw new AccountingException(
                $"رصيد '{p.NameAr}' غير كافٍ — المتوفر {balance.QuantityOnHand:N2}.");
        }

        balance.ApplyDelta(delta);
    }

    private async Task<InventoryBalance?> FindBalanceAsync(long companyId, long warehouseId, long productId)
    {
        var local = db.InventoryBalances.Local.FirstOrDefault(b =>
            b.CompanyId == companyId && b.WarehouseId == warehouseId && b.ProductId == productId);
        if (local is not null)
            return local;

        return await db.InventoryBalances
            .SingleOrDefaultAsync(b =>
                b.CompanyId == companyId && b.WarehouseId == warehouseId && b.ProductId == productId);
    }
}
