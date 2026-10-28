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
    // ‏(جاهزيّةُ الإطلاق · ح-١١٢) ‏**مصادرُ عُكست تكلفتُها في وحدة العمل هذه، باتّجاهها.** الطبقاتُ مفتاحُها المستند لا الحركة، والإلغاءُ يعكس
    // ‏حركةً حركة — فمستندٌ بحركتين (فاتورةٌ ووصفةُ مطبخها، إشعارٌ مدينٌ من مستودعين) كانت تُرمَّم طبقاتُه مرّتين.
    private readonly HashSet<(string Source, bool Inbound)> _costReversed = [];

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
    /// <summary>
    /// (CEEF-230 · ط1-٣) ‏التحميلُ القديم — <b>مسارٌ بلا هويّةِ دفعة</b>.
    ///
    /// <para>يُفوّض إلى التحميل الحامل للهويّة بـ<c>null</c> لكلّ سطر. <b>ومسارٌ يستعمله
    /// يرفض الصنفَ المتتبَّع رفضاً مسمّى</b> عند المَخنَق — ولا يمرّره بلا رقم. فتحويلُ
    /// المسارات إلى الهويّة يجري واحداً واحداً، <b>وما لم يُحوَّل يُعلن نفسَه</b> بدل أن
    /// يبتلع الصلاحية بصمت.</para>
    /// </summary>
    public Task<StockMovement> PostDocumentMovementAsync(long companyId, long warehouseId,
        StockMovementType type, DateOnly date, string description, string sourceDocument,
        IEnumerable<(long ProductId, decimal Qty, decimal UnitCost)> lines) =>
        PostDocumentMovementAsync(companyId, warehouseId, type, date, description, sourceDocument,
            lines.Select(l => (l.ProductId, l.Qty, l.UnitCost, (string?)null, (DateOnly?)null)));

    /// <summary>‏التحميلُ الحاملُ لهويّة الدفعة — وهو ما تستعمله المسارات المحوَّلة.</summary>
    public async Task<StockMovement> PostDocumentMovementAsync(long companyId, long warehouseId,
        StockMovementType type, DateOnly date, string description, string sourceDocument,
        IEnumerable<(long ProductId, decimal Qty, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate)> lines,
        bool expiredMayLeave = false)
    {
        var costing = new InventoryCostingService(db);
        var movement = StockMovement.CreateDraft(companyId, date, type, warehouseId, description, sourceDocument: sourceDocument);
        foreach (var (productId, qty, cost, batchNumber, expiry) in lines)
            movement.AddLine(productId, qty, cost, batchNumber, expiry);

        if (movement.IsInbound)
        {
            foreach (var line in movement.Lines)
            {
                await costing.OnReceiptAsync(companyId, line.ProductId, warehouseId, date,
                    line.Quantity, line.UnitCost, sourceDocument);
                await ApplyBalanceDeltaAsync(companyId, warehouseId, line.ProductId, line.Quantity,
                    BatchRef.From(line.BatchNumber, line.ExpiryDate), date, type);
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
                await ApplyBalanceDeltaAsync(companyId, warehouseId, line.ProductId, -line.Quantity,
                    BatchRef.From(line.BatchNumber, line.ExpiryDate), date, type, expiredMayLeave);
            }
        }

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.StockMovements.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber),
            "SM", date, companyId,
            db.StockMovements.Local.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber));
        movement.MarkPosted(number);
        db.StockMovements.Add(movement);
        return movement;
    }

    /// <summary>
    /// يُحوّل بضاعةً بين مستودعين — <b>والتكلفةُ تنتقل معها</b>.
    /// </summary>
    /// <remarks>
    /// <para>(CEEF-231 · ط4-٤) <b>وما كان:</b> التحويلُ يُحرّك الكمّياتِ <b>ولا يمسّ
    /// طبقاتِ التكلفة البتّة</b>. فمستودعٌ يُفرَّغ عن آخره تبقى طبقاتُه فيه كاملةً،
    /// <b>والمستودعُ المستقبِل يمتلئ بضاعةً بلا طبقةٍ واحدة</b>.</para>
    ///
    /// <para><b>وأثرُه ثلاثةٌ:</b> جردُ المستودع الأوّل يُقوَّم بمالٍ لبضاعةٍ ليست فيه ·
    /// والثاني بصفرٍ وفيه بضاعة · <b>وبيعٌ من الأوّل بعد ذلك يُرفض بـ«طبقات FIFO غير
    /// كافية»</b> وفيه رصيدٌ ماديّ — رفضٌ كاذبٌ يُوقف البيع.</para>
    ///
    /// <para><b>وهويّةُ هـ-١٩:</b> بعد التحويل، مجموعُ الطبقات الباقية في المستودعين
    /// <b>مساوٍ لما كان قبله</b>، وتوزيعُها يتبع الكمّيات. <b>والتحويلُ لا يُغيّر قيمةَ
    /// المخزون كلِّه</b> — فلا قيدَ في الأستاذ، وهو صوابٌ يبقى.</para>
    /// </remarks>
    public async Task<StockMovement> TransferAsync(long companyId, long fromWarehouseId, long toWarehouseId,
        DateOnly date, string description, IEnumerable<(long ProductId, decimal Qty)> lines)
    {
        // (CEEF-231 · ط4-٤) ‏**الرقمُ يُخصَّص قبل استهلاك الطبقات** — فهو مفتاحُ سجلّ
        // ‏الاستهلاك الذي يُرمَّم منه عند الإلغاء. ولو خُصّص بعده لَسُجّل الاستهلاكُ باسمٍ
        // ‏فارغ **فلا يجده الإلغاءُ ولا تعود الطبقات** — وهو درسُ `م0-٣` بعينه.
        var number = await DocumentNumberGenerator.NextAsync(db,
            db.StockMovements.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber),
            "SM", date, companyId,
            db.StockMovements.Local.Where(m => m.CompanyId == companyId).Select(m => m.MovementNumber));

        // ‏**ولا يُخزَّن في `SourceDocument`** — فذاك معناه «حركةٌ يملكها مستندٌ آخر»،
        // ‏وتخزينُه هنا يمنع إلغاءَ التحويل من شاشته.
        var sourceDocument = $"StockMovement:{number}";
        var movement = StockMovement.CreateDraft(companyId, date, StockMovementType.Transfer,
            fromWarehouseId, description, toWarehouseId);

        var costing = new InventoryCostingService(db);

        foreach (var (productId, qty) in lines)
        {
            var onHand = await GetOnHandAsync(companyId, productId, fromWarehouseId);
            if (qty > onHand)
            {
                var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId);
                throw new AccountingException(
                    $"رصيد '{product.NameAr}' غير كافٍ للتحويل — المتوفر {onHand:N2}.");
            }

            // ‏**والطبقةُ تُستهلَك من المُرسِل وتُنشأ في المستقبِل بالتكلفة نفسِها.**
            //
            // ‏والمتوسطُ المرجّح لا طبقاتِ له، فتُعيد `ConsumeForIssueAsync` المتوسطَ
            // ‏العامّ ولا تُغيّر شيئاً — **والمنشأةُ على المتوسط لا تُفرّق بين مستودعين
            // ‏أصلاً**، فالسلوكان صحيحان كلٌّ في طريقته.
            var (_, unitCost) = await costing.ConsumeForIssueAsync(
                companyId, productId, fromWarehouseId, qty, sourceDocument);

            await costing.AddCostLayerAsync(companyId, productId, toWarehouseId, date,
                qty, unitCost, sourceDocument);

            movement.AddLine(productId, qty, unitCost);

            // (CEEF-230 · ط1-٣) ‏**التحويلُ لا يحمل هويّةَ دفعةٍ بعد** — ووسيطُه
            // ‏`(ProductId, Qty)` بلا رقم. فيمرّ `null`، ويرفض المَخنَقُ الصنفَ المتتبَّع
            // ‏**رفضاً مسمّى**. وهو مقصود: نقلُ دفعةٍ بلا رقمها يُفقدها هويّتَها في
            // ‏المستودع الآخر، **والرفضُ أصدقُ من نقلٍ يمحو الصلاحية**.
            await ApplyBalanceDeltaAsync(companyId, fromWarehouseId, productId, -qty,
                null, date, StockMovementType.Transfer);
            await ApplyBalanceDeltaAsync(companyId, toWarehouseId, productId, qty,
                null, date, StockMovementType.Transfer);
        }

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
            var batch = BatchRef.From(line.BatchNumber, line.ExpiryDate);

            if (movement.IsInbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId,
                    line.Quantity, batch, movement.MovementDate, movement.Type);
            else if (movement.IsOutbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId,
                    -line.Quantity, batch, movement.MovementDate, movement.Type);
            else if (movement.Type == StockMovementType.Transfer)
            {
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId,
                    -line.Quantity, batch, movement.MovementDate, movement.Type);
                if (movement.ToWarehouseId is long toWh)
                    await ApplyBalanceDeltaAsync(movement.CompanyId, toWh, line.ProductId,
                        line.Quantity, batch, movement.MovementDate, movement.Type);
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

        // (CEEF-231 · ط4-٤) ‏**والتحويلُ يُعكَس في الطرفين معاً.**
        //
        // ‏هو صادرٌ ووارِدٌ في مستندٍ واحد: **استهلك طبقةً هنا وأنشأ طبقةً هناك**.
        // ‏فعكسٌ يُرمّم المستهلَكَ ولا يُزيل المُنشأ **يُضاعف قيمةَ المخزون** بقيمة كلّ
        // ‏تحويلٍ يُلغى — وهو أشدُّ من العطل الذي عولج، لا أخفّ.
        if (movement.Type == StockMovementType.Transfer)
        {
            // ‏**والمفتاحُ يُشتقّ من رقم الحركة** — لا من `SourceDocument`، فذاك معناه
            // ‏«حركةٌ يملكها مستندٌ آخر» ويمنع الإلغاءَ من أصله.
            if (!string.IsNullOrWhiteSpace(movement.MovementNumber))
            {
                var costing = new InventoryCostingService(db);
                var costKey = $"StockMovement:{movement.MovementNumber}";
                await costing.ReverseReceiptLayersAsync(movement.CompanyId, costKey);
                await costing.RestoreConsumedLayersAsync(movement.CompanyId, costKey);
            }

            return;
        }

        if (!movement.IsInbound)
        {
            // (CEEF-229 · م0-٣) **الصادرُ يُرمّم طبقاتِه المستهلَكة — لا يُنشئ طبقةً جديدة.**
            //
            // كان التعليقُ هنا يُقرّ بأنّ `InventoryCostLayer.Restore()` بصفر مستدعٍ وأنّ
            // البند مفتوح. وقد أُغلق: `ConsumeForIssueAsync` يُسجّل ما أُخذ من كلّ طبقة
            // مربوطاً بالمستند، فيُعاد بعينه.
            if (!string.IsNullOrWhiteSpace(movement.SourceDocument))
            {
                // ‏(ح-١١٢) ‏رُمِّم المستهلَكُ كلُّه باسم المستند مع حركته الأولى — فلا يُرمَّم ثانيةً.
                if (_costReversed.Contains((movement.SourceDocument, false)))
                    return;
                var costing = new InventoryCostingService(db);
                var restored = await costing.RestoreConsumedLayersAsync(
                    movement.CompanyId, movement.SourceDocument);
                if (restored)
                    _costReversed.Add((movement.SourceDocument, false));

                // (جاهزيّةُ الإطلاق · ح-٣١ب) ‏**و«لا سجلّ» لا يُبتلع.**
                //
                // ‏`RestoreConsumedLayersAsync` **موثّقٌ فيها** أنّ `false` يعني «مستندٌ
                // ‏رُحّل قبل هذه الدفعة — **فيُقرّر المستدعي** ولا يُبتلع الأمرُ
                // ‏صامتاً». **وكلا مستدعيَها كان يرمي القيمةَ** — فالقرارُ الموصوفُ
                // ‏بصفرِ متخذٍ.
                //
                // ‏والأثرُ مقيس: `ReverseMovementBalancesAsync` أعاد الكمّيةَ سلفاً،
                // ‏**فإن لم تعد طبقةٌ معها بقي الرصيدُ بلا تكلفةٍ تحملُه**:
                // ‏**الطبقات ٦ والرصيد ١٠** على إشعارٍ مدينٍ رُحّل قبل ح-٣١.
                //
                // ‏**والترميمُ بمفتاحٍ قديمٍ غيرُ ممكن** — ذاك المفتاحُ كان فارغاً
                // ‏مشترَكاً، فالترميمُ به يردُّ طبقاتَ مستندٍ آخر. **فتُبنى طبقةٌ من
                // ‏سطور الحركة نفسِها** — وهي تحمل التكلفةَ التي خرجت بها فعلاً.
                //
                // ‏**وهي أدنى من الترميم بعينه ولا تُساويه**: تقع في ذيل طابور
                // ‏الوارد-أوّلاً لا في موضعها. **لكنّ هويّةً قائمةً بترتيبٍ مزاحَم
                // ‏أصدقُ من هويّةٍ منشقّة** — وهذا ما كانت تفعلُه الحركةُ المضادّة
                // ‏قبلَ ح-٣١، **فلا تراجُع عن حالٍ قائم**.
                //
                // ‏و`AddCostLayerAsync` **تُميّز بنفسها**: لا تفعل شيئاً في المتوسّط
                // ‏المرجّح (ولا طبقاتِ هناك أصلاً، **و`false` حالُه دائماً**)، ولا لصنفٍ
                // ‏غير مخزنيٍّ. **فالمسارُ السليمُ لا يُمسّ.**
                if (!restored)
                {
                    foreach (var line in movement.Lines)
                        await costing.AddCostLayerAsync(
                            movement.CompanyId, line.ProductId, movement.WarehouseId,
                            movement.MovementDate, line.Quantity, line.UnitCost,
                            movement.SourceDocument);
                }
            }

            return;
        }

        // (CEEF-229 · م0-٣ب) وللداخل: تُزال الطبقةُ التي أنشأها هذا المستند بعينها.
        // ‏وإن لم توجد طبقاتٌ (منشأةٌ على المتوسط المرجّح) فالعكسُ بحساب المتوسط وحده.
        // ‏(ح-١١٢) ‏وطبقاتُ المستند كلُّها أُزيلت مع حركته الأولى — فلا تسقط الثانيةُ إلى عكس المتوسّط ثانيةً.
        if (!string.IsNullOrWhiteSpace(movement.SourceDocument) && _costReversed.Contains((movement.SourceDocument, true)))
            return;
        var layersRemoved = !string.IsNullOrWhiteSpace(movement.SourceDocument)
            && await new InventoryCostingService(db)
                .ReverseReceiptLayersAsync(movement.CompanyId, movement.SourceDocument);

        if (layersRemoved)
        {
            _costReversed.Add((movement.SourceDocument!, true));
            return;
        }

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
            // (CEEF-230 · ط1-٣) ‏الدفعةُ تُقرأ من السطر المحفوظ — **فيعود ما خرج بعينه**.
            // ‏ولو رُدّت الكمّيةُ بلا رقمٍ لانكسرت هـ-٩ عند أوّل إلغاء.
            var batch = BatchRef.From(line.BatchNumber, line.ExpiryDate);

            if (movement.IsInbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId,
                    -line.Quantity, batch, movement.MovementDate, movement.Type);
            else if (movement.IsOutbound)
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId,
                    line.Quantity, batch, movement.MovementDate, movement.Type);
            else if (movement.Type == StockMovementType.Transfer)
            {
                await ApplyBalanceDeltaAsync(movement.CompanyId, movement.WarehouseId, line.ProductId,
                    line.Quantity, batch, movement.MovementDate, movement.Type);
                if (movement.ToWarehouseId is long toWh)
                    await ApplyBalanceDeltaAsync(movement.CompanyId, toWh, line.ProductId,
                        -line.Quantity, batch, movement.MovementDate, movement.Type);
            }
        }
    }

    /// <summary>
    /// (CEEF-230 · ط1-٣) <b>المَخنَقُ الوحيد لكلّ تغيّرٍ في كمّية المخزون.</b>
    ///
    /// <para>ولذلك <b>لا قيمةَ افتراضية</b> لـ<paramref name="batch"/> ولا لـ<paramref name="type"/>:
    /// المُصرِّفُ يُجبر كلَّ مستدعٍ من الاثني عشر على أن <b>يُقرّر</b> هل يحمل هويّةَ دفعةٍ أم
    /// لا. <b>وحارسٌ يُفرض في زمن الترجمة لا يُنسى</b> — وحارسٌ ذو قيمةٍ افتراضية يُنسى
    /// بالضبط في المسار الذي لم يُفكَّر فيه.</para>
    ///
    /// <para><b>ومسارٌ لا يحمل دفعةً يرفض الصنفَ المتتبَّع رفضاً صريحاً</b> — لا يمرّره
    /// بلا هويّة. فالبضاعةُ التي تدخل بلا رقمِ دفعةٍ تخرج بلا صلاحية، <b>وصمتُ المسار
    /// أخطرُ من رفضه</b>: المستخدم يظنّ التتبّعَ عاملاً وهو مثقوب.</para>
    /// </summary>
    private async Task ApplyBalanceDeltaAsync(long companyId, long warehouseId, long productId,
        decimal delta, BatchRef? batch, DateOnly onDate, StockMovementType type, bool expiredMayLeave = false)
    {
        await ApplyBatchDeltaAsync(companyId, warehouseId, productId, delta, batch, onDate, type, expiredMayLeave);

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

    /// <summary>
    /// (CEEF-230 · ط1-٣) يفرض هويّةَ الدفعة ويُحرّك رصيدَها.
    ///
    /// <para><b>وثلاثةُ قراراتٍ محسومةٌ هنا:</b></para>
    /// <list type="number">
    /// <item><b>صنفٌ غيرُ متتبَّعٍ ووردت له دفعة</b> ⟵ رفض. فقيمةٌ تُمرَّر ولا تُخزَّن
    /// أسوأُ من قيمةٍ لا تُمرَّر: المستخدم يراها في الشاشة ويظنّها محفوظة.</item>
    /// <item><b>صنفٌ متتبَّعٌ بلا دفعة</b> ⟵ رفض، ولو كان المسار هو الذي لا يحملها.</item>
    /// <item><b>المنتهيةُ تخرج ولا تُباع.</b> فالبيع (<c>Issue</c>) يُرفض عليها، أمّا
    /// الإتلافُ (<c>AdjustmentOut</c>) والتحويلُ إلى مستودع حجرٍ فيمرّان — <b>ومنعُ
    /// إخراجِ المنتهي يحبسه في الدفاتر إلى الأبد</b>، وهو ضررٌ لا حماية.</item>
    /// </list>
    /// </summary>
    private async Task ApplyBatchDeltaAsync(long companyId, long warehouseId, long productId,
        decimal delta, BatchRef? batch, DateOnly onDate, StockMovementType type, bool expiredMayLeave = false)
    {
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == productId);

        if (!product.IsTracked)
        {
            if (batch is not null)
                throw new AccountingException(
                    $"الصنف '{product.NameAr}' لا يُتتبَّع بالدفعة، فلا يُقبل له رقمُ دفعة. "
                    + "غيّرْ نمطَ تتبّعه أوّلاً إن كان يلزمه.");
            return;
        }

        if (batch is null)
            throw new AccountingException(
                $"الصنف '{product.NameAr}' يُتتبَّع بـ{TrackingName(product.Tracking)} "
                + "ولا يحمل هذا السطرُ هويّةً — فلا يدخل ولا يخرج بلا رقم.");

        if (product.Tracking == InventoryTracking.Serial && Math.Abs(delta) != 1m)
            throw new AccountingException(
                $"الصنف '{product.NameAr}' يُتتبَّع بالرقم التسلسليّ، فكلُّ سطرٍ وحدةٌ واحدة "
                + $"— ووردت {Math.Abs(delta):N4}.");

        var row = await FindBatchAsync(companyId, warehouseId, productId, batch.Value.Number);

        if (row is null)
        {
            if (delta < 0)
                throw new AccountingException(
                    $"لا توجد دفعةٌ «{batch.Value.Number}» من '{product.NameAr}' في هذا المستودع.");

            if (product.Tracking == InventoryTracking.Serial
                && await db.StockBatches.AnyAsync(b => b.CompanyId == companyId
                    && b.ProductId == productId && b.BatchNumber == batch.Value.Number
                    && b.Quantity > 0))
                throw new AccountingException(
                    $"الرقم التسلسليّ «{batch.Value.Number}» مستعمَلٌ لوحدةٍ قائمة من "
                    + $"'{product.NameAr}' — والرقمُ التسلسليّ لا يتكرّر.");

            row = StockBatch.Create(companyId, warehouseId, productId,
                batch.Value.Number, batch.Value.Expiry);
            db.StockBatches.Add(row);
        }

        row.EnsureExpiry(batch.Value.Expiry);

        // ‏(جاهزيّةُ الإطلاق · ح-١٠٥) ‏**والمنتهيةُ تخرج إلى مورّدها وحدَه** — `expiredMayLeave` يمرّره مرتجعُ المشتريات لا غيرُه،
        // ‏وافتراضيُّه الرفض: فالبيعُ والصرفُ والمطبخُ والإنتاجُ على حالها. وكان مرتجعُ الدفعة المنتهية يُدفع إلى الإتلاف فيضيع رصيدُ المورّد.
        if (delta < 0 && type == StockMovementType.Issue && !expiredMayLeave && row.IsExpiredOn(onDate))
            throw new AccountingException(
                $"الدفعة «{row.BatchNumber}» من '{product.NameAr}' منتهيةُ الصلاحية في "
                + $"{row.ExpiryDate:yyyy-MM-dd} — لا تُباع. أخرِجْها بتسويةِ إتلاف.");

        row.ApplyDelta(delta);
    }

    private static string TrackingName(InventoryTracking tracking) => tracking switch
    {
        InventoryTracking.Batch => "الدفعة",
        InventoryTracking.Serial => "الرقم التسلسليّ",
        _ => "الكمّية",
    };

    private async Task<StockBatch?> FindBatchAsync(
        long companyId, long warehouseId, long productId, string batchNumber)
    {
        var local = db.StockBatches.Local.FirstOrDefault(b =>
            b.CompanyId == companyId && b.WarehouseId == warehouseId
            && b.ProductId == productId && b.BatchNumber == batchNumber);
        if (local is not null) return local;

        return await db.StockBatches.SingleOrDefaultAsync(b =>
            b.CompanyId == companyId && b.WarehouseId == warehouseId
            && b.ProductId == productId && b.BatchNumber == batchNumber);
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
