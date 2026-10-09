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
        if (!await AddCostLayerAsync(companyId, productId, warehouseId, date, qty, unitCost, sourceDocument, ct))
            return;

        await new InventoryService(db).ApplyReceiptCostAsync(companyId, productId, qty, unitCost);
    }

    /// <summary>
    /// (CEEF-229 · م0-٥) ‏تسجيلُ طبقة التكلفة <b>وحدها</b>، بلا لمس المتوسط المرجّح.
    ///
    /// <para>‏فُصلت عن <see cref="OnReceiptAsync"/> لأنّ الاثنين يختلفان في وحدة العمل:
    /// <b>الطبقةُ لكلّ سطر</b> — بتكلفة ذلك السطر بعينها، وإلا ضاعت تفرقةُ الدفعات التي
    /// تقوم عليها FIFO كلُّها. <b>والمتوسطُ لكلّ صنف</b> — يُحسب مرّةً واحدة من مجموع
    /// الكميات وقيمتها، لأنّه دالّةٌ في رصيدٍ لا يتقدّم بين السطور.</para>
    ///
    /// <para>‏تُعيد <c>false</c> لصنفٍ غير مخزنيّ — فلا طبقةَ له ولا متوسط.</para>
    /// </summary>
    public async Task<bool> AddCostLayerAsync(
        long companyId, long productId, long warehouseId, DateOnly date,
        decimal qty, decimal unitCost, string sourceDocument, CancellationToken ct = default)
    {
        var product = await db.Products.IgnoreQueryFilters()
            .SingleAsync(p => p.Id == productId && p.CompanyId == companyId, ct);
        if (!product.IsStockable()) return false;

        var method = await GetMethodAsync(companyId, ct);
        if (method == InventoryCostingMethod.Fifo)
        {
            db.InventoryCostLayers.Add(InventoryCostLayer.Create(
                companyId, productId, warehouseId, date, qty, unitCost, sourceDocument));
        }

        return true;
    }

    /// <summary>
    /// يستهلك تكلفة كمية صادرة ويعيد التكلفة الفعلية للوحدة المستخدمة في القيد.
    /// في FIFO يحدّث الطبقات؛ في WAC يقرأ المتوسط الحالي فقط.
    /// </summary>
    /// <summary>
    /// (CEEF-229 · م0-٣) <paramref name="sourceDocument"/> إلزاميّ — وهو مفتاحُ ترميم
    /// الطبقات عند الإلغاء. وبلا تسجيلِ ما أُخذ من كلّ طبقة لا يمكن عكسُ استهلاك FIFO،
    /// لأنّه يوزّع الكميةَ على طبقاتٍ عدّة بنسبٍ لا تُخمَّن.
    /// </summary>
    public async Task<(decimal TotalCost, decimal UnitCost)> ConsumeForIssueAsync(
        long companyId, long productId, long warehouseId, decimal qty,
        string sourceDocument, CancellationToken ct = default)
    {
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId && p.CompanyId == companyId, ct);
        if (!product.IsStockable())
            return (0m, 0m);

        var method = await GetMethodAsync(companyId, ct);
        if (method == InventoryCostingMethod.WeightedAverage)
        {
            await db.Entry(product).ReloadAsync(ct);
            var total = Math.Round(qty * product.WeightedAverageCost, 2, MidpointRounding.AwayFromZero);
            return (total, product.WeightedAverageCost);
        }

        return await ConsumeFifoAsync(companyId, productId, warehouseId, qty, sourceDocument, ct);
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
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId && p.CompanyId == companyId, ct);
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

    /// <summary>
    /// (CEEF-229 · م0-٣) <b>يُعيد إلى كلّ طبقةٍ ما أُخذ منها بعينه</b> — لا يُنشئ طبقةً جديدة.
    ///
    /// <para>والفرقُ ليس تجميلياً: الطبقةُ الجديدة تحمل تاريخَ الإلغاء فتقع في <b>آخر طابور
    /// FIFO</b>، فتأكل البيعةُ التالية طبقةً أغلى. وقيس الأثر: التكلفةُ نفسُها تخرج
    /// <b>ضِعفَ</b> ما ينبغي.</para>
    ///
    /// <para>ويعيد <c>false</c> حين لا سجلَّ — وهو حالُ المستندات التي رُحّلت قبل هذه الدفعة.
    /// فيُقرّر المستدعي ماذا يفعل، ولا يُبتلع الأمرُ صامتاً.</para>
    /// </summary>
    public async Task<bool> RestoreConsumedLayersAsync(
        long companyId, string sourceDocument, CancellationToken ct = default)
    {
        var consumptions = await db.InventoryCostConsumptions
            .Where(c => c.CompanyId == companyId && c.SourceDocument == sourceDocument)
            .ToListAsync(ct);

        if (consumptions.Count == 0)
            return false;

        var layerIds = consumptions.Select(c => c.CostLayerId).Distinct().ToList();
        var layers = await db.InventoryCostLayers
            .Where(l => layerIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);

        foreach (var consumption in consumptions)
        {
            if (layers.TryGetValue(consumption.CostLayerId, out var layer))
                layer.Restore(consumption.Quantity);
        }

        // ‏يُمحى السجلّ بعد الترميم — فلا يُرمَّم مرّتين لو أُعيد الإلغاء.
        db.InventoryCostConsumptions.RemoveRange(consumptions);

        foreach (var productId in consumptions.Select(c => c.ProductId).Distinct())
            await RecalculateWacFromLayersAsync(companyId, productId, ct);

        return true;
    }

    /// <summary>
    /// (CEEF-229 · م0-٣ب) <b>يُزيل الطبقةَ التي أنشأها مستندٌ داخل — مرآةُ الترميم.</b>
    ///
    /// <para>إلغاءُ الإشعار الدائن كان يُرحّل حركةَ <c>Issue</c> جديدة، <b>وهي تستهلك من
    /// رأس طابور FIFO</b> — أقدمَ طبقةٍ وأرخصَها — وتترك طبقةَ المرتجع قائمة. فتُقلَب
    /// تكلفةُ البيعة التالية، <b>وقيمةُ المخزون الإجمالية لا تكشف ذلك لأنّها متطابقة</b>.</para>
    ///
    /// <para><b>وإن استُهلك من الطبقة شيءٌ بعد إنشائها فلا إلغاء:</b> لا يُمكن سحبُ بضاعةٍ
    /// بيعت. والرفضُ الصريح أصدقُ من ترقيعٍ يُنتج تكلفةً مخترَعة.</para>
    /// </summary>
    public async Task<bool> ReverseReceiptLayersAsync(
        long companyId, string sourceDocument, CancellationToken ct = default)
    {
        var layers = await db.InventoryCostLayers
            .Where(l => l.CompanyId == companyId && l.SourceDocument == sourceDocument)
            .ToListAsync(ct);

        if (layers.Count == 0)
            return false;

        var consumed = layers.Where(l => l.RemainingQuantity < l.OriginalQuantity).ToList();
        if (consumed.Count > 0)
        {
            var names = await db.Products.IgnoreQueryFilters()
                .Where(p => consumed.Select(l => l.ProductId).Contains(p.Id))
                .Select(p => p.NameAr).ToListAsync(ct);

            throw new AccountingException(
                "لا يمكن إلغاء هذا المستند: بضاعتُه الداخلة استُهلكت في حركةٍ لاحقة — "
                + string.Join("، ", names)
                + ". اعكس الحركاتِ اللاحقة أوّلاً.");
        }

        db.InventoryCostLayers.RemoveRange(layers);

        // (CEEF-229 · م0-٣ج) **يُحفَظ قبل إعادة الحساب.** `RemoveRange` تُعلّم الصفوفَ للحذف
        // ولا تُنفّذه، وإعادةُ الحساب تستعلم **من القاعدة** فترى الطبقاتِ المحذوفةَ قائمةً
        // وتُعيد بناء المتوسط منها. وأثرُه مقيس: إلغاءُ الشراء الوحيد كان يُبقي المتوسطَ
        // عند 100 بدل صفر — أمسكه `CancelPurchaseInvoice_ReversesWac` القائم.
        await db.SaveChangesAsync(ct);

        foreach (var productId in layers.Select(l => l.ProductId).Distinct())
            await RecalculateWacFromLayersAsync(companyId, productId, ct);

        return true;
    }

    private async Task<(decimal TotalCost, decimal UnitCost)> ConsumeFifoAsync(
        long companyId, long productId, long warehouseId, decimal qty,
        string sourceDocument, CancellationToken ct)
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
            var p = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == productId, ct);
            throw new AccountingException(
                $"طبقات FIFO للصنف '{p.NameAr}' غير كافية — المتبقي {available:N4}، المطلوب {qty:N4}.");
        }

        decimal remaining = qty;
        decimal totalCost = 0m;
        foreach (var layer in layers)
        {
            if (remaining <= 0) break;
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٣) ‏**طبقةٌ استنفدها سطرٌ سابقٌ في المستند نفسِه تُتخطّى.** الشرطُ «المتبقّي موجب» يُقيَّم في
            // ‏القاعدة، والاستهلاكُ السابقُ في الذاكرة لم يُحفظ — فتعود الطبقةُ المتتبَّعةُ برصيدها الصفر، وكان يُطلب منها صفرٌ
            // ‏فيرمي `Consume`: فلا تُرحَّل فاتورةٌ بالصنف على سطرين متى استنفد الأوّلُ طبقة.
            if (layer.RemainingQuantity <= 0) continue;
            var take = Math.Min(remaining, layer.RemainingQuantity);
            totalCost += layer.Consume(take);

            // ‏يُسجَّل ما أُخذ من هذه الطبقة بعينها — وهو كلُّ ما يجعل الإلغاء عكساً حقيقياً.
            db.InventoryCostConsumptions.Add(InventoryCostConsumption.Record(
                companyId, layer.Id, productId, layer.WarehouseId,
                sourceDocument, take, layer.UnitCost, DateTime.UtcNow));

            remaining = Math.Round(remaining - take, 4, MidpointRounding.AwayFromZero);
        }

        var unit = qty == 0 ? 0m : Math.Round(totalCost / qty, 4, MidpointRounding.AwayFromZero);

        // حافظ على WAC متسقاً مع الطبقات المتبقية
        await RecalculateWacFromLayersAsync(companyId, productId, ct);
        return (totalCost, unit);
    }
}
