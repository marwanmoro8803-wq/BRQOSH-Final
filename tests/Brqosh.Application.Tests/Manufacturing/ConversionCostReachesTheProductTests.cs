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
/// (جاهزيّةُ الإطلاق · ح-١٣) <b>تكلفةُ المنتَج التامّ كانت موادَّ فقط.</b>
///
/// <para>معيارُ المخزون يقضي أن تشمل التكلفةُ <b>تكاليفَ التحويل</b> — الأجرَ المباشرَ
/// ونصيباً منهجيّاً من التكاليف الصناعيّة. <b>وكان <c>WorkOrderService</c> يحسب
/// <c>materialCost</c> وحدَه</b>، فيدخل التامُّ المخزونَ بأقلَّ من كلفته.</para>
///
/// <para><b>والأثرُ يمتدّ إلى ما بعد الإنتاج:</b> مخزونٌ مُقوَّمٌ بأدنى من حقيقته،
/// <b>وتكلفةُ مبيعاتٍ تخرج ناقصةً في كلّ بيعةٍ لاحقة</b> — فيُعلَن ربحٌ أعلى من الواقع
/// على كلّ أمرِ إنتاج، <b>وتُدفع عليه زكاةٌ ويُوزَّع</b>.</para>
///
/// <para><b>وحسابا <c>5106</c> و<c>5107</c> موجودان في الدليل منذ نشأته ولا يكتبهما أحد</b> —
/// فالعطلُ لم يكن نقصَ دليلٍ بل نقصَ تحميل.</para>
/// </summary>
public class ConversionCostReachesTheProductTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _finishedId;
    private long _rawId;
    private long _warehouseId;

    public ConversionCostReachesTheProductTests()
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

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم.</b> عشرُ وحداتٍ، موادُّها ٢٠٠ (٢٠ كجم × ١٠)، وأجرٌ ٥ للوحدة
    /// (٥٠)، ونسبةٌ صناعيّةٌ ١٠٪ على (٢٠٠+٥٠) = ٢٥. <b>فالتكلفةُ ٢٧٥ لا ٢٠٠</b>،
    /// والوحدةُ ٢٧٫٥٠.
    /// </summary>
    [Fact]
    public async Task FinishedGoods_CarryMaterialsPlusLabourAndOverhead()
    {
        await SetupAsync(labourPerUnit: 5m, overheadPercent: 10m);
        await CompleteAsync(produced: 10m);

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var finished = rows.Single(r => r.ProductId == _finishedId);

        Assert.Equal(10m, finished.Quantity);
        Assert.Equal(275m, Math.Round(finished.TotalValue, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// <b>والتحميلُ يبلغ حسابَيه</b> — <c>5106</c> بالأجر و<c>5107</c> بالنصيب الصناعيّ،
    /// <b>دائنَين</b>: فالتكلفةُ تُنقَل إلى المخزون ولا تُخترع.
    /// </summary>
    [Fact]
    public async Task TheAbsorption_ReachesItsTwoAccounts()
    {
        await SetupAsync(labourPerUnit: 5m, overheadPercent: 10m);
        await CompleteAsync(produced: 10m);

        Assert.Equal(-50m, await LedgerAsync("5106"));
        Assert.Equal(-25m, await LedgerAsync("5107"));
    }

    /// <summary>
    /// <b>وWIP يُغلق تماماً</b> — يُحمَّل بالثلاثة ويُفرَّغ بالثلاثة، <b>فلا يبقى فيه
    /// ريالٌ عالق</b> بعد إكمال الأمر.
    /// </summary>
    [Fact]
    public async Task WorkInProgress_ClosesToZero()
    {
        await SetupAsync(labourPerUnit: 5m, overheadPercent: 10m);
        await CompleteAsync(produced: 10m);

        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبط:</b> قائمةُ موادٍّ بلا تكاليفِ تحويل تبقى كما كانت حرفاً — <b>التكلفةُ
    /// موادُّ فقط ولا يُمسّ <c>5106</c> ولا <c>5107</c></b>. ولولاه لجاز أن يُحمَّل كلُّ
    /// أمرٍ بشيءٍ لم يُطلب.
    /// </summary>
    [Fact]
    public async Task WithoutConversionCosts_NothingChanges()
    {
        await SetupAsync(labourPerUnit: 0m, overheadPercent: 0m);
        await CompleteAsync(produced: 10m);

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        Assert.Equal(200m, Math.Round(
            rows.Single(r => r.ProductId == _finishedId).TotalValue, 2, MidpointRounding.AwayFromZero));

        Assert.Equal(0m, await LedgerAsync("5106"));
        Assert.Equal(0m, await LedgerAsync("5107"));
    }

    /// <summary>
    /// <b>والنسبةُ تُحمَّل على الموادّ والأجر معاً</b> — لا على الموادّ وحدَها.
    /// أجرٌ صفرٌ ونسبةٌ ٢٥٪ ⟵ ٢٠٠ + ٥٠ = ٢٥٠.
    /// </summary>
    [Fact]
    public async Task TheOverheadBase_IncludesLabour()
    {
        await SetupAsync(labourPerUnit: 0m, overheadPercent: 25m);
        await CompleteAsync(produced: 10m);

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        Assert.Equal(250m, Math.Round(
            rows.Single(r => r.ProductId == _finishedId).TotalValue, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>وتكاليفُ تحويلٍ خارج المدى تُرفض في موضع ضبطها لا بعد الإنتاج.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 101)]
    public void OutOfRangeConversionCosts_AreRefused(decimal labour, decimal overhead)
    {
        var bom = BillOfMaterials.Create(1L, "BOM-Z", "تجميع", 2L);

        Assert.Throws<AccountingException>(() => bom.SetConversionCosts(labour, overhead));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync(decimal labourPerUnit, decimal overheadPercent)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000077", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-C", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
        var raw = Product.Create(_companyId, "RM-C", "خامة", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, raw);
        await _db.SaveChangesAsync();
        _finishedId = finished.Id;
        _rawId = raw.Id;

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        _warehouseId = warehouse.Id;

        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, _warehouseId, "افتتاح خامة");
        opening.AddLine(_rawId, 100m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(_companyId, "BOM-C", "تجميع", _finishedId);
        bom.AddLine(_rawId, 2m);
        bom.SetConversionCosts(labourPerUnit, overheadPercent);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();
    }

    private async Task CompleteAsync(decimal produced)
    {
        var bom = await _db.BillsOfMaterials.SingleAsync(b => b.CompanyId == _companyId);
        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 1), produced);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        var service = new WorkOrderService(_db);
        await service.ReleaseAsync(_companyId, order.Id);
        await service.CompleteAsync(_companyId, order.Id, produced, "admin");
    }

    /// <summary>رصيدٌ مُوقَّع: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
