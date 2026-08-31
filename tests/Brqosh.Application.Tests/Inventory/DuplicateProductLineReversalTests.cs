using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م0-٥ / I-NEW-2) <b>صنفٌ واحد على سطرين — والعكسُ يجب أن يعكس مجموعَهما لا
/// كلَّ سطرٍ كاملاً.</b>
///
/// <para><b>ما قالته الخطة:</b> «لا <c>GroupBy(ProductId)</c> قبل حلقة العكس — سطران لصنفٍ
/// واحد يعكسان كلٌّ الكميةَ كاملة». <b>وكانت مفتوحةً بلا اختبارٍ واحد يمرّ بهذا المسار</b>،
/// وكلُّ اختبارات الإلغاء تستعمل صنفاً واحداً في سطرٍ واحد.</para>
///
/// <para><b>وهذا القياسُ يسأل السؤالَ مباشرةً</b>: مستندٌ بسطرين لصنفٍ واحد (٣ ثمّ ٤)،
/// ثمّ إلغاء. فالرصيدُ يعود إلى ما كان، <b>والهوية هـ-٣ تبقى صادقة</b> — أو ينكشف العطل.</para>
///
/// <para><b>ولا يُقاس بالرصيد وحده:</b> عكسٌ ناقصٌ يُبقي بضاعةً في الدفتر المساعد، وعكسٌ
/// زائدٌ يُخرج ما لم يدخل — وكلاهما يكسر <c>رصيدُ 1107 == Σ الكمية × المتوسط</c>. فالهوية
/// تمسك الاتّجاهين معاً، والرصيدُ وحده يمسك أحدهما.</para>
/// </summary>
public class DuplicateProductLineReversalTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly long _productId;

    public DuplicateProductLineReversalTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر التكرار", "310123456700066", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();

        var product = Product.Create(
            _companyId, "P-1", "صنف مكرَّر", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        _db.SaveChanges();
        _productId = product.Id;

        _warehouseId = _db.Warehouses.First(w => w.CompanyId == _companyId).Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> رصيدٌ ٢٠، ثمّ حركةٌ واردة بسطرين لصنفٍ واحد (٣ + ٤ = ٧)،
    /// ثمّ إلغاؤها ⟵ الرصيدُ يعود ٢٠ والهوية هـ-٣ صادقة.
    /// </summary>
    [Fact]
    public async Task CancellingAnInboundWithTwoLinesOfOneProduct_ReversesTheirSumExactly()
    {
        await PostAsync(StockMovementType.AdjustmentIn, "افتتاحيّ", (20m, 10m));
        Assert.Equal(20m, await OnHandAsync());

        var duplicated = await PostAsync(StockMovementType.AdjustmentIn, "وارد بسطرين",
            (3m, 10m), (4m, 10m));
        Assert.Equal(27m, await OnHandAsync());

        await CancelAsync(duplicated);

        Assert.True(20m == await OnHandAsync(),
            $"الرصيد {await OnHandAsync()} بعد إلغاء حركةٍ بسطرين مجموعُهما 7 — والصواب 20. "
            + "فالعكسُ حسب كلَّ سطرٍ بالكميّة الكاملة أو أسقط أحدهما.");

        await AssertInventoryIdentityAsync();
    }

    /// <summary>
    /// <b>والصادرُ كذلك</b> — وهو الاتّجاه الذي يمسّ تكلفةَ البضاعة المباعة، فخطؤه يُضخّم
    /// مجملَ الربح أو يبخسه.
    /// </summary>
    [Fact]
    public async Task CancellingAnOutboundWithTwoLinesOfOneProduct_ReversesTheirSumExactly()
    {
        await PostAsync(StockMovementType.AdjustmentIn, "افتتاحيّ", (20m, 10m));

        var duplicated = await PostAsync(StockMovementType.AdjustmentOut, "صادر بسطرين",
            (3m, 10m), (4m, 10m));
        Assert.Equal(13m, await OnHandAsync());

        await CancelAsync(duplicated);

        Assert.True(20m == await OnHandAsync(),
            $"الرصيد {await OnHandAsync()} بعد إلغاء صرفٍ بسطرين مجموعُهما 7 — والصواب 20.");

        await AssertInventoryIdentityAsync();
    }

    /// <summary>
    /// <b>وثلاثةُ سطورٍ بتكاليفَ مختلفة</b> — فالمتوسطُ المرجّح هو ما ينكشف هنا: عكسٌ يحسب
    /// سطراً واحداً بتكلفةٍ خاطئة يُبقي المتوسطَ مسموماً وإن عاد الرصيدُ صحيحاً.
    /// </summary>
    [Fact]
    public async Task ThreeLinesAtDifferentCosts_LeaveTheAverageClean()
    {
        await PostAsync(StockMovementType.AdjustmentIn, "افتتاحيّ", (10m, 10m));

        var mixed = await PostAsync(StockMovementType.AdjustmentIn, "وارد بثلاثة",
            (2m, 12m), (3m, 15m), (5m, 20m));
        Assert.Equal(20m, await OnHandAsync());

        await CancelAsync(mixed);

        Assert.Equal(10m, await OnHandAsync());

        var product = await _db.Products.AsNoTracking().SingleAsync(p => p.Id == _productId);
        Assert.True(product.WeightedAverageCost == 10m,
            $"المتوسط {product.WeightedAverageCost} بعد إلغاء وارِدٍ بثلاثة سطور — والصواب 10. "
            + $"فبقيت تكلفةُ حركةٍ أُلغيت مسمومةً في الصنف. {await IdentityStateAsync()}");

        await AssertInventoryIdentityAsync();
    }

    /// <summary>
    /// <b>شاهدُ التحكّم — وهو ما يفصل التشخيص.</b> السطرُ الواحد بتكلفةٍ مختلفة: إن سُمّم
    /// المتوسطُ هنا أيضاً فالعطلُ في <b>عكس التكلفة</b> لا في <b>تعدّد السطور</b>، وبطل
    /// وصفُ الخطة (م0-٥) وصار البندُ غيرَه.
    /// </summary>
    [Fact]
    public async Task ASingleLineAtADifferentCost_LeavesTheAverageClean()
    {
        await PostAsync(StockMovementType.AdjustmentIn, "افتتاحيّ", (10m, 10m));

        var single = await PostAsync(StockMovementType.AdjustmentIn, "وارد بسطر واحد", (10m, 20m));
        Assert.Equal(20m, await OnHandAsync());

        await CancelAsync(single);

        var product = await _db.Products.AsNoTracking().SingleAsync(p => p.Id == _productId);
        Assert.True(product.WeightedAverageCost == 10m,
            $"المتوسط {product.WeightedAverageCost} بعد إلغاء وارِدٍ بسطرٍ **واحد** — والصواب 10. "
            + $"فالعطلُ في عكس التكلفة لا في تعدّد السطور. {await IdentityStateAsync()}");

        await AssertInventoryIdentityAsync();
    }

    private async Task<string> IdentityStateAsync()
    {
        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var trial = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var ledger = trial
            .Where(l => l.AccountCode.StartsWith("1107", StringComparison.Ordinal))
            .Sum(l => l.TotalDebit - l.TotalCredit);
        return $"[مساعد {rows.Sum(r => r.TotalValue):0.00} · أستاذ {ledger:0.00}]";
    }

    private async Task<StockMovement> PostAsync(
        StockMovementType type, string description, params (decimal Qty, decimal Cost)[] lines)
    {
        var movement = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 1), type, _warehouseId, description);
        foreach (var (qty, cost) in lines)
            movement.AddLine(_productId, qty, cost);

        await new StockMovementPostingService(_db).PostAsync(movement);
        return movement;
    }

    private Task CancelAsync(StockMovement movement) =>
        new StockMovementCancellationService(_db).CancelAsync(
            _companyId, movement.Id, new DateOnly(2026, 2, 1), "admin");

    private Task<decimal> OnHandAsync() =>
        new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId);

    /// <summary>هـ-٣: رصيدُ حساب المخزون في الأستاذ == Σ (الكمية × المتوسط) في الدفتر المساعد.</summary>
    private async Task AssertInventoryIdentityAsync()
    {
        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var subsidiary = rows.Sum(r => r.TotalValue);

        var trial = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var ledger = trial
            .Where(l => l.AccountCode.StartsWith("1107", StringComparison.Ordinal))
            .Sum(l => l.TotalDebit - l.TotalCredit);

        Assert.True(Math.Abs(subsidiary - ledger) < 0.01m,
            $"هـ-٣ مكسورة: الدفتر المساعد {subsidiary:0.00} والأستاذ {ledger:0.00} — "
            + $"الفرق {subsidiary - ledger:0.00} ريالاً.");
    }
}
