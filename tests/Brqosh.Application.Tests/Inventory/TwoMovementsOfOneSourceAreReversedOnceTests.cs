using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١٢ · كشفه العمل) <b>حركتان لمستندٍ واحدٍ تُعكس تكلفتُهما مرّةً واحدة.</b>
///
/// <para>‏طبقاتُ التكلفة مفتاحُها <b>المستند</b> لا الحركة: الصرفُ يُسجّل ما أخذ من كلّ طبقةٍ باسم المستند، والاستلامُ يُنشئ طبقاتٍ باسمه.
/// والإلغاءُ يعكس <b>حركةً حركة</b> — فمستندٌ له حركتا صرفٍ (فاتورةٌ ووصفةُ مطبخها، أو إشعارٌ مدينٌ من مستودعين) <b>تُرمَّم طبقاتُه مرّتين</b>:
/// الأولى تُرمّم المستهلَكَ كلَّه، والثانيةُ تُرمّمه ثانيةً. فترتفع الطبقاتُ فوق الرصيد والأستاذ. وحركتا استلامٍ تُزال طبقاتُهما كلُّها مع الأولى،
/// فتجد الثانيةُ لا شيء فتسقط إلى عكس المتوسّط ثانيةً.</para>
/// </summary>
public sealed class TwoMovementsOfOneSourceAreReversedOnceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public TwoMovementsOfOneSourceAreReversedOnceTests()
    {
        TestActor.Accountant();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> طبقةٌ بعشرٍ، وصرفان بثلاثٍ وأربعٍ لمستندٍ واحد، وبيعٌ مستقلٌّ باثنتين ⟵ الباقي واحدة. وإلغاءُ المستند يُرمّم سبعاً
/// <b>مرّةً</b> فتصير ثمانياً؛ <b>وكانت تُرمَّم مرّتين</b> فتبلغ سقفَها عشراً — والرصيدُ ثمانٍ.
/// </summary>
    [Fact]
    public async Task TwoIssuesOfOneSource_RestoreTheirLayersOnce()
    {
        var (company, warehouse, product) = await FifoWithOneLayerAsync();
        await IssueAsync(company, warehouse, product, 3m, "DOC:1");
        await IssueAsync(company, warehouse, product, 4m, "DOC:1");
        await IssueAsync(company, warehouse, product, 2m, "OTHER:1");

        var inv = new InventoryService(_db);
        foreach (var movement in await _db.StockMovements.Include(m => m.Lines)
                     .Where(m => m.SourceDocument == "DOC:1").ToListAsync())
            await inv.ReverseDocumentMovementAsync(movement);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var layer = await _db.InventoryCostLayers.SingleAsync(l => l.ProductId == product);
        Assert.Equal(8m, layer.RemainingQuantity);
        Assert.Equal(8m, await new InventoryService(_db).GetOnHandAsync(company, product, warehouse));
    }

    /// <summary>
    /// <b>ووارداه يُزالان مرّةً</b> — استلامان لمستندٍ واحد (١٠ بعشرة و١٠ بعشرين) وثالثٌ مستقلٌّ (٥ بثلاثين)؛ إلغاءُ المستند يُبقي الثالثَ
    /// ومتوسّطُه ثلاثون. <b>وكانت الحركةُ الثانيةُ تجد طبقاتِها أُزيلت فتعكس المتوسّطَ ثانيةً</b> فيصير خمسين.
    /// </summary>
    [Fact]
    public async Task TwoReceiptsOfOneSource_AreRemovedOnce()
    {
        var (company, warehouse, product) = await FifoWithOneLayerAsync();
        var inv = new InventoryService(_db);
        await inv.PostDocumentMovementAsync(company, warehouse, StockMovementType.Receipt, new DateOnly(2026, 1, 2), "استلام", "DOC:R", [(product, 10m, 10m)]);
        await inv.PostDocumentMovementAsync(company, warehouse, StockMovementType.Receipt, new DateOnly(2026, 1, 2), "استلام", "DOC:R", [(product, 10m, 20m)]);
        await _db.SaveChangesAsync();
        await IssueAsync(company, warehouse, product, 10m, "OTHER:1");
        await inv.PostDocumentMovementAsync(company, warehouse, StockMovementType.Receipt, new DateOnly(2026, 1, 6), "استلام", "R3", [(product, 5m, 30m)]);
        await _db.SaveChangesAsync();

        var reverser = new InventoryService(_db);
        foreach (var movement in await _db.StockMovements.Include(m => m.Lines)
                     .Where(m => m.SourceDocument == "DOC:R").ToListAsync())
            await reverser.ReverseDocumentMovementAsync(movement);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        Assert.Equal(30m, (await _db.Products.SingleAsync(p => p.Id == product)).WeightedAverageCost);
    }

    /// <summary><b>شاهدُ ضبط</b> — حركةٌ واحدةٌ تُرمَّم كما كانت: صرفٌ بثلاث، وبيعٌ باثنتين، والإلغاءُ يُعيد الثلاث ⟵ ثمانٍ.</summary>
    [Fact]
    public async Task OneIssue_IsRestoredAsBefore()
    {
        var (company, warehouse, product) = await FifoWithOneLayerAsync();
        await IssueAsync(company, warehouse, product, 3m, "DOC:1");
        await IssueAsync(company, warehouse, product, 2m, "OTHER:1");

        var movement = await _db.StockMovements.Include(m => m.Lines).SingleAsync(m => m.SourceDocument == "DOC:1");
        await new InventoryService(_db).ReverseDocumentMovementAsync(movement);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        Assert.Equal(8m, (await _db.InventoryCostLayers.SingleAsync(l => l.ProductId == product)).RemainingQuantity);
    }

    private async Task IssueAsync(long company, long warehouse, long product, decimal qty, string source)
    {
        var (_, unit) = await new InventoryCostingService(_db).ConsumeForIssueAsync(company, product, warehouse, qty, source);
        await new InventoryService(_db).PostDocumentMovementAsync(company, warehouse, StockMovementType.Issue,
            new DateOnly(2026, 1, 5), "صرف", source, [(product, qty, unit)]);
        await _db.SaveChangesAsync();
    }

    private async Task<(long Company, long Warehouse, long Product)> FifoWithOneLayerAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();
        var warehouse = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        var product = Product.Create(company.Id, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await new InventoryService(_db).PostDocumentMovementAsync(company.Id, warehouse.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 1), "استلام", "R1", [(product.Id, 10m, 10m)]);
        await _db.SaveChangesAsync();
        return (company.Id, warehouse.Id, product.Id);
    }
}
