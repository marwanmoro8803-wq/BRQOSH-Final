using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-231 · ط5-٨) <b>تكلفةُ الصنف عند التزاحم — وهو صنفُ عطلِ <c>ع-D1</c> في موضعٍ آخر.</b>
///
/// <para><b>ما كان:</b> أُغلق <c>ع-D1</c> برمز تزامنٍ على <b>رصيد</b> المخزون
/// (<c>InventoryBalance</c>) — <b>وبقيت التكلفةُ بلا رمز</b>. و<c>WeightedAverageCost</c>
/// يُقرأ ثمّ يُكتب في خمسة مسارات ترحيل، <b>وطبقاتُ FIFO تُستهلَك بقراءةٍ ثمّ كتابة</b>،
/// <b>ودفعاتُ المخزون كذلك</b>.</para>
///
/// <para><b>وأثرُه:</b> ترحيلان متزامنان يقرآن المتوسّطَ نفسَه ويكتبان قيمتين، والثاني
/// يمحو الأوّل — <b>فتُقيَّد تكلفةُ مبيعاتٍ بمتوسّطٍ لم يشهد شراءً وقع فعلاً</b>، وينفصل
/// الدفترُ المساعد عن الأستاذ فرقاً دائماً <b>لا يُكتشف إلا بجردٍ بعد شهور</b>.</para>
///
/// <para><b>ولا يظهر على SQLite أصلاً</b>: كتبةٌ متزامنون على اتصالٍ في الذاكرة يتسلسلون
/// بحكم المحرّك — <b>درسُ Z-4</b>. فيُقاس على PostgreSQL حصراً.</para>
///
/// <para><b>وهويّةُ هـ-٢٣:</b> بعد <c>ن</c> استلاماً متزامناً لكمّياتٍ وتكاليفَ معلومة،
/// <b>قيمةُ المخزون = مجموعُ ما دخل</b> — لا أقلّ. <b>وقياسٌ على عدد النجاحات لا يقيس
/// شيئاً</b>: المهمُّ أنّ ما نجح يظهر أثرُه كاملاً في القيمة، لا أن ينجح الكلّ.</para>
///
/// <para><b>وما قِيس بالطفرة — ويُقال كما هو:</b> نُزع رمزُ التزامن عن <c>Product</c>
/// وحدَه فمرّ الاختبار، ونُزع عن <c>InventoryCostLayer</c> وحدَه فمرّ كذلك.
/// <b>فالرمزان لا يعضّان في هذا السيناريو</b> — والسببُ معروفٌ ومقيس: <b>كلُّ حركةٍ
/// مخزنيةٍ تمسّ <c>InventoryBalance</c> كذلك، وله رمزٌ منذ <c>ع-D1</c></b>، فيتسلسل
/// المتزاحمون عليه قبل أن يبلغوا التكلفة؛ ويُمسك <c>ConsumeFifoAsync</c> ما بقي
/// برفض «طبقات FIFO غير كافية».</para>
///
/// <para><b>فهذان الرمزان دفاعٌ في العمق مُعلَنٌ لا إغلاقٌ مُثبَت.</b> وموضعُ نفعهما
/// مسارٌ يمسّ التكلفةَ <b>ولا يمسّ الرصيد</b> — كتوزيع تكاليف الشراء
/// (<c>LandedCostAllocationService</c>) الذي يُعدّل المتوسّطَ وتكلفةَ الطبقات بلا
/// حركةِ كمّية. <b>وذاك المسارُ لم يُقَس بعد، ويُكتب بنداً مفتوحاً لا يُدَّعى إغلاقُه.</b>
/// وهذا الملفُّ يقيس <b>الهويّتين</b> — لا أيَّ طبقةٍ فرضتهما، وهو اصطلاحُ
/// <c>StockCountConcurrencyTests</c> نفسِه.</para>
/// </summary>
public class CostConcurrencyTests
{
    private const int Writers = 6;

    /// <summary>
    /// <b>القياسُ الحاكم: قيمةُ المخزون بعد استلاماتٍ متزامنة = مجموعُ ما دخل فعلاً.</b>
    ///
    /// <para><b>والتكاليفُ مختلفةٌ عمداً</b>: استلاماتٌ بالتكلفة نفسِها <b>تُنتج المتوسّطَ
    /// نفسَه أياً كان الترتيب</b> — فيمرّ الاختبارُ ولو ضاع تحديثٌ كامل. ولا يُفرّق إلا
    /// حين تختلف.</para>
    ///
    /// <para><b>والقياسُ على القيمة لا على المتوسّط</b>: المتوسّطُ رقمٌ مشتقّ، <b>والقيمة
    /// هي ما يظهر في الميزانية</b> وما يُقابَل به حسابُ المخزون في الأستاذ.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentReceipts_DoNotLoseCost()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("costconcurrency");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("متجر التكلفة", "310123456700111", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var product = Product.Create(
            company.Id, "P-1", "صنف التكلفة", "قطعة", ProductType.Stockable, 100m);
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();

        var warehouseId = scope.Db.Warehouses.First(w => w.CompanyId == company.Id).Id;

        // ‏عشرُ حبّاتٍ بتكلفةٍ تختلف لكلّ كاتب — فمجموعُ ما دخل يفترق بترتيبٍ ضائع.
        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            var movement = StockMovement.CreateDraft(
                company.Id, new DateOnly(2026, 1, 1), StockMovementType.AdjustmentIn,
                warehouseId, $"استلام {i}");
            movement.AddLine(product.Id, 10m, 10m + i);

            await new StockMovementPostingService(db).PostAsync(movement);
            return 10m * (10m + i);
        });

        await using var reader = scope.OpenIndependentContext();

        var stored = await reader.Products.SingleAsync(p => p.Id == product.Id);
        var onHand = await new InventoryService(reader)
            .GetOnHandAsync(company.Id, product.Id, warehouseId);

        var value = Math.Round(onHand * stored.WeightedAverageCost, 2, MidpointRounding.AwayFromZero);
        var expected = Math.Round(outcome.Successes.Sum(), 2, MidpointRounding.AwayFromZero);

        Assert.True(outcome.Successes.Count >= 1,
            $"سقط كلُّ الاستلامات — فالتكلفةُ متعذّرةٌ عند التزاحم لا محميّة. {outcome.Describe()}");

        // ‏هـ-٢٣: قيمةُ المخزون = مجموعُ ما دخل. **وفرقٌ هنا تحديثٌ ضاع.**
        Assert.True(Math.Abs(value - expected) < 1m,
            $"قيمةُ المخزون {value:N2} وما دخل فعلاً {expected:N2} — "
            + $"ففرقُ {value - expected:N2} تحديثُ تكلفةٍ ضاع بين قراءةٍ وكتابة. {outcome.Describe()}");
    }

    /// <summary>
    /// <b>والطبقةُ الواحدة لا يُستهلَك منها أكثرُ ممّا فيها.</b>
    ///
    /// <para><c>InventoryCostLayer.Consume</c> يقرأ <c>RemainingQuantity</c> ثمّ يكتبها.
    /// <b>فصرفان متزامنان من الطبقة نفسِها يمرّان كلاهما على فحص «المتبقي يكفي»</b> —
    /// ويُستهلك أكثرُ ممّا فيها، <b>فتخرج بضاعةٌ بتكلفةٍ لا وجودَ لها</b>.</para>
    ///
    /// <para><b>وهويّةُ هـ-٢٤:</b> <c>RemainingQuantity</c> لا يكون سالباً، ومجموعُ ما
    /// استُهلك من طبقةٍ لا يتجاوز أصلَها.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentIssues_CannotOverconsumeOneLayer()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("layerconcurrency");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("متجر الطبقات", "310123456700122", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        (await scope.Db.Companies.SingleAsync(c => c.Id == company.Id))
            .SetCostingMethod(InventoryCostingMethod.Fifo);
        await scope.Db.SaveChangesAsync();

        var product = Product.Create(
            company.Id, "P-1", "صنف الطبقات", "قطعة", ProductType.Stockable, 100m);
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();

        var warehouseId = scope.Db.Warehouses.First(w => w.CompanyId == company.Id).Id;

        var opening = StockMovement.CreateDraft(
            company.Id, new DateOnly(2026, 1, 1), StockMovementType.AdjustmentIn,
            warehouseId, "رصيد افتتاحيّ");
        opening.AddLine(product.Id, 100m, 10m);
        await new StockMovementPostingService(scope.Db).PostAsync(opening);

        // ‏ستُّ صرفياتٍ بعشرين — والمتاحُ مئة. **فأربعُ نجاحاتٍ على الأكثر.**
        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            var consumed = await new InventoryCostingService(db).ConsumeForIssueAsync(
                company.Id, product.Id, warehouseId, 20m, $"Probe:{i}");

            // ‏**والحفظُ جزءٌ من الكتابة لا زينة**: الاستهلاكُ يُبدّل كياناتٍ متعقَّبة،
            // ‏**وقياسٌ بلا حفظٍ يقرأ ما قبل العمل** — ولا يمرّ برمز التزامن أصلاً.
            await db.SaveChangesAsync();
            return consumed;
        });

        await using var reader = scope.OpenIndependentContext();

        var remaining = await reader.InventoryCostLayers
            .Where(l => l.CompanyId == company.Id && l.ProductId == product.Id)
            .SumAsync(l => l.RemainingQuantity);

        // ‏هـ-٢٤: ما بقي = مئةٌ ناقصَ ما نجح × عشرين، ولا يكون سالباً.
        var expected = 100m - (outcome.Successes.Count * 20m);

        Assert.True(remaining >= 0m,
            $"طبقةٌ بمتبقٍّ سالب ({remaining:N4}) — فاستُهلك أكثرُ ممّا فيها. {outcome.Describe()}");

        Assert.True(remaining == expected,
            $"المتبقي {remaining:N4} والمنتظَر {expected:N4} — "
            + $"فاستهلاكٌ ضاع أو تكرّر بين قراءةٍ وكتابة. {outcome.Describe()}");
    }
}
