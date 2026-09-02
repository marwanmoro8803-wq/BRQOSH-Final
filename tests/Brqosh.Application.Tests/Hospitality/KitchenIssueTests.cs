using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Hospitality;

/// <summary>
/// (CEEF-230 · ط3-٣) <b>الضيافة — بيعُ طبقٍ يصرف مكوّناتِه.</b>
///
/// <para><b>ما كان:</b> ملفُّ «الضيافة» في شاشة التأسيس <b>ولا قائمةَ طعامٍ ولا وصفة</b>.
/// فالمطعمُ يبيع «برجر»، <b>والخبزُ واللحمُ في المستودع لا ينقصان بريالٍ واحد</b>: تكلفةُ
/// المبيعات <b>صفر</b>، والربحُ في القوائم <b>مئةٌ بالمئة</b>. ثمّ يأتي جردُ آخر السنة
/// بفارقٍ هائلٍ <b>يُقيَّد «عجزَ جرد»</b> — وهو في الحقيقة بيعٌ صحيحٌ لم يُسجَّل له مصروف،
/// <b>وقوائمُ سنةٍ كاملةٍ قُدِّمت للزكاة بربحٍ لا وجودَ له</b>.</para>
///
/// <para><b>وهويّةُ هـ-١١:</b> بعد صرف <c>ن</c> حصّةً، ينقص كلُّ مكوّنٍ بمقدار
/// <c>ن × كمّيتِه في الوصفة</c> — لا أقلّ ولا أكثر.</para>
/// </summary>
public class KitchenIssueTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _breadId;
    private long _meatId;
    private long _cheeseId;

    public KitchenIssueTests()
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
    /// <b>القياسُ الحاكم (هـ-١١): عشرُ حصصٍ تصرف عشرةَ أرغفةٍ وكيلوَ لحمٍ ونصفاً.</b>
    ///
    /// <para>ووصفةٌ لا تُفجَّر تترك المخزونَ كما اشتُري. <b>والفرقُ لا يُرى في يومه</b> —
    /// يظهر بعد سنةٍ رقماً واحداً في الجرد لا يدلّ على سببه.</para>
    /// </summary>
    [Fact]
    public async Task SellingServings_ConsumesTheRecipeExactly()
    {
        await SetupAsync();
        var burger = await MenuAsync("BURG", "برجر", (_breadId, 1m), (_meatId, 0.15m));

        await new KitchenIssueService(_db).IssueForSalesAsync(
            _companyId, new DateOnly(2026, 3, 1), [(burger.Id, 10m)], "POS:1");

        Assert.Equal(90m, await OnHandAsync(_breadId));      // ‏100 − 10
        Assert.Equal(48.5m, await OnHandAsync(_meatId));     // ‏50 − 1.5
        Assert.Equal(60m, await OnHandAsync(_cheeseId));     // ‏لم يُمسّ
    }

    /// <summary>
    /// <b>والمكوّنُ المشترك بين طبقين يُجمع لا يُستبدل.</b>
    ///
    /// <para><b>وهذا عطلُ القاموس الكلاسيكيّ</b>: إسنادٌ بدل جمعٍ يُسقط أحدَ الطبقين
    /// صامتاً. وبرجرٌ وساندويتشُ جبنٍ كلاهما رغيف: <b>عشرةٌ وخمسةٌ يجب أن تُنقص خمسةَ
    /// عشرَ رغيفاً</b>، والإسنادُ يُنقص خمسةً فقط — <b>ولا شيءَ في النظام يُنبّه</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnIngredientSharedByTwoDishes_IsSummedNotOverwritten()
    {
        await SetupAsync();
        var burger = await MenuAsync("BURG", "برجر", (_breadId, 1m), (_meatId, 0.15m));
        var cheeseSandwich = await MenuAsync("SAND", "ساندويتش جبن", (_breadId, 1m), (_cheeseId, 0.05m));

        var result = await new KitchenIssueService(_db).IssueForSalesAsync(
            _companyId, new DateOnly(2026, 3, 1),
            [(burger.Id, 10m), (cheeseSandwich.Id, 5m)], "POS:2");

        Assert.Equal(85m, await OnHandAsync(_breadId));      // ‏100 − 15
        Assert.Equal(48.5m, await OnHandAsync(_meatId));
        Assert.Equal(59.75m, await OnHandAsync(_cheeseId));  // ‏60 − 0.25

        var bread = result.Consumed.Single(c => c.ProductId == _breadId);
        Assert.Equal(15m, bread.Quantity);
    }

    /// <summary>
    /// <b>ووصفةٌ فيها المكوّنُ مرّتين تُجمع عند الإضافة.</b>
    ///
    /// <para>زيتٌ للقليِ وزيتٌ للتتبيل صنفٌ واحد. <b>والاستبدالُ يُسقط أحدَهما</b>،
    /// فيُصرف نصفُ ما استُهلك فعلاً في كلّ طلب.</para>
    /// </summary>
    [Fact]
    public void AComponentAddedTwice_AccumulatesInTheRecipe()
    {
        var item = MenuItem.Create(1L, "X", "طبق", MenuSection.Main, 30m);
        item.AddComponent(7L, 0.2m);
        item.AddComponent(7L, 0.3m);

        Assert.Single(item.Components);
        Assert.Equal(0.5m, item.Components[0].QuantityPerServing);
    }

    /// <summary>
    /// <b>والتكلفةُ تُقيَّد: مدينٌ تكلفةَ المبيعات ودائنٌ المخزون.</b>
    ///
    /// <para><b>والصرفُ بلا قيدٍ يُنقص المخزونَ في الكمّيات ويتركه في الأستاذ</b> — فينفصل
    /// الدفترُ المساعد عن الحساب، وتنكسر <c>هـ-٣</c>.</para>
    /// </summary>
    [Fact]
    public async Task TheIssue_PostsCostOfGoodsSoldAgainstInventory()
    {
        await SetupAsync();
        var burger = await MenuAsync("BURG", "برجر", (_breadId, 1m), (_meatId, 0.15m));

        var result = await new KitchenIssueService(_db).IssueForSalesAsync(
            _companyId, new DateOnly(2026, 3, 1), [(burger.Id, 10m)], "POS:3");

        // ‏عشرةُ أرغفةٍ بريالين = 20 · وكيلو ونصفٌ لحماً بأربعين = 60 ⟵ 80.
        Assert.Equal(80m, result.TotalCost);
        Assert.NotNull(result.JournalEntryId);
        Assert.Equal(80m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));
        Assert.Equal(-80m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
    }

    /// <summary>
    /// <b>وطبقٌ بلا وصفةٍ يُرفض باسمه.</b>
    ///
    /// <para>تفجيرُه يُنتج صفراً، <b>فيمرّ البيعُ بلا صرفٍ ولا تكلفة</b> — وهو عينُ العطل
    /// الذي بُني هذا الباب لإغلاقه. <b>وصمتُه أخطرُ من رفضه</b>: لا شيءَ يقول إنّ الوصفةَ
    /// ناقصة، والفرقُ يظهر بعد سنة.</para>
    /// </summary>
    [Fact]
    public async Task ADishWithNoRecipe_IsRefusedByName()
    {
        await SetupAsync();
        var empty = MenuItem.Create(_companyId, "PLAIN", "طبقٌ بلا وصفة", MenuSection.Main, 25m);
        _db.MenuItems.Add(empty);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new KitchenIssueService(_db).IssueForSalesAsync(
                _companyId, new DateOnly(2026, 3, 1), [(empty.Id, 3m)], "POS:4"));

        Assert.Contains("طبقٌ بلا وصفة", error.Message, StringComparison.Ordinal);
        Assert.Equal(100m, await OnHandAsync(_breadId));
    }

    /// <summary>
    /// <b>ورصيدٌ غيرُ كافٍ يمنع الصرف كلَّه — لا بعضَه.</b>
    ///
    /// <para><b>والمعاملةُ هي الحارس</b>: صرفُ الخبز ثمّ تعثّرُ اللحم يترك المخزونَ ناقصاً
    /// خبزاً بلا تكلفةٍ مقابلة، <b>ويكسر هـ-٣</b>.</para>
    /// </summary>
    [Fact]
    public async Task WhenOneIngredientIsShort_NothingIsConsumed()
    {
        await SetupAsync();
        var feast = await MenuAsync("FEAST", "وليمة", (_breadId, 1m), (_meatId, 10m));

        await Assert.ThrowsAsync<AccountingException>(
            () => new KitchenIssueService(_db).IssueForSalesAsync(
                _companyId, new DateOnly(2026, 3, 1), [(feast.Id, 20m)], "POS:5"));

        Assert.Equal(100m, await OnHandAsync(_breadId));
        Assert.Equal(50m, await OnHandAsync(_meatId));
    }

    /// <summary>
    /// <b>ومكوّنٌ متتبَّعٌ بالدفعة يُرفض باسمه</b> — والقرارُ نفسُه المُعلَن في <c>ط1-٣</c>.
    ///
    /// <para>فالصرفُ بالأقدم انتهاءً لم يُبنَ، <b>واختيارٌ عشوائيٌّ للدفعة يُفقد الأثر</b>:
    /// لا يُعرف عند سحبِ دفعةٍ ما الذي صُرف منها ولا لمَن قُدّم.</para>
    /// </summary>
    [Fact]
    public async Task ATrackedIngredient_IsRefusedByNameNotSwallowed()
    {
        await SetupAsync();
        var meat = await _db.Products.SingleAsync(p => p.Id == _meatId);
        meat.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        await _db.SaveChangesAsync();

        var burger = await MenuAsync("BURG", "برجر", (_breadId, 1m), (_meatId, 0.15m));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new KitchenIssueService(_db).IssueForSalesAsync(
                _companyId, new DateOnly(2026, 3, 1), [(burger.Id, 5m)], "POS:6"));

        Assert.Contains("لحم", error.Message, StringComparison.Ordinal);
        Assert.Equal(100m, await OnHandAsync(_breadId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> OnHandAsync(long productId) =>
        await new InventoryService(_db).GetOnHandAsync(_companyId, productId, _warehouseId);

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<MenuItem> MenuAsync(string code, string name,
        params (long ProductId, decimal Quantity)[] components)
    {
        var item = MenuItem.Create(_companyId, code, name, MenuSection.Main, 30m);
        foreach (var (productId, quantity) in components)
            item.AddComponent(productId, quantity);

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مطعم", "310123456700055", BusinessProfile.Hospitality);
        _companyId = company.Id;

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        _warehouseId = warehouse.Id;

        _breadId = await StockAsync("BREAD", "خبز", quantity: 100m, unitCost: 2m);
        _meatId = await StockAsync("MEAT", "لحم", quantity: 50m, unitCost: 40m);
        _cheeseId = await StockAsync("CHEESE", "جبن", quantity: 60m, unitCost: 25m);
    }

    private async Task<long> StockAsync(string code, string name, decimal quantity, decimal unitCost)
    {
        var product = Product.Create(_companyId, code, name, "حبة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await new InventoryService(_db).PostDocumentMovementAsync(
            _companyId, _warehouseId, StockMovementType.Receipt, new DateOnly(2026, 1, 1),
            "رصيد افتتاحيّ", $"Opening:{code}", [(product.Id, quantity, unitCost, null, (DateOnly?)null)]);
        await _db.SaveChangesAsync();

        return product.Id;
    }
}
