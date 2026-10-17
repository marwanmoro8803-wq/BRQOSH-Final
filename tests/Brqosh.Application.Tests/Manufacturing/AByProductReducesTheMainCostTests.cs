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
/// (جاهزيّةُ الإطلاق · ح-١٤٩ · <c>manufacturing#4</c>) <b>المنتجُ الثانويّ يدخل المخزونَ بقيمته، وتُطرح من تكلفة الرئيسيّ.</b>
///
/// <para><b>ما كان:</b> لا منتجَ ثانويّ — فالنخالةُ وقصاصةُ الحديد إمّا لا تدخل المخزونَ أصلاً (فتُباع بلا تكلفةٍ وبلا رصيد)،
/// وإمّا تُدخَل بقيدٍ يدويٍّ وتكلفتُها محمولةٌ على وحدة الرئيسيّ مرّتين.</para>
///
/// <para>الوصفة: ٤ قطعٍ من ٦ «أ» (٢ ريال) و٢ «ب» (١٠ ريالات)؛ والأمر ٨ قطع — فالتكلفةُ ٦٤.</para>
/// </summary>
public class AByProductReducesTheMainCostTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _finishedId;
    private long _byProductId;
    private long _orderId;

    public AByProductReducesTheMainCostTests()
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
    /// <b>القياسُ الحاكم: ثانويٌّ بكميّتين وثلاثة ريالات = ستّة، فالرئيسيُّ ثمانٍ بثمانيةٍ وخمسين</b> — والمجموعُ أربعةٌ وستّون كما خرج من WIP.
    /// </summary>
    [Fact]
    public async Task TheByProduct_IsReceivedAtItsValue_AndTheMainProductCostsTheRest()
    {
        await SetupAsync();

        await CompleteAsync([(_byProductId, 2m, 3m)]);

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var main = rows.Single(r => r.ProductId == _finishedId);
        var by = rows.Single(r => r.ProductId == _byProductId);
        Assert.Equal((8m, 58m), (main.Quantity, Math.Round(main.TotalValue, 2)));
        Assert.Equal((2m, 6m), (by.Quantity, Math.Round(by.TotalValue, 2)));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess));
    }

    [Fact]
    public async Task ByProductsWorthMoreThanTheBatch_AreRefused()
    {
        await SetupAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => CompleteAsync([(_byProductId, 10m, 7m)]));

        Assert.Contains("تتجاوز تكلفةَ الدفعة", ex.Message, StringComparison.Ordinal);
        Assert.Equal(WorkOrderStatus.Released, (await _db.WorkOrders.AsNoTracking().SingleAsync(o => o.Id == _orderId)).Status);
    }

    [Fact]
    public async Task TheMainProduct_CannotBeItsOwnByProduct()
    {
        await SetupAsync();

        await Assert.ThrowsAsync<AccountingException>(() => CompleteAsync([(_finishedId, 1m, 1m)]));
    }

    /// <summary><b>وإلغاءُ الأمر يُخرج الثانويَّ مع الرئيسيّ.</b></summary>
    [Fact]
    public async Task CancellingTheOrder_TakesTheByProductOutToo()
    {
        await SetupAsync();
        await CompleteAsync([(_byProductId, 2m, 3m)]);

        await new WorkOrderService(_db).CancelAsync(_companyId, _orderId, "admin", new DateOnly(2026, 4, 6));

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        Assert.Equal(0m, rows.Where(r => r.ProductId == _byProductId).Sum(r => r.Quantity));
        Assert.Equal(0m, rows.Where(r => r.ProductId == _finishedId).Sum(r => r.Quantity));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000149", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-149", "دقيق", "كيس", ProductType.Stockable, 0m);
        var byProduct = Product.Create(_companyId, "BP-149", "نخالة", "كيس", ProductType.Stockable, 0m);
        var rawA = Product.Create(_companyId, "RM-A", "قمح", "كجم", ProductType.Stockable, 0m);
        var rawB = Product.Create(_companyId, "RM-B", "تعبئة", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, byProduct, rawA, rawB);
        await _db.SaveChangesAsync();
        _finishedId = finished.Id;
        _byProductId = byProduct.Id;

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouse.Id, "افتتاح الخامات");
        opening.AddLine(rawA.Id, 100m, 2m);
        opening.AddLine(rawB.Id, 50m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(_companyId, "BOM-149", "طحن", _finishedId, outputQuantity: 4m);
        bom.AddLine(rawA.Id, 6m);
        bom.AddLine(rawB.Id, 2m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 1), 8m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        _orderId = order.Id;
        await new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id);
    }

    private Task CompleteAsync(IReadOnlyList<(long ProductId, decimal Quantity, decimal UnitValue)> byProducts) =>
        new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, 8m, "admin", new DateOnly(2026, 4, 5), byProducts: byProducts);

    private async Task<decimal> BalanceAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
