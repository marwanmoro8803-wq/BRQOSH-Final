using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م2-٢١) <b>أوّلُ جردٍ في حياة المنشأة — وكان يُرفض كاملاً.</b>
///
/// <para><b>السيناريو ليس طرفياً بل هو اليوم الأول:</b> المنشأة تُدخل دليل أصنافها ثم تجري
/// جرداً لإثبات أرصدتها الافتتاحية. وكلُّ صنفٍ لم يُشترَ بعدُ متوسطُ تكلفته <b>صفر</b>.</para>
///
/// <para><b>وما كان يقع:</b> خدمةُ الجرد تبني سطر الفائض بـ<c>product.WeightedAverageCost</c>
/// فيصير <c>TotalValue = 0</c>، ثم يبني القيدُ سطراً بـ<c>AddLine(inventory, 0m, 0m)</c>
/// فيرمي <c>JournalEntry</c>: «سطر القيد يجب أن يحمل مبلغاً مديناً أو دائناً». والكلُّ داخل
/// معاملةٍ واحدة، <b>فيرتدّ الجردُ بأكمله</b> — لا فائض ولا عجز. والرسالة لا علاقة لها بما
/// فعل المستخدم، وعمودُ «تكلفة الوحدة» في الشاشة للقراءة فقط فلا يستطيع تصحيحه.</para>
///
/// <para><b>والإصلاح محاسبيّ لا التفافيّ:</b> حركةٌ قيمتُها صفر <b>لا أثر محاسبيّ لها</b>،
/// فلا يُبنى لها قيدٌ أصلاً — لا قيدٌ بأصفار. الكمية تُسجَّل، والأستاذ لا يتغيّر، وهوية المخزون
/// (رصيد الحساب == Σ الكمية × المتوسط) تبقى صادقة لأن الطرفين صفر.</para>
/// </summary>
public class FirstStockCountTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly List<long> _productIds = [];

    public FirstStockCountTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر جديد", "310123456700099", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();

        for (var i = 1; i <= 5; i++)
        {
            var product = Product.Create(
                _companyId, $"P-{i}", $"صنف {i}", "قطعة", ProductType.Stockable, 100m);
            _db.Products.Add(product);
            _db.SaveChanges();
            _productIds.Add(product.Id);
        }

        _warehouseId = _db.Warehouses.Single(w => w.CompanyId == _companyId).Id;
    }

    private async Task<decimal> InventoryLedgerBalanceAsync()
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var row = rows.SingleOrDefault(r =>
            r.AccountCode == SaudiChartOfAccounts.SystemCodes.Inventory);
        return row is null ? 0m : row.BalanceDebit - row.BalanceCredit;
    }

    private async Task<decimal> StockValueAsync()
    {
        var inv = new InventoryService(_db);
        var total = 0m;
        foreach (var id in _productIds)
        {
            var qty = await inv.GetOnHandAsync(_companyId, id, _warehouseId);
            var product = await _db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == id);
            total += qty * product.WeightedAverageCost;
        }
        return total;
    }

    private async Task<int> CountAsync(params (long ProductId, decimal Counted)[] rows)
    {
        var inv = new InventoryService(_db);
        var lines = new List<(long ProductId, decimal SystemQty, decimal CountedQty)>();
        foreach (var (id, counted) in rows)
            lines.Add((id, await inv.GetOnHandAsync(_companyId, id, _warehouseId), counted));

        return await new StockCountAdjustmentService(_db).PostVariancesAsync(
            _companyId, _warehouseId, new DateOnly(2026, 1, 10), lines, "admin");
    }

    /// <summary>
    /// <b>القياس المركزيّ — دورة التأسيس الحقيقية.</b> خمسةُ أصنافٍ لم تُشترَ قطّ، جردٌ يُثبت
    /// كمياتها. يجب أن ينجح، وأن تُسجَّل الكميات، وأن تبقى هوية المخزون صادقة.
    /// </summary>
    [Fact]
    public async Task FirstCount_WithNoCostHistory_Posts()
    {
        var applied = await CountAsync([.. _productIds.Select(id => (id, 10m))]);

        Assert.True(applied > 0, "لم تُرحَّل أيّ حركة — ارتدّ الجرد بأكمله.");

        var inv = new InventoryService(_db);
        foreach (var id in _productIds)
            Assert.Equal(10m, await inv.GetOnHandAsync(_companyId, id, _warehouseId));

        // هوية المخزون: الأستاذ يساوي قيمة المخزون — والطرفان صفر، وهذا صادقٌ لا خطأ.
        Assert.Equal(await StockValueAsync(), await InventoryLedgerBalanceAsync());
    }

    /// <summary>
    /// <b>حارسٌ للحارس — ولولاه لكان «لا تُنشئ قيداً أبداً» يُمرّر الاختبار السابق:</b>
    /// جردٌ لأصنافٍ <b>لها تكلفة</b> يجب أن يُنتج قيداً بقيمته، وأن تبقى الهوية صادقة بقيمةٍ
    /// غير صفرية.
    /// </summary>
    [Fact]
    public async Task ACountOnPricedStock_StillPostsItsJournalEntry()
    {
        var product = _productIds[0];
        var opening = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 5), StockMovementType.Opening, _warehouseId, "افتتاحيّ");
        opening.AddLine(product, 20m, 30m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var ledgerBefore = await InventoryLedgerBalanceAsync();

        await CountAsync((product, 25m));   // فائض 5 بتكلفة 30

        Assert.Equal(ledgerBefore + 150m, await InventoryLedgerBalanceAsync());
        Assert.Equal(await StockValueAsync(), await InventoryLedgerBalanceAsync());
    }

    /// <summary>
    /// وجردٌ يخلط الاثنين — صنفٌ له تكلفة وصنفٌ بلا تكلفة — يُرحَّل كلُّه، ويحمل القيدُ قيمة
    /// المُسعَّر وحده. فلا يُرفض الجرد كلُّه بسبب صنفٍ واحد.
    /// </summary>
    [Fact]
    public async Task AMixedCount_PostsEverything_AndValuesOnlyWhatHasCost()
    {
        var priced = _productIds[0];
        var opening = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 5), StockMovementType.Opening, _warehouseId, "افتتاحيّ");
        opening.AddLine(priced, 20m, 30m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var ledgerBefore = await InventoryLedgerBalanceAsync();

        await CountAsync((priced, 22m), (_productIds[1], 7m), (_productIds[2], 3m));

        var inv = new InventoryService(_db);
        Assert.Equal(22m, await inv.GetOnHandAsync(_companyId, priced, _warehouseId));
        Assert.Equal(7m, await inv.GetOnHandAsync(_companyId, _productIds[1], _warehouseId));
        Assert.Equal(ledgerBefore + 60m, await InventoryLedgerBalanceAsync());   // 2 × 30 فقط
        Assert.Equal(await StockValueAsync(), await InventoryLedgerBalanceAsync());
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
