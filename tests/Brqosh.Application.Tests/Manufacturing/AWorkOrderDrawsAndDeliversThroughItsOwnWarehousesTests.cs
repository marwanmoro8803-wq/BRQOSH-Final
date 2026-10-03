using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٧ · <c>manufacturing#7</c>) <b>أمرُ الإنتاج يصرف من مستودع موادّه ويُسلِّم إلى مستودع إنتاجه — وكان المستودعَ الافتراضيَّ أبداً.</b>
///
/// <para><c>WorkOrderService.CompleteAsync</c> كان يأخذ <c>GetDefaultWarehouseAsync</c> للصرف والاستلام معاً، ولا حقلَ على الأمر يقول غيرَه —
/// <b>ومصنعٌ يحفظ خاماته في «مستودع المصنع» لا يُكمل أمراً</b>: قِيس على <c>HEAD</c> — ١٠٠ خامةٍ في المصنع والإكمالُ يُرفض بـ«رصيد 'خامة' غير كافٍ —
/// المتوفر 0.00»؛ والتامُّ يدخل الافتراضيَّ ولو كان له مستودع. <b>والاستلامُ والجردُ والتحويلُ تختار أيَّ مستودع</b>، فالتصنيعُ وحدَه كان حبيسَ الافتراضيّ.</para>
///
/// <para>ثلاثةُ مستودعات: <b>الرئيسيُّ</b> (الافتراضيّ)، و<b>«مستودع المصنع»</b> فيه ١٠٠ خامةٍ بـ٢٫٠٠، و<b>«مستودع التامّ»</b>. قائمةٌ تُخرج وحدةً
/// من ٣ خامات؛ والأمرُ ٥ ⟵ <b>١٥ خامةً تخرج من المصنع (يبقى ٨٥)، و٥ تامّةٌ بـ٣٠ تدخل مستودعَ التامّ</b>. <b>وفي الوارد-أوّلاً</b> يحمل الرئيسيُّ
/// خامةً أغلى (١٠٠ بـ٩٫٠٠) — <b>فصرفٌ من غير مستودع الموادّ يُخطئ المالَ لا الكمّيّةَ وحدَها</b> (١٣٥ بدل ٣٠).</para>
/// </summary>
public class AWorkOrderDrawsAndDeliversThroughItsOwnWarehousesTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _finishedId;
    private long _rawId;
    private long _mainId;
    private long _factoryId;
    private long _finishedStoreId;
    private long _bomId;

    public AWorkOrderDrawsAndDeliversThroughItsOwnWarehousesTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: الخامةُ تخرج من مستودع الموادّ، والتامُّ يدخل مستودعَ الإنتاج بتكلفته</b> — والافتراضيُّ لا خامةَ فيه ولا تامّ.
    /// </summary>
    [Fact]
    public async Task TheOrder_DrawsFromItsMaterialsWarehouse_AndDeliversToItsOutputWarehouse()
    {
        await SetupAsync(rawIn: "factory", fifo: false);
        var orderId = await NewOrderAsync(_factoryId, _finishedStoreId);

        await CompleteAsync(orderId);

        Assert.Equal(85m, await OnHandAsync(_factoryId, _rawId));
        Assert.Equal(5m, await OnHandAsync(_finishedStoreId, _finishedId));
        Assert.Equal(30m, await ValueAsync(_finishedStoreId, _finishedId));
        Assert.Equal(0m, await OnHandAsync(_mainId, _rawId));
        Assert.Equal(0m, await OnHandAsync(_mainId, _finishedId));
    }

    /// <summary>
    /// <b>وفي الوارد-أوّلاً يُكلَّف الأمرُ بطبقات مستودع موادّه</b> — ٣٠ لا ١٣٥؛ وطبقةُ التامّ في مستودع الإنتاج، وطبقةُ الرئيسيّ الأغلى لا تُمسّ.
    /// </summary>
    [Fact]
    public async Task UnderFifo_TheOrderCostsTheLayersOfItsMaterialsWarehouse()
    {
        await SetupAsync(rawIn: "both", fifo: true);
        var orderId = await NewOrderAsync(_factoryId, _finishedStoreId);

        await CompleteAsync(orderId);

        Assert.Equal(30m, await LayerValueAsync(_finishedStoreId, _finishedId));
        Assert.Equal(170m, await LayerValueAsync(_factoryId, _rawId));
        Assert.Equal(900m, await LayerValueAsync(_mainId, _rawId));
        Assert.Equal(0m, await LayerValueAsync(_mainId, _finishedId));
    }

    /// <summary>
    /// <b>وأمرٌ بلا مستودعاتٍ يبقى على الافتراضيّ</b> — كما كان كلُّ أمرٍ قبله، فلا يتغيّر شيءٌ لأمرٍ قائمٍ بالترقية.
    /// </summary>
    [Fact]
    public async Task AnOrderWithoutWarehouses_StaysOnTheDefault()
    {
        await SetupAsync(rawIn: "main", fifo: false);
        var orderId = await NewOrderAsync(null, null);

        await CompleteAsync(orderId);

        Assert.Equal(85m, await OnHandAsync(_mainId, _rawId));
        Assert.Equal(5m, await OnHandAsync(_mainId, _finishedId));
        Assert.Equal(30m, await ValueAsync(_mainId, _finishedId));
    }

    /// <summary>
    /// <b>ومستودعٌ أُوقف بين الإصدار والإكمال يُرفض باسمه</b> — ولا تُرحَّل حركةٌ ولا قيد؛ فلا يُسلِّم الأمرُ إلى مستودعٍ أوقفه صاحبُه.
    /// </summary>
    [Fact]
    public async Task AStoppedWarehouse_IsRefusedAtCompletion_ByName()
    {
        await SetupAsync(rawIn: "factory", fifo: false);
        var orderId = await NewOrderAsync(_factoryId, _finishedStoreId);
        var store = await _db.Warehouses.SingleAsync(w => w.Id == _finishedStoreId);
        store.Deactivate();
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => CompleteAsync(orderId));

        Assert.Contains("مستودع التامّ", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.StockMovements.AnyAsync(m => m.SourceDocument == $"WorkOrder:{orderId}"));
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == $"WorkOrderComplete:{orderId}"));
        Assert.Equal(100m, await OnHandAsync(_factoryId, _rawId));
    }

    /// <summary>
    /// <b>وإلغاءُ الأمر المكتمل يُعيد كلَّ حركةٍ إلى مستودعها</b> — الخامةُ إلى المصنع بطبقتها (١٠٠ و٢٠٠)، والتامُّ يخرج من مستودع التامّ.
    /// </summary>
    [Fact]
    public async Task CancellingTheOrder_ReturnsEachMovementToItsOwnWarehouse()
    {
        await SetupAsync(rawIn: "both", fifo: true);
        var orderId = await NewOrderAsync(_factoryId, _finishedStoreId);
        await CompleteAsync(orderId);

        await new WorkOrderService(_db).CancelAsync(_companyId, orderId, "admin", new DateOnly(2026, 4, 10));

        Assert.Equal(100m, await OnHandAsync(_factoryId, _rawId));
        Assert.Equal(200m, await LayerValueAsync(_factoryId, _rawId));
        Assert.Equal(900m, await LayerValueAsync(_mainId, _rawId));
        Assert.Equal(0m, await OnHandAsync(_finishedStoreId, _finishedId));
        Assert.Equal(0m, await LayerValueAsync(_finishedStoreId, _finishedId));
    }

    /// <summary><b>والمجالُ لا يقبل مستودعاً لأمرٍ انتهى ولا معرّفاً غيرَ موجب</b>.</summary>
    [Fact]
    public void TheDomain_RefusesWarehousesOnAFinishedOrder()
    {
        var order = WorkOrder.CreateDraft(1, 1, new DateOnly(2026, 4, 1), 5m);
        Assert.Throws<AccountingException>(() => order.SetWarehouses(0, null));

        order.SetWarehouses(7, 8);
        Assert.Equal((7L, 8L), (order.MaterialsWarehouseId!.Value, order.OutputWarehouseId!.Value));

        order.Cancel();
        Assert.Throws<AccountingException>(() => order.SetWarehouses(7, 9));
    }

    /// <summary>
    /// <b>ونافذةُ الأمر تحمل الحقلين وتضبطهما</b> — قياسٌ بنيويٌّ على المصدر: النافذةُ تفتح دفاترَ الإنتاج (<c>AppServices.CreateDb</c>)
    /// ولا تُشغَّل في اختبار، على سُنّة ح-٦٦ وح-٩٦.
    /// </summary>
    [Fact]
    public void TheWorkOrderDialog_ChoosesBothWarehouses()
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pages, "WorkOrderDialog.xaml"));
        var code = File.ReadAllText(Path.Combine(pages, "WorkOrderDialog.xaml.cs"));

        Assert.Contains("x:Name=\"MaterialsWarehouseCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OutputWarehouseCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains(".SetWarehouses(", code, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <param name="rawIn"><c>factory</c> · <c>main</c> · <c>both</c> (المصنعُ بـ٢٫٠٠ والرئيسيُّ بـ٩٫٠٠).</param>
    private async Task SetupAsync(string rawIn, bool fifo)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000077", BusinessProfile.Manufacturing);
        _companyId = company.Id;
        if (fifo)
        {
            company.SetCostingMethod(InventoryCostingMethod.Fifo);
            await _db.SaveChangesAsync();
        }

        var finished = Product.Create(_companyId, "FG-97", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
        var raw = Product.Create(_companyId, "RM-97", "خامة", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, raw);
        await _db.SaveChangesAsync();
        _finishedId = finished.Id;
        _rawId = raw.Id;

        _mainId = (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id;
        var factory = Warehouse.Create(_companyId, "WH-FAC", "مستودع المصنع");
        var finishedStore = Warehouse.Create(_companyId, "WH-FG", "مستودع التامّ");
        _db.Warehouses.AddRange(factory, finishedStore);
        await _db.SaveChangesAsync();
        _factoryId = factory.Id;
        _finishedStoreId = finishedStore.Id;

        if (rawIn is "factory" or "both")
            await OpenAsync(_factoryId, 2m);
        if (rawIn is "main" or "both")
            await OpenAsync(_mainId, rawIn == "both" ? 9m : 2m);

        var bom = BillOfMaterials.Create(_companyId, "BOM-97", "تجميع", _finishedId, outputQuantity: 1m);
        bom.AddLine(_rawId, 3m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();
        _bomId = bom.Id;
    }

    private async Task OpenAsync(long warehouseId, decimal unitCost)
    {
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouseId, "افتتاح الخامة");
        opening.AddLine(_rawId, 100m, unitCost);
        await new StockMovementPostingService(_db).PostAsync(opening);
    }

    private async Task<long> NewOrderAsync(long? materialsWarehouseId, long? outputWarehouseId)
    {
        var order = WorkOrder.CreateDraft(_companyId, _bomId, new DateOnly(2026, 4, 1), 5m);
        if (materialsWarehouseId is not null || outputWarehouseId is not null)
            order.SetWarehouses(materialsWarehouseId, outputWarehouseId);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id);
        return order.Id;
    }

    private Task CompleteAsync(long orderId) =>
        new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 5m, "admin", new DateOnly(2026, 4, 5));

    private async Task<IReadOnlyList<StockBalanceRow>> RowsAsync(long warehouseId, long productId) =>
        (await new StockBalanceQuery(_db).ExecuteAsync(_companyId, warehouseId, includeZeroBalances: true))
        .Where(r => r.ProductId == productId).ToList();

    private async Task<decimal> OnHandAsync(long warehouseId, long productId) =>
        (await RowsAsync(warehouseId, productId)).Sum(r => r.Quantity);

    /// <summary>قيمةُ المستودع بالمتوسّط المرجّح — الطريقةُ الافتراضيّة، ولا طبقاتَ فيها.</summary>
    private async Task<decimal> ValueAsync(long warehouseId, long productId) =>
        (await RowsAsync(warehouseId, productId)).Sum(r => r.TotalValue);

    /// <summary>قيمةُ طبقات الوارد-أوّلاً الباقية في المستودع — وفيها يُرى من أيّ مستودعٍ صُرف.</summary>
    private async Task<decimal> LayerValueAsync(long warehouseId, long productId)
    {
        var layers = await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.WarehouseId == warehouseId && l.ProductId == productId)
            .Select(l => new { l.RemainingQuantity, l.UnitCost })
            .ToListAsync();
        return Math.Round(layers.Sum(l => l.RemainingQuantity * l.UnitCost), 2, MidpointRounding.AwayFromZero);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}
