using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;

namespace Brqosh.Application.Tests.Reporting;

public class DashboardQueryTests
{
    [Fact]
    public async Task DashboardQuery_does_not_throw_on_empty_database()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-dash-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var company = await new DatabaseInitializer(db).InitializeAsync("شركة", "300000000000006", BusinessProfile.Retail);

            var kpis = await new DashboardQuery(db).ExecuteAsync(company.Id);

            Assert.Equal(0m, kpis.SalesThisMonth);
            Assert.Equal(0m, kpis.PurchasesThisMonth);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
