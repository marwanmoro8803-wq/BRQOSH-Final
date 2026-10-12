using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٢ · ‏`manufacturing#9`) <b>كشفُ تكلفة الإنتاج واستهلاكُ الموادّ — من قيد الإكمال وحركة الصرف نفسَيهما.</b>
///
/// <para>‏لم يكن للتصنيع تقريرٌ واحد: لا كشفُ تكلفةٍ لأمرٍ ولا استهلاكُ موادٍّ ولا ملخّصُ إنتاج. <b>والكشفُ يُقرأ من الدفتر</b> —
/// ‏قيدُ <c>WorkOrderComplete:{id}</c> (الموادُّ دائنةً من المخزون، والأجرُ من 5106، والتحميلُ من 5107، والإجماليُّ مديناً في WIP)
/// ‏وحركةُ <c>WorkOrder:{id}</c> — لا من حسابٍ موازٍ قد يفترق عمّا رُحِّل.</para>
///
/// <para>‏أمرٌ مخطَّطٌ لعشرٍ وأُكمل بثمانٍ: خامةٌ ٢ كجم للوحدة بعشرة ⟵ ١٦ كجم = ١٦٠، وأجرٌ ٥ للوحدة = ٤٠، وتحميلٌ ١٠٪ على ٢٠٠ = ٢٠ ⟵
/// ‏٢٢٠، والوحدةُ ٢٧٫٥٠، والإنتاجيّةُ ٨٠٪؛ والخامةُ المخطَّطةُ ٢٠ كجم والمصروفةُ ١٦ — فرقٌ −٤.</para>
/// </summary>
public class TheProductionCostSheetReadsTheLedgerTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _rawId;

    public TheProductionCostSheetReadsTheLedgerTests()
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

    [Fact]
    public async Task A_completed_order_shows_its_posted_cost_split_and_unit_cost()
    {
        await SetupAsync();
        var orderId = await OrderAsync(planned: 10m, produced: 8m, on: new DateOnly(2026, 4, 5));

        var row = (await Sheet()).Single();

        Assert.Equal(orderId, row.WorkOrderId);
        Assert.Equal(new DateOnly(2026, 4, 5), row.CompletedOn);
        Assert.Equal(10m, row.PlannedQuantity);
        Assert.Equal(8m, row.ProducedQuantity);
        Assert.Equal(80m, row.YieldPercent);
        Assert.Equal(160m, row.MaterialCost);
        Assert.Equal(40m, row.LabourCost);
        Assert.Equal(20m, row.OverheadCost);
        Assert.Equal(220m, row.TotalCost);
        Assert.Equal(27.5m, row.UnitCost);
    }

    [Fact]
    public async Task Material_usage_compares_the_plan_with_what_was_issued()
    {
        await SetupAsync();
        var orderId = await OrderAsync(planned: 10m, produced: 8m, on: new DateOnly(2026, 4, 5));

        var usage = (await new ProductionCostSheetQuery(_db).MaterialUsageAsync(_companyId, orderId)).Single();

        Assert.Equal(_rawId, usage.ProductId);
        Assert.Equal(20m, usage.PlannedQuantity);
        Assert.Equal(16m, usage.IssuedQuantity);
        Assert.Equal(-4m, usage.QuantityVariance);
        Assert.Equal(160m, usage.IssuedCost);
    }

    /// <summary>شاهدُ ضبط: أمرٌ لم يُكمَل لا يظهر، وأمرٌ أُكمل خارج المدى لا يظهر.</summary>
    [Fact]
    public async Task Only_orders_completed_within_the_period_are_listed()
    {
        await SetupAsync();
        await OrderAsync(planned: 5m, produced: 5m, on: new DateOnly(2026, 4, 5));
        await OrderAsync(planned: 5m, produced: 5m, on: new DateOnly(2026, 5, 5));
        var bom = await _db.BillsOfMaterials.SingleAsync();
        var open = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 6), 5m);
        _db.WorkOrders.Add(open);
        await _db.SaveChangesAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, open.Id);

        var rows = await Sheet();

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 4, 5), rows[0].CompletedOn);
    }

    private Task<IReadOnlyList<ProductionCostRow>> Sheet() =>
        new ProductionCostSheetQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

    private async Task<long> OrderAsync(decimal planned, decimal produced, DateOnly on)
    {
        var bom = await _db.BillsOfMaterials.SingleAsync();
        var order = WorkOrder.CreateDraft(_companyId, bom.Id, on, planned);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        var service = new WorkOrderService(_db);
        await service.ReleaseAsync(_companyId, order.Id);
        await service.CompleteAsync(_companyId, order.Id, produced, "admin", on);
        return order.Id;
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000077", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-S", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
        var raw = Product.Create(_companyId, "RM-S", "خامة", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, raw);
        await _db.SaveChangesAsync();
        _rawId = raw.Id;

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouse.Id, "افتتاح خامة");
        opening.AddLine(_rawId, 100m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(_companyId, "BOM-S", "تجميع", finished.Id);
        bom.AddLine(_rawId, 2m);
        bom.SetConversionCosts(5m, 10m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();
    }
}
