using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

public class StartupSimulationTests
{
    [Fact]
    public async Task Full_startup_path_with_schema_updater_on_file_db()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-probe-{Guid.NewGuid():N}.db");
        try
        {
            await using (var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}"))
            {
                await new DatabaseBootstrapService(db).InitializeAsync(
                    "شركة", "300000000000001", BusinessProfile.Retail,
                    adminUsername: "opsadmin", adminPassword: TestUserFactory.DefaultPassword);

                var user = await new UserAuthService(db).AuthenticateAsync(
                    db.Companies.Select(c => c.Id).First(), "opsadmin", TestUserFactory.DefaultPassword);
                Assert.NotNull(user);
            }
        }
        finally
        {
            ReleaseSqliteFile(path);
        }
    }

    [Fact]
    public async Task Schema_updater_on_existing_legacy_db_simulation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await using (var db1 = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}"))
            {
                await new DatabaseInitializer(db1).InitializeAsync("شركة", "300000000000001", BusinessProfile.General);
                await new BrqoshSchemaUpdater(db1).ApplyAsync();
            }

            await using (var db2 = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}"))
            {
                await new DatabaseBootstrapService(db2).InitializeAsync(
                    "شركة", "300000000000001", BusinessProfile.General,
                    adminUsername: "legacyadmin", adminPassword: TestUserFactory.DefaultPassword);

                Assert.True(await db2.AppUsers.AnyAsync());
                Assert.False(await db2.BillsOfMaterials.AnyAsync());
            }
        }
        finally
        {
            ReleaseSqliteFile(path);
        }
    }

    private static void ReleaseSqliteFile(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path))
            File.Delete(path);
    }
}
