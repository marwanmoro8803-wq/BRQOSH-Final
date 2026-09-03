using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-231 · ط5-٨ب · O-35) <b>توزيعُ تكاليف الشراء عند التزاحم — المسارُ الذي يمسّ
/// التكلفةَ ولا يمسّ الرصيد.</b>
///
/// <para><b>ما كان:</b> أُضيف رمزا تزامنٍ إلى <c>Product</c> و<c>InventoryCostLayer</c>
/// في <c>ط5-٨</c>، <b>ونجت طفرتاهما كلتاهما</b>: نُزع كلٌّ على حدة فمرّ
/// <c>CostConcurrencyTests</c>. والسببُ مقيسٌ لا مُخمَّن — <b>كلُّ حركةٍ مخزنيةٍ تمسّ
/// <c>InventoryBalance</c>، وله رمزٌ منذ <c>ع-D1</c></b>، فيتسلسل المتزاحمون عليه قبل أن
/// يبلغوا التكلفة. <b>فكُتب الرمزان «دفاعاً في العمق مُعلَناً لا إغلاقاً مُثبَتاً»</b>،
/// وفُتح <c>O-35</c> باسم المسار الذي لا يمرّ على الرصيد.</para>
///
/// <para><b>وهذا هو ذاك المسار:</b> <c>LandedCostAllocationService</c> يقرأ تكلفةَ الطبقة
/// (أو متوسّطَ الصنف) ثمّ يكتبها، <b>ولا يُحرّك كمّيةً واحدة</b> — فلا صفَّ رصيدٍ يتسلسل
/// عليه المتزاحمون، ولا <c>ConsumeFifoAsync</c> يُمسك ما بقي برفض «طبقات غير كافية».</para>
///
/// <para><b>وأثرُ العطل ماليٌّ لا تقنيّ:</b> كلُّ توزيعٍ ناجحٍ <b>يُرحّل قيداً مديناً حسابَ
/// المخزون بكامل المبلغ</b>. فلو ضاع امتصاصُ أحدهم بين قراءةٍ وكتابة <b>بقي قيدُه في
/// الأستاذ</b> — فيعلو حسابُ المخزون على قيمة المخزون فرقاً دائماً، <b>ويُقفل العامُ على
/// أصلٍ لا وجودَ له</b>.</para>
///
/// <para><b>وهويّةُ هـ-١ هي القياس</b>: رصيدُ حساب المخزون في الأستاذ = قيمةُ المخزون في
/// الدفتر المساعد. <b>ويُقاس الفرقان لا القيمتان</b> — فما قبل التزاحم لا يعني هذا
/// الاختبار.</para>
///
/// <para><b>ولا يظهر على SQLite أصلاً</b> — كتبةٌ متزامنون على اتصالٍ في الذاكرة يتسلسلون
/// بحكم المحرّك (درسُ Z-4). فيُقاس على PostgreSQL حصراً.</para>
/// </summary>
public class LandedCostConcurrencyTests
{
    private const int Writers = 4;
    private const decimal Allocation = 60m;
    private const decimal OpeningQuantity = 100m;
    private const decimal OpeningUnitCost = 10m;

    private static readonly DateOnly ReceiptDate = new(2026, 1, 1);
    private static readonly DateOnly CostDate = new(2026, 1, 5);

    /// <summary>
    /// <b>القياسُ الحاكم — طبقاتُ FIFO: الفرقُ في الأستاذ = الفرقُ في قيمة الطبقات.</b>
    ///
    /// <para><c>AbsorbLandedCost</c> يقرأ <c>UnitCost</c> ويكتبه. <b>فموزّعان متزامنان
    /// يقرآن العشرةَ نفسَها ويكتبان ١٠٫٦ كلاهما</b> — فتُمتَصّ ستّون مرّةً واحدة
    /// <b>ويُرحَّل قيدان بمئةٍ وعشرين</b>.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentLandedCost_DoesNotLoseAbsorbedCost_OnFifoLayers()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("landedcostfifo");

        var (companyId, productId, warehouseId) = await SeedAsync(scope, InventoryCostingMethod.Fifo);

        var glBefore = await InventoryLedgerBalanceAsync(scope, companyId);
        var valueBefore = await FifoValueAsync(scope, companyId, productId);

        var outcome = await AllocateConcurrentlyAsync(scope, companyId, productId, warehouseId);

        var glAfter = await InventoryLedgerBalanceAsync(scope, companyId);
        var valueAfter = await FifoValueAsync(scope, companyId, productId);

        AssertIdentity(outcome, glAfter - glBefore, valueAfter - valueBefore);
    }

    /// <summary>
    /// <b>ومتوسّطُ التكلفة كذلك — وهو المسارُ الافتراضيّ لأكثر المنشآت.</b>
    ///
    /// <para><c>SetWeightedAverageCost(WeightedAverageCost + addPerUnit)</c> قراءةٌ ثمّ
    /// كتابةٌ على <c>Product</c> بعينه. <b>وهذا هو الصفُّ الذي وُضع عليه الرمزُ في ط5-٨
    /// ونجت طفرتُه</b> — فيُقاس هنا حيث يعضّ.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentLandedCost_DoesNotLoseAbsorbedCost_OnWeightedAverage()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("landedcostwac");

        var (companyId, productId, warehouseId) = await SeedAsync(scope, InventoryCostingMethod.WeightedAverage);

        var glBefore = await InventoryLedgerBalanceAsync(scope, companyId);
        var valueBefore = await WacValueAsync(scope, companyId, productId);

        var outcome = await AllocateConcurrentlyAsync(scope, companyId, productId, warehouseId);

        var glAfter = await InventoryLedgerBalanceAsync(scope, companyId);
        var valueAfter = await WacValueAsync(scope, companyId, productId);

        AssertIdentity(outcome, glAfter - glBefore, valueAfter - valueBefore);
    }

    // ── القياس ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>هـ-١ على الفرقين — ورفضُ المتزاحم سلوكٌ سليم، والابتلاعُ الصامت هو العطل.</b>
    /// </summary>
    /// <remarks>
    /// <b>ولا يُقاس على عدد النجاحات</b>: المهمُّ أنّ ما نجح ظهر أثرُه كاملاً في الطرفين
    /// معاً، لا أن ينجح الكلّ. <b>وسقوطُ الكلِّ يُرفض صراحةً</b> — فخدمةٌ تتعذّر على كلّ
    /// متزاحمٍ ليست محميّةً بل معطّلة.
    /// </remarks>
    private static void AssertIdentity(
        ParallelWriters.Outcome<decimal> outcome, decimal ledgerDelta, decimal valueDelta)
    {
        Assert.True(outcome.Successes.Count >= 1,
            $"سقط كلُّ التوزيعات — فالتكلفةُ متعذّرةٌ عند التزاحم لا محميّة. {outcome.Describe()}");

        var expected = outcome.Successes.Sum();

        Assert.True(Math.Abs(ledgerDelta - expected) < 0.01m,
            $"رحّل الأستاذُ {ledgerDelta:N2} وما نجح {expected:N2} — "
            + $"فقيدٌ رُحّل لتوزيعٍ ارتدّ، أو توزيعٌ نجح بلا قيد. {outcome.Describe()}");

        // ‏هـ-١: الدفترُ المساعد = حسابُ المراقبة. **وفرقٌ هنا امتصاصٌ ضاع بين قراءةٍ وكتابة.**
        Assert.True(Math.Abs(valueDelta - ledgerDelta) < 0.01m,
            $"علا حسابُ المخزون {ledgerDelta:N2} وقيمةُ المخزون {valueDelta:N2} — "
            + $"ففرقُ {ledgerDelta - valueDelta:N2} امتصاصُ تكلفةٍ ضاع، وقيدُه باقٍ في الأستاذ "
            + $"فيُقفل العامُ على أصلٍ لا وجودَ له. {outcome.Describe()}");
    }

    private static Task<ParallelWriters.Outcome<decimal>> AllocateConcurrentlyAsync(
        PostgresTestDatabase.PostgresScope scope, long companyId, long productId, long warehouseId) =>
        ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            await new LandedCostAllocationService(db).AllocateAsync(
                companyId, CostDate, $"شحن {i}", Allocation,
                [(productId, warehouseId, 1m)], "admin");

            return Allocation;
        });

    // ── تهيئة وقراءة ────────────────────────────────────────────────────────

    private static async Task<(long CompanyId, long ProductId, long WarehouseId)> SeedAsync(
        PostgresTestDatabase.PostgresScope scope, InventoryCostingMethod method)
    {
        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("متجر التكلفة الإضافية", "310123456700133", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        (await scope.Db.Companies.SingleAsync(c => c.Id == company.Id)).SetCostingMethod(method);
        await scope.Db.SaveChangesAsync();

        var product = Product.Create(
            company.Id, "P-LC", "صنف التكلفة الإضافية", "قطعة", ProductType.Stockable, 100m);
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();

        var warehouseId = scope.Db.Warehouses.First(w => w.CompanyId == company.Id).Id;

        // ‏**والترحيلُ عبر خدمته لا عبر `PostDocumentMovementAsync`**: الثانيةُ تُرقّم
        // ‏خارج معاملة، **والقفلُ الاستشاريّ يُحرَّر فوراً فلا يحمي شيئاً** — وحارسُ
        // ‏`DocumentSequenceLock` يرفضها على PostgreSQL بالنصّ.
        var opening = StockMovement.CreateDraft(
            company.Id, ReceiptDate, StockMovementType.AdjustmentIn, warehouseId, "رصيد افتتاحيّ");
        opening.AddLine(product.Id, OpeningQuantity, OpeningUnitCost);

        await new StockMovementPostingService(scope.Db).PostAsync(opening);

        return (company.Id, product.Id, warehouseId);
    }

    /// <summary>رصيدُ حساب المخزون في الأستاذ — <b>حسابُ المراقبة</b>.</summary>
    private static async Task<decimal> InventoryLedgerBalanceAsync(PostgresTestDatabase.PostgresScope scope, long companyId)
    {
        await using var db = scope.OpenIndependentContext();

        var inventoryId = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.Inventory)
            .Select(a => a.Id)
            .SingleAsync();

        var lines = await db.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == inventoryId)
            .ToListAsync();

        return lines.Sum(l => l.Debit - l.Credit);
    }

    /// <summary>قيمةُ الطبقات — <b>الدفترُ المساعد على مسار FIFO</b>.</summary>
    private static async Task<decimal> FifoValueAsync(PostgresTestDatabase.PostgresScope scope, long companyId, long productId)
    {
        await using var db = scope.OpenIndependentContext();

        var layers = await db.InventoryCostLayers.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.ProductId == productId)
            .ToListAsync();

        return Math.Round(layers.Sum(l => l.RemainingQuantity * l.UnitCost), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>قيمةُ المخزون بالمتوسّط — <b>الرصيدُ × المتوسّط</b>.</summary>
    private static async Task<decimal> WacValueAsync(PostgresTestDatabase.PostgresScope scope, long companyId, long productId)
    {
        await using var db = scope.OpenIndependentContext();

        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        var onHand = await new InventoryService(db).GetCompanyOnHandAsync(companyId, productId);

        return Math.Round(onHand * product.WeightedAverageCost, 2, MidpointRounding.AwayFromZero);
    }
}
