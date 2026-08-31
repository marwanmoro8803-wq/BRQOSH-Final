using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م1-١٢ / A-5 — الشقّ الثاني) <b>ترحيلان متزامنان لورقة جردٍ واحدة.</b>
///
/// <para>حارسُ اللقطة البائتة في <c>StockCountAdjustmentService</c> يمنع الصورةَ المتباعدة
/// في الزمن: يقرأ الرصيدَ داخل المعاملة ويرفض إن خالف اللقطة. <b>وهو لا يمنع التزامن
/// بذاته</b> — معاملتان تقرآن ١٠٠ قبل أن تكتب إحداهما، فتمرّان معاً على الفحص.</para>
///
/// <para><b>فهل يقع السباق؟</b> سؤالٌ لا يُجاب بالقراءة. ولا يظهر على SQLite أصلاً: كتبةٌ
/// متزامنون على اتصالٍ في الذاكرة يتسلسلون بحكم المحرّك — <b>درسُ Z-4</b>. فيُقاس على
/// PostgreSQL حصراً، بمِرفاس ح-٢ وببوّابة انطلاقٍ تُجبر التداخل.</para>
///
/// <para><b>وما قِيس فعلاً</b> (أربعةُ كتبةٍ ببوّابة انطلاق):
/// <c>نجح 1 وأخفق 3 — AccountingException×3: تغيّر رصيدُ «صنف الجرد» ... كان 100 وصار 95</c>.
/// أيْ أنّ <b>حارسَ اللقطة نفسَه</b> هو ما أمسك الثلاثة: قرؤوا الرصيدَ داخل معاملاتهم
/// <b>بعد</b> أن كتب الأوّل، فرأوا ٩٥ لا ١٠٠.</para>
///
/// <para><b>ويبقى تداخلٌ أضيق لم يقع في هذا التشغيل</b>: أن يقرأ اثنان ١٠٠ قبل أن يكتب
/// أحدهما، فيمرّا معاً على الفحص. وهناك يقع الحملُ على <b>رمز التزامن على رصيد المخزون</b>
/// (مقيسٌ في <c>MigrationProbe</c>: ستّةُ كتبةٍ ⟵ نجح واحدٌ وكُشف خمسةُ تعارضٍ، والرصيدُ
/// مطابقٌ تماماً). <b>فالطبقتان تحفظان الرقمَ نفسَه</b>.</para>
///
/// <para><b>ولهذا يقيس هذا الاختبارُ الرصيدَ لا آليّةَ الحماية</b> — فأيُّهما فَرَض،
/// المحفوظُ واحد. وهو يسقط إن سقطت الطبقتان معاً، ولا يكذب إن تبدّلت أيُّهما.</para>
/// </summary>
public class StockCountConcurrencyTests
{
    private const int Writers = 4;

    /// <summary>
    /// <b>القياس المركزيّ.</b> أربعةُ ترحيلاتٍ متزامنة لورقةٍ واحدة (لقطة ١٠٠، معدود ٩٥) ⟵
    /// <b>الرصيدُ ٩٥ مهما كان عددُ من نجح</b>. فحسمُ العجز مرّتين هو العطل، لا رفضُ الثاني.
    ///
    /// <para><b>والقياسُ على الرصيد لا على عدد النجاحات</b> عمداً: هو الرقمُ الذي يراه المالك،
    /// وهو ما يبقى صادقاً أو يكذب. وأيُّ طريقٍ يحفظه — قفلٌ أو رمزُ تزامن أو الحارسُ نفسُه —
    /// مقبولٌ ما دام محفوظاً.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentPostsOfOneSheet_CannotDeductTheShortageTwice()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("stockcount");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("متجر الجرد", "310123456700088", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var product = Product.Create(
            company.Id, "P-1", "صنف الجرد", "قطعة", ProductType.Stockable, 100m);
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();

        var warehouseId = scope.Db.Warehouses.First(w => w.CompanyId == company.Id).Id;

        var opening = StockMovement.CreateDraft(
            company.Id, new DateOnly(2026, 1, 1), StockMovementType.AdjustmentIn,
            warehouseId, "رصيد افتتاحيّ");
        opening.AddLine(product.Id, 100m, 10m);
        await new StockMovementPostingService(scope.Db).PostAsync(opening);

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            return await new StockCountAdjustmentService(db).PostVariancesAsync(
                company.Id, warehouseId, new DateOnly(2026, 3, 1),
                [(product.Id, 100m, 95m, null)], $"amin{i}");
        });

        await using var reader = scope.OpenIndependentContext();
        var onHand = await new InventoryService(reader)
            .GetOnHandAsync(company.Id, product.Id, warehouseId);

        Assert.True(onHand == 95m,
            $"الرصيد {onHand} بعد أربعة ترحيلاتٍ متزامنة لورقةٍ واحدة — والصواب 95. "
            + $"فالعجزُ حُسم أكثرَ من مرّة. {outcome.Describe()}");

        Assert.True(outcome.Successes.Count >= 1,
            $"سقط كلُّ الترحيلات — فالجردُ متعذّرٌ عند التزاحم لا محميّ. {outcome.Describe()}");
    }
}
