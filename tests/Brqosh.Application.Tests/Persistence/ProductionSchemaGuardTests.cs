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

    [Fact]
    public void Development_IsDefaultWhenEnvUnset()
    {
        AppEnvironmentConfig.ForceForTests(null);
        Environment.SetEnvironmentVariable("BRQOSH_ENV", null);
        AppEnvironmentConfig.ResetForTests();
        Assert.Equal(AppEnvironment.Development, AppEnvironmentConfig.Current);
        Assert.False(AppEnvironmentConfig.IsProduction);
    }
}

[CollectionDefinition("SequentialEnv", DisableParallelization = true)]
public class SequentialEnvCollection;
