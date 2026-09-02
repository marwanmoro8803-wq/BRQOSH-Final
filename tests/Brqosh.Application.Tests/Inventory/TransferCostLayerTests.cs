using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-231 · ط4-٤) <b>التحويلُ بين المستودعين ينقل التكلفةَ مع البضاعة.</b>
///
/// <para><b>ما كان:</b> التحويلُ يُحرّك الكمّياتِ <b>ولا يمسّ طبقاتِ التكلفة البتّة</b>.
/// فمستودعٌ يُفرَّغ عن آخره <b>تبقى طبقاتُه فيه كاملةً</b>، والمستقبِلُ يمتلئ بضاعةً
/// <b>بلا طبقةٍ واحدة</b>.</para>
///
/// <para><b>وأثرُه ثلاثةٌ لا واحد:</b> جردُ المستودع الأوّل يُقوَّم بمالٍ لبضاعةٍ ليست
/// فيه · والثاني بصفرٍ وفيه بضاعة · <b>وبيعٌ من الأوّل بعد ذلك يُرفض بـ«طبقات FIFO غير
/// كافية»</b> وفيه رصيدٌ ماديٌّ كامل — <b>رفضٌ كاذبٌ يُوقف البيع</b> ولا يُفهم سببُه.</para>
///
/// <para><b>وهويّةُ هـ-١٩:</b> بعد التحويل، <b>الطبقاتُ الباقية في كلّ مستودعٍ تُطابق
/// كمّيتَه</b>، ومجموعُ قيمة المخزون لا يتغيّر. <b>والتحويلُ لا يُنشئ قيداً</b> — وهو
/// صوابٌ يبقى: البضاعةُ لم تُبَع ولم تُشترَ.</para>
///
/// <para><b>ويُقاس المسارُ الذي تسلكه الشاشة</b> — <c>StockMovementPostingService</c> —
/// لا <c>InventoryService.TransferAsync</c> التي <b>بلا مستدعٍ في المستودع كلِّه</b>.
/// <b>ودرسُ <c>ط4-٣</c>: إصلاحُ أحد مسارَي حسابٍ واحدٍ يترك العطلَ حيّاً حيث يُستعمل.</b></para>
/// </summary>
public class TransferCostLayerTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _whA;
    private readonly long _whB;
    private readonly long _productId;

    public TransferCostLayerTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000009", BusinessProfile.Manufacturing)
            .GetAwaiter().GetResult();
        _companyId = company.Id;

        // ‏**والطبقاتُ لا توجد إلا في FIFO** — والمتوسطُ المرجّح عامٌّ للصنف لا يعرف
        // ‏مستودعاً، فلا يقع فيه هذا العطلُ أصلاً. فالقياسُ حيث العطل.
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        _db.SaveChanges();

        var a = Warehouse.Create(_companyId, "WH-A", "مستودع أ", isDefault: true);
        var b = Warehouse.Create(_companyId, "WH-B", "مستودع ب");
        _db.Warehouses.AddRange(a, b);

        var product = Product.Create(_companyId, "TR-P", "صنف تحويل", "PCS", ProductType.Stockable);
        _db.Products.Add(product);
        _db.SaveChanges();

        _whA = a.Id;
        _whB = b.Id;
        _productId = product.Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── هـ-١٩: الطبقاتُ تتبع الكمّيات ────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: التحويلُ يُخرج الطبقةَ من المُرسِل ويُنشئها في المستقبِل.</b>
    ///
    /// <para><b>وقيمةُ المخزون كلِّه لا تتغيّر</b> — فما خرج من هنا دخل هناك بتكلفته.</para>
    /// </summary>
    [Fact]
    public async Task Transferring_MovesTheCostLayerWithTheGoods()
    {
        await ReceiveAsync(_whA, qty: 100m, unitCost: 10m);

        await TransferAsync(40m);

        Assert.Equal(60m, await LayerQtyAsync(_whA));
        Assert.Equal(40m, await LayerQtyAsync(_whB));

        // ‏والقيمةُ الكلّية كما كانت: 100 × 10.
        Assert.Equal(1_000m, await LayerValueAsync());
    }

    /// <summary>
    /// <b>وطبقةُ المستقبِل تحمل تكلفةَ ما خرج فعلاً — لا المتوسطَ العامّ.</b>
    ///
    /// <para>مستودعٌ فيه دفعتان بتكلفتين، <b>و<c>FIFO</c> يُخرج الأقدمَ</b>. فنقلُ
    /// المتوسط العامّ يُقوّم المنقولَ بغير ما كلّف، <b>ويُبقي في المُرسِل قيمةً ليست له</b>.
    /// وقِيس بتكلفتين مختلفتين عمداً — <b>ومثالٌ بتكلفةٍ واحدة لا يُفرّق</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheDestinationLayer_CarriesTheConsumedCost_NotTheGlobalAverage()
    {
        await ReceiveAsync(_whA, qty: 10m, unitCost: 10m);
        await ReceiveAsync(_whA, qty: 10m, unitCost: 30m);

        // ‏المتوسطُ العامّ ٢٠، **والأقدمُ ١٠** — والفرقُ هو ما يقيسه هذا الاختبار.
        await TransferAsync(10m);

        var destination = await _db.InventoryCostLayers
            .Where(l => l.WarehouseId == _whB).ToListAsync();

        Assert.Single(destination);
        Assert.Equal(10m, destination[0].UnitCost);
        Assert.Equal(300m, await LayerValueAsync(_whA));
    }

    /// <summary>
    /// <b>والبيعُ من المُرسِل بعد التحويل لا يُرفض ما دام فيه رصيد.</b>
    ///
    /// <para><b>وهذا هو العطلُ كما يراه المستخدم</b>: حوّل نصفَ مخزونه ثمّ باع من الباقي،
    /// <b>فرُفض بيعُه بأنّ الطبقات لا تكفي</b> — والبضاعةُ في يده. وقبلَ الإصلاح كان
    /// الرفضُ يقع لأنّ طبقاتِ المُرسِل تكفي عدداً <b>ولا تكفي بعد أن يُستهلَك منها
    /// للمستودع الآخر</b>.</para>
    /// </summary>
    [Fact]
    public async Task SellingFromTheSourceAfterATransfer_IsNotFalselyRefused()
    {
        await ReceiveAsync(_whA, qty: 100m, unitCost: 10m);
        await TransferAsync(40m);

        var costing = new InventoryCostingService(_db);
        var (totalCost, unitCost) = await costing.ConsumeForIssueAsync(
            _companyId, _productId, _whA, 60m, "SalesInvoice:X");

        Assert.Equal(600m, totalCost);
        Assert.Equal(10m, unitCost);

        // ‏**والحفظُ لازمٌ قبل القياس**: `Sum` يُحسب في القاعدة، والاستهلاكُ ما زال
        // ‏تغييراً متعقَّباً لم يُكتب — **فقياسٌ بلا حفظٍ يقرأ ما قبل العمل**.
        await _db.SaveChangesAsync();
        Assert.Equal(0m, await LayerQtyAsync(_whA));
    }

    /// <summary>
    /// <b>وإلغاءُ التحويل يُعيد الطبقةَ إلى مكانها — ولا يُضاعف القيمة.</b>
    ///
    /// <para><b>وهذا أخطرُ ما في الإصلاح</b>: التحويلُ صادرٌ ووارِدٌ في مستندٍ واحد.
    /// <b>فعكسٌ يُرمّم المستهلَكَ ولا يُزيل المُنشأ يُضاعف قيمةَ المخزون</b> بقيمة كلّ
    /// تحويلٍ يُلغى — وهو أشدُّ من العطل الذي عولج، لا أخفّ.</para>
    /// </summary>
    [Fact]
    public async Task CancellingATransfer_RestoresTheLayers_WithoutDoublingValue()
    {
        await ReceiveAsync(_whA, qty: 100m, unitCost: 10m);
        var movement = await TransferAsync(40m);

        // ‏**والحالةُ الوسطى تُقاس عمداً.** فبغيرها يمرّ الاختبارُ بالمصادفة: طفرةٌ تمنع
        // ‏النقلَ أصلاً تُبقي الطبقاتِ في مكانها من أوّلها إلى آخرها، **فيقرأ الحارسُ ما
        // ‏ينتظره ولم يجرِ شيءٌ يُعكَس**. وقِيس ذلك بطفرةٍ نجت.
        Assert.Equal(60m, await LayerQtyAsync(_whA));
        Assert.Equal(40m, await LayerQtyAsync(_whB));

        await new StockMovementCancellationService(_db)
            .CancelAsync(_companyId, movement.Id, new DateOnly(2026, 3, 3), "admin");

        Assert.Equal(100m, await LayerQtyAsync(_whA));
        Assert.Equal(0m, await LayerQtyAsync(_whB));
        Assert.Equal(1_000m, await LayerValueAsync());

        var onHandA = await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _whA);
        Assert.Equal(100m, onHandA);
    }

    /// <summary>
    /// <b>وتحويلٌ يتجاوز الرصيد يُرفض — والحارسُ القائم يبقى.</b>
    /// </summary>
    [Fact]
    public async Task TransferringMoreThanOnHand_IsRefused()
    {
        await ReceiveAsync(_whA, qty: 10m, unitCost: 10m);

        var error = await Assert.ThrowsAsync<AccountingException>(() => TransferAsync(40m));
        Assert.Contains("غير كافٍ", error.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> LayerQtyAsync(long warehouseId) =>
        await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.WarehouseId == warehouseId)
            .SumAsync(l => l.RemainingQuantity);

    private async Task<decimal> LayerValueAsync(long? warehouseId = null) =>
        await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId
                        && (warehouseId == null || l.WarehouseId == warehouseId))
            .SumAsync(l => l.RemainingQuantity * l.UnitCost);

    private async Task ReceiveAsync(long warehouseId, decimal qty, decimal unitCost)
    {
        await new InventoryService(_db).PostDocumentMovementAsync(
            _companyId, warehouseId, StockMovementType.Receipt, new DateOnly(2026, 3, 1),
            "رصيد", $"OPEN-{Guid.NewGuid():N}", [(_productId, qty, unitCost)]);
        await _db.SaveChangesAsync();
    }

    private async Task<StockMovement> TransferAsync(decimal qty)
    {
        var movement = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 3, 2),
            StockMovementType.Transfer, _whA, "تحويل", _whB);
        movement.AddLine(_productId, qty, 10m);

        await new StockMovementPostingService(_db).PostAsync(movement);
        await _db.SaveChangesAsync();
        return movement;
    }
}
