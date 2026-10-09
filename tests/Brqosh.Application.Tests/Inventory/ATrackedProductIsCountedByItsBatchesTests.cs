using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٩ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>الصنفُ المتتبَّع يُجرد بدفعاته.</b>
///
/// <para>كان <c>PostVariancesAsync</c> يبني سطورَه بلا دفعة، فيرفضها المَخنَقُ «ولا يحمل هذا السطرُ هويّةً» — فلا تصحّح صيدليةٌ دفعةً معدودة.
/// الآن يحمل السطرُ رقمَ الدفعة، ولقطتُه (وفحصُ البطلان) رصيدُ الدفعة لا مجموعُ الصنف، وبلا دفعةٍ يُرفض باسم الصنف.</para>
/// </summary>
public sealed class ATrackedProductIsCountedByItsBatchesTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _productId;
    private long _plainId;

    public ATrackedProductIsCountedByItsBatchesTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SetUpAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary><b>القياسُ الحاكم:</b> عجزٌ على LOT-B يُنقص LOT-B وحدَها، وLOT-A وLOT-X لا تُمسّان.</summary>
    [Fact]
    public async Task AShortageOnOneBatch_ReducesOnlyThatBatch()
    {
        var posted = await CountAsync([(_productId, "LOT-B", 10m, 7m, null)]);

        Assert.Equal(1, posted);
        Assert.Equal(7m, await BatchQtyAsync("LOT-B"));
        Assert.Equal(10m, await BatchQtyAsync("LOT-A"));
        Assert.Equal(4m, await BatchQtyAsync("LOT-X"));
        Assert.Equal(21m, await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>وفائضٌ على LOT-A يزيد LOT-A وحدَها ويحفظ انتهاءَها القائم.</summary>
    [Fact]
    public async Task ASurplusOnOneBatch_IncreasesOnlyThatBatch_KeepingItsExpiry()
    {
        await CountAsync([(_productId, "LOT-A", 10m, 12m, null)]);

        Assert.Equal(12m, await BatchQtyAsync("LOT-A"));
        Assert.Equal(10m, await BatchQtyAsync("LOT-B"));
        var a = await _db.StockBatches.AsNoTracking().SingleAsync(b => b.BatchNumber == "LOT-A");
        Assert.Equal(new DateOnly(2027, 1, 1), a.ExpiryDate);
    }

    /// <summary>وسطرٌ لمتتبَّعٍ بلا دفعةٍ يُرفض <b>باسم الصنف</b> ولا يُرحَّل شيء.</summary>
    [Fact]
    public async Task ATrackedLineWithoutABatch_IsRefusedByName_AndNothingIsPosted()
    {
        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => CountAsync([(_productId, null, 24m, 20m, null)]));

        Assert.Contains("دواء", ex.Message, StringComparison.Ordinal);
        // ‏نصُّ الحارس نفسِه — فالمَخنَقُ يرفض الحالةَ باسم الصنف أيضاً (ح-١٠٩).
        Assert.Contains("يُجرد بدفعاته", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.Equal(10m, await BatchQtyAsync("LOT-B"));
        Assert.Equal(24m, await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>وبيعٌ من الدفعة بين اللقطة والترحيل يُبطل الورقة كاملةً — ولو كان الفرقُ على دفعةٍ أخرى.</summary>
    [Fact]
    public async Task AMovementOnABatchAfterTheSnapshot_RefusesTheWholeSheet()
    {
        var sale = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 2, 1),
            StockMovementType.AdjustmentOut, _warehouseId, "صرف");
        sale.AddLine(_productId, 3m, 10m, "LOT-B", new DateOnly(2027, 6, 1));
        await new StockMovementPostingService(_db).PostAsync(sale);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => CountAsync(
            [(_productId, "LOT-A", 10m, 9m, null), (_productId, "LOT-B", 10m, 10m, null)]));

        Assert.Contains("LOT-B", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.Equal(10m, await BatchQtyAsync("LOT-A"));
        Assert.Equal(7m, await BatchQtyAsync("LOT-B"));
    }

    /// <summary>والدفعةُ المنتهيةُ يُزال رصيدُها بالجرد — فالجردُ لا يحبس المنتهيَ في الدفاتر.</summary>
    [Fact]
    public async Task AnExpiredBatch_CanBeRemovedByTheCount()
    {
        await CountAsync([(_productId, "LOT-X", 4m, 0m, null)]);

        Assert.Equal(0m, await BatchQtyAsync("LOT-X"));
        Assert.Equal(20m, await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>وغيرُ المتتبَّع كما كان: بالصنف، بلا دفعة، وعبر التوقيع القديم.</summary>
    [Fact]
    public async Task AnUntrackedProduct_IsCountedAsBefore_ThroughTheOldSignature()
    {
        await new StockCountAdjustmentService(_db).PostVariancesAsync(
            _companyId, _warehouseId, new DateOnly(2026, 3, 1),
            [(_plainId, 5m, 3m, null)], "admin");

        Assert.Equal(3m, await new InventoryService(_db).GetOnHandAsync(_companyId, _plainId, _warehouseId));
    }

    private Task<int> CountAsync(
        IReadOnlyList<(long ProductId, string? BatchNumber, decimal SystemQty, decimal CountedQty, decimal? OpeningUnitCost)> lines) =>
        new StockCountAdjustmentService(_db).PostVariancesAsync(
            _companyId, _warehouseId, new DateOnly(2026, 3, 1), lines, "admin");

    private async Task<decimal> BatchQtyAsync(string number) =>
        (await _db.StockBatches.AsNoTracking().SingleAsync(b => b.BatchNumber == number)).Quantity;

    private async Task SetUpAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-LOT", "دواء", "علبة", ProductType.Stockable, salePrice: 20m);
        product.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        var plain = Product.Create(_companyId, "P-PLAIN", "صنف عاديّ", "قطعة", ProductType.Stockable, salePrice: 5m);
        _db.Products.AddRange(product, plain);
        await _db.SaveChangesAsync();
        _productId = product.Id;
        _plainId = plain.Id;

        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 1, 1));
        order.AddLine("دواء", 24m, 10m, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();

        var receipt = GoodsReceipt.CreateDraft(_companyId, supplier.Id, order.Id, _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("دواء", 10m, 10m, 10m, product.Id, "LOT-A", new DateOnly(2027, 1, 1));
        receipt.AddLine("دواء", 10m, 10m, 10m, product.Id, "LOT-B", new DateOnly(2027, 6, 1));
        receipt.AddLine("دواء", 4m, 4m, 10m, product.Id, "LOT-X", new DateOnly(2026, 2, 1));
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();

        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.AdjustmentIn, _warehouseId, "رصيد افتتاحيّ");
        opening.AddLine(plain.Id, 5m, 2m);
        await new StockMovementPostingService(_db).PostAsync(opening);
    }
}
