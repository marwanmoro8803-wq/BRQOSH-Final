using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

[Collection("SequentialEnv")]
public class ProductionSchemaGuardTests : IDisposable
{
    public ProductionSchemaGuardTests()
    {
        AppEnvironmentConfig.ResetForTests();
    }

    public void Dispose()
    {
        AppEnvironmentConfig.ResetForTests();
    }

    [Fact]
    public async Task Production_RejectsEnsureCreatedOnEmptyRelationalWithoutMigrations()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        Assert.True(AppEnvironmentConfig.IsProduction);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new BrqoshDbContext(
            new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(connection).Options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DatabaseSchemaService(db).EnsureSchemaAsync());
        Assert.Contains("إنتاج", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// (CEEF-228) الافتراض الآمن: غياب BRQOSH_ENV = إنتاج، لا تطوير.
    ///
    /// الاختبار السابق كان يؤكد العكس - فيثبّت الثغرة بدل كشفها. المثبِّت لا يضبط
    /// المتغيّر واختصارات المستخدم تشغّل exe مباشرة، فكان كل تركيب حقيقيّ يعمل
    /// في وضع التطوير: رمز OTP يُعرض على الشاشة، وSQLite مقبولة، وتجاوز RBAC مفتوح.
    ///
    /// **ويُقاس على الدالّة النقيّة `Decide`** (٢٠٢٦-١٠-٢٧): الاختبارُ كان يقرأ الجهازَ
    /// فيسقط على جهازٍ منصَّبٍ عليه المنتج — `BRQOSH_ENV=production` في نطاق الجهاز مصدرٌ
    /// رسميٌّ لا يخفضه متغيّرُ العمليّة — فيقيس الجهازَ لا السياسة. والدالّةُ نقيّة: كلُّ
    /// مدخلاتها معطاةٌ هنا، فتُقاس القاعدةُ نفسُها في كلّ بيئة.
    /// </summary>
    [Fact]
    public void Production_IsSecureDefault_WhenNoEnvironmentDeclared() =>
        Assert.Equal(AppEnvironment.Production, AppEnvironmentConfig.Decide(null, null, null));

    /// <summary>harness الاختبارات يُصرّح عن نفسه بـBRQOSH_TEST=1 فلا يقع في وضع الإنتاج.</summary>
    [Fact]
    public void Test_IsResolved_WhenOnlyBrqoshTestIsSet() =>
        Assert.Equal(AppEnvironment.Test, AppEnvironmentConfig.Decide(null, null, "1"));

    /// <summary>التصريح الصريح يغلب الافتراض — والمصدرُ الرسميُّ يغلب التصريح.</summary>
    [Fact]
    public void ExplicitEnvironmentVariable_Wins()
    {
        Assert.Equal(AppEnvironment.Development, AppEnvironmentConfig.Decide(null, "development", null));
        Assert.Equal(AppEnvironment.Production, AppEnvironmentConfig.Decide(null, "production", null));

        // والمصدرُ الرسميُّ (نطاقُ الجهاز) يتقدّم على إعلان العمليّة:
        Assert.Equal(
            AppEnvironment.Production,
            AppEnvironmentConfig.Decide(AppEnvironment.Production, "development", null));
    }
}

[CollectionDefinition("SequentialEnv", DisableParallelization = true)]
public class SequentialEnvCollection;
