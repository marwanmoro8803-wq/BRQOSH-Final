using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229 · م2-٢) <b>إقلاعٌ حقيقيّ على PostgreSQL — على قاعدةٍ تُتلَف، لا على دفاتر صاحبه.</b>
///
/// <para><b>ما كان هنا:</b> اختبارٌ يفتح <c>%APPDATA%/BRQOSH/database.json</c> — أي
/// <b>قاعدة المستخدم الحقيقية</b> — ويُنشئ فيها شركةً باسم «marwan» ومستخدماً باسم
/// <c>pgadmin</c>. واختبارٌ يكتب في دفاتر صاحبه عطلٌ مهما كان ما يؤكّده.</para>
///
/// <para><b>وثلاثةُ أعطالٍ أخرى في اثني عشر سطراً:</b> (١) يبدأ بـ <c>if (!File.Exists) return;</c>
/// فيُحسب <b>ناجحاً</b> على كلّ جهازٍ بلا إعداد — وهو حال CI كلّه؛ (٢) كلُّ فشلٍ مخطّطيّ
/// يبتلعه <c>IsIgnorableSchemaConflict</c>؛ (٣) وما يؤكّده في النهاية <c>CanConnectAsync</c>
/// و<c>CountAsync</c> — أي أنّ القاعدة تستجيب، لا أنّ الإقلاع صحيح.</para>
///
/// <para><b>وما صار:</b> قاعدةٌ باسمٍ مستقلّ تُنشأ وتُسقط (<see cref="PostgresTestDatabase"/>)،
/// وتخطٍّ <b>معلَن</b> لا صامت (<see cref="PostgresFactAttribute"/>)، وتأكيدٌ على أنّ الإقلاع
/// أنتج <b>دليلَ حساباتٍ ومستودعاً وشركةً</b> — لا على أنّ الشبكة تعمل.</para>
/// </summary>
public class PostgreSqlStartupTests
{
    /// <summary>
    /// <b>القياس المركزيّ.</b> إقلاعٌ من الصفر على PostgreSQL يُنتج منشأةً صالحةً للعمل:
    /// شركةٌ، ودليلُ حساباتٍ سعوديّ، ومستودعٌ افتراضيّ. وهذا ما يراه المالك أوّل يوم.
    /// </summary>
    [PostgresFact]
    public async Task AFreshPostgreSqlDatabase_BootsIntoAWorkingEstablishment()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("startup");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة الإقلاع", "310123456700099", BusinessProfile.Retail);

        Assert.True(company.Id > 0, "الشركة لم تُنشأ — والمعرّف صفر يعني أنّ عمود identity لا يعمل.");

        // ‏دليلُ الحسابات هو ما يجعل الترحيل ممكناً أصلاً — وغيابُه إقلاعٌ ناجحٌ بلا نظام.
        var accounts = await scope.Db.Accounts.CountAsync(a => a.CompanyId == company.Id);
        Assert.True(accounts > 0, "لا دليلَ حسابات — الإقلاع نجح شكلاً ولا يُمكن ترحيلُ قيدٍ واحد.");

        // ‏والمستودع الافتراضيّ شرطُ أيّ حركة مخزون في منشأةٍ بمستودعٍ واحد.
        Assert.True(
            await scope.Db.Warehouses.AnyAsync(w => w.CompanyId == company.Id),
            "لا مستودعَ افتراضيّ — وأوّلُ شراءٍ سيسقط.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> الإقلاع مرّتين على القاعدة نفسها لا يُنشئ منشأةً ثانية ولا يرمي —
    /// وهذا هو ما يحدث فعلاً عند كلّ تشغيلٍ للبرنامج، لا عند التركيب فقط.
    /// </summary>
    [PostgresFact]
    public async Task BootingTwice_IsIdempotent()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("startup_twice");

        var first = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة الإقلاع", "310123456700099", BusinessProfile.Retail);
        var second = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة الإقلاع", "310123456700099", BusinessProfile.Retail);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await scope.Db.Companies.CountAsync());
    }

    /// <summary>
    /// <b>حارسٌ ضدّ عودة العطل — ويقيس الآلية لا نصَّ الملفّ.</b>
    ///
    /// <para>الذي يمنع اختباراً من فتح قاعدة المستخدم <c>brqosh</c> ليس انضباطَ الكاتب بل
    /// البادئةُ <c>brqosh_t_</c> التي يفرضها <see cref="PostgresTestDatabase.DatabaseNameFor"/>.
    /// فيُستدعى بأسوأ المدخلات — بما فيها اسمُ قاعدة المستخدم نفسه — ويُتحقَّق أنّ الناتج
    /// لا يساويها أبداً. وحارسٌ يقرأ نصَّ الملفّ يُثبت وجودَ كلمةٍ لا صحّةَ سلوك.</para>
    /// </summary>
    [Theory]
    [InlineData("brqosh")]
    [InlineData("postgres")]
    [InlineData("BRQOSH")]
    [InlineData("brqosh; DROP DATABASE brqosh --")]
    [InlineData("startup")]
    public void NoTestDatabaseName_CanEverBeTheUsersOwn(string requested)
    {
        var actual = PostgresTestDatabase.DatabaseNameFor(requested);

        Assert.StartsWith("brqosh_t_", actual, StringComparison.Ordinal);
        Assert.NotEqual("brqosh", actual);
        Assert.NotEqual("postgres", actual);
        Assert.All(actual, c => Assert.True(char.IsLetterOrDigit(c) || c == '_', $"محرفٌ غير آمن: {c}"));
    }
}
