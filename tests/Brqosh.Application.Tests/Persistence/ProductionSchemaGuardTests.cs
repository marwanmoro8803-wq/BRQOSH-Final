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
    /// الاختبار السابق كان يؤكد العكس - فيثبّت الثغرة بدل كشفها. المثبّت لا يضبط
    /// المتغيّر واختصارات المستخدم تشغّل exe مباشرة، فكان كل تركيب حقيقيّ يعمل
    /// في وضع التطوير: رمز OTP يُعرض على الشاشة، وSQLite مقبولة، وتجاوز RBAC مفتوح.
    /// </summary>
    [Fact]
    public void Production_IsSecureDefault_WhenNoEnvironmentDeclared()
    {
        var priorEnv = Environment.GetEnvironmentVariable("BRQOSH_ENV");
        var priorTest = Environment.GetEnvironmentVariable("BRQOSH_TEST");
        try
        {
            AppEnvironmentConfig.ForceForTests(null);
            Environment.SetEnvironmentVariable("BRQOSH_ENV", null);
            Environment.SetEnvironmentVariable("BRQOSH_TEST", null);
            AppEnvironmentConfig.ResetForTests();

            Assert.Equal(AppEnvironment.Production, AppEnvironmentConfig.Current);
            Assert.True(AppEnvironmentConfig.IsProduction);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRQOSH_ENV", priorEnv);
            Environment.SetEnvironmentVariable("BRQOSH_TEST", priorTest);
            AppEnvironmentConfig.ResetForTests();
        }
    }

    /// <summary>harness الاختبارات يُصرّح عن نفسه بـBRQOSH_TEST=1 فلا يقع في وضع الإنتاج.</summary>
    [Fact]
    public void Test_IsResolved_WhenOnlyBrqoshTestIsSet()
    {
        var priorEnv = Environment.GetEnvironmentVariable("BRQOSH_ENV");
        var priorTest = Environment.GetEnvironmentVariable("BRQOSH_TEST");
        try
        {
            AppEnvironmentConfig.ForceForTests(null);
            Environment.SetEnvironmentVariable("BRQOSH_ENV", null);
            Environment.SetEnvironmentVariable("BRQOSH_TEST", "1");
            AppEnvironmentConfig.ResetForTests();

            Assert.Equal(AppEnvironment.Test, AppEnvironmentConfig.Current);
            Assert.False(AppEnvironmentConfig.IsProduction);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRQOSH_ENV", priorEnv);
            Environment.SetEnvironmentVariable("BRQOSH_TEST", priorTest);
            AppEnvironmentConfig.ResetForTests();
        }
    }

    /// <summary>التصريح الصريح يغلب كل شيء - المطوّر يستطيع طلب وضع التطوير.</summary>
    [Fact]
    public void ExplicitEnvironmentVariable_Wins()
    {
        var priorEnv = Environment.GetEnvironmentVariable("BRQOSH_ENV");
        try
        {
            AppEnvironmentConfig.ForceForTests(null);
            Environment.SetEnvironmentVariable("BRQOSH_ENV", "development");
            AppEnvironmentConfig.ResetForTests();
            Assert.Equal(AppEnvironment.Development, AppEnvironmentConfig.Current);

            Environment.SetEnvironmentVariable("BRQOSH_ENV", "production");
            AppEnvironmentConfig.ResetForTests();
            Assert.Equal(AppEnvironment.Production, AppEnvironmentConfig.Current);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRQOSH_ENV", priorEnv);
            AppEnvironmentConfig.ResetForTests();
        }
    }
}

[CollectionDefinition("SequentialEnv", DisableParallelization = true)]
public class SequentialEnvCollection;
