using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// محرك المخزون: أرصدة مجمّعة، متوسط مرجح، وحركات — يُحدَّث الرصيد داخل معاملة الترحيل.
/// </summary>
/// <summary>
/// حركة المخزون وتكلفته.
///
/// <para>(CEEF-229 · م2-١٨) <b>قراءات الأصناف هنا تتجاوز مرشّح الحذف الناعم عمداً.</b>
/// كلُّها تُحمِّل صنفاً <b>يشير إليه مستندٌ أو رصيدٌ قائم</b> — لا صنفاً يختاره المستخدم
/// من قائمة. وحذفُ الصنف لاحقاً لا يُلغي مستنداً مرحَّلاً ولا رصيداً في مستودع.</para>
///
/// <para>وقراءةُ المرشَّح كانت تُنتج عطلَين مختلفَين: <b>رمياً</b> يُسقط إلغاء الفاتورة
/// («Sequence contains no elements» بالإنجليزية في واجهةٍ عربية، فتبقى الفاتورة مرحّلة
/// إلى الأبد ولا مخرج)، و<b>تخطّياً صامتاً</b> في الإشعار الدائن يُعكس المال ولا يُرجع
/// البضاعة ولا يعكس تكلفتها — ولا يُكشف إلا بجردٍ بعد شهور.</para>
/// </summary>
public class InventoryService(BrqoshDbContext db)
{
    public async Task<Warehouse> GetDefaultWarehouseAsync(long companyId)
    {
        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsDefault && w.IsActive);
        warehouse ??= await db.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsActive);

        // (CEEF-229 · م2-١٧د) **الاختراعُ لمن لا مستودعَ له، لا لمن أوقف مستودعاته.**
        //
        // كان الإنشاءُ أدناه يقع في الحالتين. وُضع لأوّل تشغيلٍ في منشأةٍ فارغة — وهو صواب.
        // لكنّ منشأةً أوقفت مستودعاتها كلَّها كانت تحصل على مستودعٍ **لم يطلبه أحد**، فتنقسم
        // بضاعتُها بين اثنين: القديمُ فيه ما اشترته، والجديدُ فيه ما بعده.
        //
        // وهذا حالُ العميل التجريبيّ بعينه — متجرٌ بمستودعٍ واحد.
        //
        // ولم يكن العطلُ يظهر رسالةً بل انفجاراً: الاختراعُ يستعمل الكود `WH-001` فيصطدم
        // بالمستودع الموقوف نفسِه، فيخرج `UNIQUE constraint failed` — نصٌّ إنجليزيّ من قاعدة
        // البيانات في تطبيقٍ عربيّ، لا يقول للمالك ما وقع ولا ما يفعل. ولو كان كودُ مستودعه
        // مختلفاً لنجح الاختراعُ صامتاً، وهو أسوأ.
        if (warehouse is null
            && await db.Warehouses.AnyAsync(w => w.CompanyId == companyId))
        {
            throw new Brqosh.Domain.Accounting.AccountingException(
                "كلُّ المستودعات موقوفة — لا يمكن ترحيل حركةٍ مخزنية."
                + "\n\nفعِّل مستودعاً من: المستودعات ← تعديل المستودع.");
        }

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
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId && p.CompanyId == companyId);
        if (!product.IsStockable() || qtyOut <= 0) return;

        // (CEEF-228) المستدعيان (إلغاء فاتورة مشتريات وإلغاء إذن استلام) يخصمان الرصيد
        // بحركة Issue *قبل* هذا النداء - فالمقروء هنا هو الرصيد المتبقّي بعد السحب.
        //
        // كانت المعادلة تطرح qtyOut من البسط ولا تطرحه من المقام، وتفترض أن المقروء رصيدٌ
        // *سابق*. المثال: شراء 100@10 ثم 100@20 (WAC=15، رصيد 200). بإلغاء الثاني:
        //   الخطأ:  (100*15 - 100*20) / 100 = -5  ->  يُقصّ إلى صفر
        //   الصواب: ((100+100)*15 - 100*20) / 100 = 10
        // وتكلفة صفر تعني أن SalesInvoicePostingService يُسقط سطرَي COGS والمخزون كلياً،
        // فيبقى حساب المخزون في الأستاذ بلا سند كمّي ويُضخَّم مجمل الربح.
        var remainingQty = await GetCompanyOnHandAsync(companyId, productId);
        if (remainingQty <= 0)
        {
            product.SetWeightedAverageCost(0m);
            return;
        }

        var oldCost = product.WeightedAverageCost;
        var quantityBeforeReversal = remainingQty + qtyOut;
        var valueBeforeReversal = quantityBeforeReversal * oldCost;
        var valueRemoved = qtyOut * unitCost;

        var wac = Math.Round(
            (valueBeforeReversal - valueRemoved) / remainingQty, 4, MidpointRounding.AwayFromZero);
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
                    var p = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == line.ProductId);
                    throw new AccountingException($"رصيد '{p.NameAr}' غير كافٍ — المتوفر {onHand:N2}.");
                }
                // التحقق من كفاية الطبقات FIFO (إن وُجدت) — التكلفة تُستهلك من المستدعي عبر CostingService
                await ApplyBalanceDeltaAsync(companyId, warehouseId, line.ProductId, -line.Quantity);
            }
        }

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.StockMovements.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber),
            "SM", date.Year, companyId,
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
                var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId);
                throw new AccountingException(
                    $"رصيد '{product.NameAr}' غير كافٍ للتحويل — المتوفر {onHand:N2}.");
            }
            var productEntity = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId);
            movement.AddLine(productId, qty, productEntity.WeightedAverageCost);
            await ApplyBalanceDeltaAsync(companyId, fromWarehouseId, productId, -qty);
            await ApplyBalanceDeltaAsync(companyId, toWarehouseId, productId, qty);
        }

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.StockMovements.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber),
            "SM", date.Year, companyId,
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
            var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId);
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
        // تتجاوز المرشّح كبقيّة قراءات هذا الصنف — انظر الشرح على رأس الصنف. وهنا تحديداً
        // كان الأثر **تخطّياً صامتاً**: الصنف الغائب عن القاموس يُهمله المستدعي بلا كلمة.
        return await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
    }

    /// <summary>
    /// (CEEF-229 · م2-١٧ب) <b>الإيقافُ يمنع تجارةً جديدة — لا يمنع ردَّ ما بيع.</b>
    ///
    /// <para>زرُّ «إيقاف» في شاشة الأصناف كان تسميةً بلا أثر: يضبط <c>IsActive = false</c>
    /// ولا يفحصه مسارُ ترحيلٍ واحد، فيُباع الصنفُ الموقوف ويُشترى كأنّ شيئاً لم يكن.</para>
    ///
    /// <para><b>ولا يُوضع هذا الحارسُ في <see cref="LoadProductsForLinesAsync"/> رغم أنّ
    /// المسارات الأربعة تمرّ به</b>: الإشعارُ الدائن ردٌّ لبضاعةٍ بيعت، والمدينُ ردٌّ لبضاعةٍ
    /// اشتُريت. ومنعُهما يحبس البضاعةَ عند الزبون أو عند المورّد بلا مخرج — ويجعل الإيقافَ
    /// عقوبةً على التاجر لا أداةً بيده. فالردُّ يبقى مفتوحاً، والتجارةُ الجديدة تُمنع.</para>
    /// </summary>
    public static void EnsureTradable(IReadOnlyDictionary<long, Product> products, string action)
    {
        var stopped = products.Values.Where(p => !p.IsActive).Select(p => p.NameAr).ToList();
        if (stopped.Count == 0)
            return;

        throw new AccountingException(
            $"لا يمكن {action} أصنافٍ موقوفة: {string.Join("، ", stopped)}."
            + "\n\nأعِد تفعيلها من: الأصناف ← تعديل الصنف، أو احذفها من المستند.");
    }

    public async Task ApplyReceiptCostAsync(long companyId, long productId, decimal qtyIn, decimal unitCost)
    {
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId && p.CompanyId == companyId);
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

    /// <summary>
    /// (CEEF-229 · م0-١) عكسٌ كاملٌ لحركة مخزنية: الكمية <b>والتكلفة</b> معاً.
    ///
    /// <para><b>لماذا مسارٌ واحد:</b> كان كلُّ مستدعٍ يعكس بطريقته. إلغاء فاتورة المشتريات
    /// وإذن الاستلام يعكسان التكلفة عبر <see cref="ReverseReceiptCostAsync"/>، بينما
    /// <c>StockMovementCancellationService</c> يعكس الكمية وحدها ويصفّر المتوسط في حالةٍ
    /// منحلّة واحدة (أن يصير الرصيد الكليّ صفراً). فإلغاء حركةٍ واردة مع بقاء رصيد يترك
    /// المتوسط <b>مسموماً بتكلفة حركةٍ أُلغيت</b>.</para>
    ///
    /// <para><b>الأثر بالأرقام:</b> رصيد 100 حبة بتكلفة 10 (قيمة 1,000)، ثم حركة «رصيد
    /// افتتاحيّ» 100 حبة بتكلفة 50 ⟵ المتوسط 30. يُكتشف الخطأ فتُلغى الحركة: الكمية تعود 100
    /// و<b>المتوسط يبقى 30</b>. الدفتر المساعد يقول 3,000 والأستاذ 1,000 بعد عكس القيد،
    /// وأوّل بيع يجعل حساب المخزون <b>دائناً</b> في الميزانية. وتصحيحُ الرصيد الافتتاحيّ هو
    /// أكثر إلغاءٍ يقع في حياة النظام — أسبوع العميل الأول.</para>
    ///
    /// <para><b>الترتيب مقصود:</b> الكمية تُعكس أولاً، ثم تُعكس التكلفة — لأن
    /// <see cref="ReverseReceiptCostAsync"/> يقرأ الرصيد <b>بعد</b> السحب بحكم عقده
    /// الموثَّق في CEEF-228. عكسُ الترتيب يُنتج متوسطاً خاطئاً بصمت.</para>
    ///
    /// <para><b>ويُعالج الطريقتين معاً (م0-٣، أُغلق):</b> المتوسطُ المرجّح بعكس التكلفة
    /// أعلاه، و<b>FIFO بترميم الطبقات المستهلَكة بعينها</b> من سجلّ
    /// <c>InventoryCostConsumption</c>. وكان هذا التعليقُ يُقرّ بأنّ
    /// <c>InventoryCostLayer.Restore()</c> بصفر مستدعٍ وأنّ البند مفتوح — وقد صار له
    /// مستدعٍ واحدٌ هو الترميم.</para>
    /// </summary>
    public async Task ReverseDocumentMovementAsync(StockMovement movement)
    {
        await ReverseMovementBalancesAsync(movement);

        if (!movement.IsInbound)
        {
            // (CEEF-229 · م0-٣) **الصادرُ يُرمّم طبقاتِه المستهلَكة — لا يُنشئ طبقةً جديدة.**
            //
            // كان التعليقُ هنا يُقرّ بأنّ `InventoryCostLayer.Restore()` بصفر مستدعٍ وأنّ
            // البند مفتوح. وقد أُغلق: `ConsumeForIssueAsync` يُسجّل ما أُخذ من كلّ طبقة
            // مربوطاً بالمستند، فيُعاد بعينه.
            if (!string.IsNullOrWhiteSpace(movement.SourceDocument))
                await new InventoryCostingService(db)
                    .RestoreConsumedLayersAsync(movement.CompanyId, movement.SourceDocument);

            return;
        }

        // (CEEF-229 · م0-٣ب) وللداخل: تُزال الطبقةُ التي أنشأها هذا المستند بعينها.
        // ‏وإن لم توجد طبقاتٌ (منشأةٌ على المتوسط المرجّح) فالعكسُ بحساب المتوسط وحده.
        var layersRemoved = !string.IsNullOrWhiteSpace(movement.SourceDocument)
            && await new InventoryCostingService(db)
                .ReverseReceiptLayersAsync(movement.CompanyId, movement.SourceDocument);

        if (layersRemoved)
            return;

        // (CEEF-229 · م0-٥ / I-NEW-2) ‏**التجميعُ بالصنف قبل عكس التكلفة.**
        //
        // ‏`ReverseReceiptCostAsync` **موثَّقٌ فيها** أنّ الرصيد المقروء نقص بكمّية هذا النداء
        // ‏سلفاً، فتُعيد بناء ما قبله بـ `remainingQty + qtyOut`. و`ReverseMovementBalancesAsync`
        // ‏أعلاه تُزيل **كلَّ سطور المستند** دفعةً واحدة — فالافتراضُ يصحّ لسطرٍ واحد ويسقط
        // ‏لسطرين فصاعداً: كلُّ نداءٍ يُعيد بناءَ رصيدٍ لم يكن.
        //
        // ‏**والأثرُ مقيس**: افتتاحيٌّ 10@10 ثمّ وارِدٌ بثلاثة سطور (2@12 · 3@15 · 5@20) ثمّ
        // ‏إلغاؤه ⟵ المتوسط **11.9701** والصواب 10، و**الدفتر المساعد 119.70 والأستاذ 100.00**.
        // ‏فهي هوية هـ-٣ مكسورةً بـ19.70 ريالاً — انفصالُ دفترٍ مساعدٍ عن الأستاذ، لا رقمٌ متّسق.
        //
        // ‏**وليس ما وصفته الخطة.** قالت «سطران يعكسان كلٌّ الكميةَ كاملة»؛ والكمياتُ تُعكس
        // ‏صحيحةً (مقيسٌ: سطران بـ3 و4 ⟵ الرصيد يعود تماماً). العطلُ في **التكلفة** وحدها،
        // ‏ولا يظهر إلا حين تختلف تكاليفُ السطور — **ودواؤه هو التجميعُ نفسُه الذي وصفته**.
        //
        // ‏والتكلفةُ الموحَّدة **مرجّحةٌ بالكمية** لا وسطاً حسابياً: هي القيمةُ التي أدخلها
        // ‏المستند فعلاً مقسومةً على كمّيته، فيخرج من المتوسط ما دخل فيه بعينه.
        foreach (var group in movement.Lines.GroupBy(l => l.ProductId))
        {
            var quantity = group.Sum(l => l.Quantity);
            if (quantity <= 0) continue;

            var weightedUnitCost = group.Sum(l => l.Quantity * l.UnitCost) / quantity;
            await ReverseReceiptCostAsync(movement.CompanyId, group.Key, quantity, weightedUnitCost);
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
                var p = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == productId);
                throw new AccountingException($"رصيد '{p.NameAr}' غير كافٍ.");
            }
            balance = InventoryBalance.Create(companyId, warehouseId, productId, 0);
            db.InventoryBalances.Add(balance);
        }

        if (balance.QuantityOnHand + delta < 0)
        {
            var p = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == productId);
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
