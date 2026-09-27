using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>manufacturing#10</c>) <b>رحلةُ التصنيع الحرجة — شراءٌ بطبقتين، فأمرُ إنتاج، فبيعُ التامّ —
/// تقيس المالَ حيث يظهر: تكلفةَ المبيعات.</b>
///
/// <para><b>ما كان:</b> معيارُ الإطلاق بلا رحلةٍ للتصنيع، ودليلُه الحيُّ «أوامر الإنتاج GridRows:1 · قوائم المواد GridRows:1»
/// وميزانُ مراجعةٍ متوازن. <b>وقيودُ أمر الإنتاج تتقابل على المخزون (1107) وWIP (1113) فصافيها صفر</b> — فخطأٌ في تكلفة
/// المنتَج التامّ <b>لا يراه الميزانُ أبداً</b>، ويخرج في كلّ بيعةٍ لاحقةٍ تكلفةَ مبيعاتٍ خاطئة. <b>فأوّلُ عميل تصنيعٍ هو
/// الاختبار.</b></para>
///
/// <para><b>والأرقامُ مختارةٌ لتُسقط ما يبدو صحيحاً:</b> خامةٌ بطبقتين (٥ بعشرة ثمّ ٥ بأربعة عشر)، وقائمةٌ تستهلك ثلاثاً للوحدة،
/// وأمرٌ لوحدتين ⟵ ستٌّ بالوارد-أوّلاً = ٥٠ + ١٤ = <b>٦٤، فالوحدةُ ٣٢</b> — لا ٣٠ (الطبقةُ الأولى وحدَها) ولا ٣٦ (الثانية)
/// ولا ٣٦٫٤ (المتوسّط).</para>
/// </summary>
public class TheManufacturingJourneyCostsWhatItConsumesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _rawId;
    private long _finishedId;
    private long _customerId;

    public TheManufacturingJourneyCostsWhatItConsumesTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>المنتَجُ التامُّ يدخل المخزونَ بما استهلكه بعينه — ٢ × ٣٢ = ٦٤، والخامةُ الباقيةُ ٤ × ١٤.</summary>
    [Fact]
    public async Task TheFinishedGoods_EnterStockAtWhatTheyConsumed()
    {
        await SetupAsync();
        await ProduceTwoAsync();

        var finished = await LayersAsync(_finishedId);
        Assert.Equal(2m, finished.Sum(l => l.RemainingQuantity));
        Assert.All(finished, l => Assert.Equal(32m, l.UnitCost));

        var raw = await LayersAsync(_rawId);
        Assert.Equal(4m, raw.Sum(l => l.RemainingQuantity));
        Assert.All(raw, l => Assert.Equal(14m, l.UnitCost));
    }

    /// <summary>
    /// <b>والحاكم:</b> بيعُ التامّ يُرحّل تكلفةَ مبيعاتٍ = ما استهلكه التصنيعُ حرفاً (٦٤)، والمخزونُ الباقي = ما اشتُري − ما بيع
    /// (١٢٠ − ٦٤ = ٥٦) <b>في الطبقات وفي حساب المخزون معاً</b>.
    /// </summary>
    [Fact]
    public async Task SellingTheFinishedGoods_CostsExactlyWhatTheyConsumed()
    {
        await SetupAsync();
        await ProduceTwoAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 1, 10));
        invoice.AddLine("منتَج", 2m, 50m, _finishedId);
        invoice = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Equal(64m, entry.Lines
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold).Sum(l => l.Debit));

        var remaining = (await LayersAsync(_rawId)).Concat(await LayersAsync(_finishedId))
            .Sum(l => l.RemainingQuantity * l.UnitCost);
        Assert.Equal(56m, remaining);
        Assert.Equal(56m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ — ولماذا لم يكفِ الميزان:</b> قيدا الأمر يتقابلان على المخزون وWIP فصافيهما صفر، <b>أيّاً كانت تكلفةُ
    /// الوحدة</b>. فهذا يبقى أخضرَ حين تفسد التكلفة، والحارسان أعلاه يسقطان.
    /// </summary>
    [Fact]
    public async Task TheWorkOrderJournals_NetToZero_SoTheTrialBalanceCannotSeeTheUnitCost()
    {
        await SetupAsync();
        var inventoryBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory);
        var wipBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.WorkInProgress);

        await ProduceTwoAsync();

        Assert.Equal(inventoryBefore, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
        Assert.Equal(wipBefore, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.WorkInProgress));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع الرحلة", "310123456700033", BusinessProfile.Manufacturing);
        _companyId = company.Id;
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        var raw = Product.Create(_companyId, "RAW", "خامة", "كجم", ProductType.Stockable);
        var finished = Product.Create(_companyId, "FG", "منتَج", "قطعة", ProductType.Stockable);
        var customer = Customer.Create(_companyId, "عميل");
        _db.Products.AddRange(raw, finished);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _rawId = raw.Id;
        _finishedId = finished.Id;
        _customerId = customer.Id;
        _warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;

        // ‏طبقتان بسعرين — عبر مسار الترحيل الذي يكتب المخزونَ والقيدَ معاً.
        foreach (var (date, cost) in new[] { (new DateOnly(2026, 1, 1), 10m), (new DateOnly(2026, 1, 2), 14m) })
        {
            var receipt = StockMovement.CreateDraft(_companyId, date, StockMovementType.Opening, _warehouseId, "خامة");
            receipt.AddLine(_rawId, 5m, cost);
            await new StockMovementPostingService(_db).PostAsync(receipt);
        }
    }

    private async Task ProduceTwoAsync()
    {
        var bom = BillOfMaterials.Create(_companyId, "BOM-J", "قائمة الرحلة", _finishedId);
        bom.AddLine(_rawId, 3m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 1, 5), 2m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        var service = new WorkOrderService(_db);
        await service.ReleaseAsync(_companyId, order.Id);
        await service.CompleteAsync(_companyId, order.Id, 2m, "admin", new DateOnly(2026, 1, 5));
    }

    private Task<List<InventoryCostLayer>> LayersAsync(long productId) =>
        _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == productId && l.RemainingQuantity > 0)
            .ToListAsync();

    private async Task<decimal> BalanceAsync(string accountCode) =>
        (await _db.JournalEntries.Where(e => e.CompanyId == _companyId && e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines).Where(l => l.AccountCode == accountCode)
            .Select(l => new { l.Debit, l.Credit }).ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
