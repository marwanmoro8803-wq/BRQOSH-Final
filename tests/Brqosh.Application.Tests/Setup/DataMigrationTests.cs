using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

public class DataMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public DataMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة ترحيل", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    [Fact]
    public async Task ImportCustomersCsv_adds_new_customers()
    {
        await SetupAsync();
        var csv = """
            عميل أ,311122233300003
            عميل ب,311122233300004
            """;

        var result = await new CsvDataMigrationService(_db).ImportCustomersCsvAsync(_companyId, csv);

        Assert.Equal(2, result.Imported);
        Assert.Equal(2, await _db.Customers.CountAsync(c => c.CompanyId == _companyId));
    }

    [Fact]
    public async Task ImportChartOfAccountsCsv_skips_duplicates()
    {
        await SetupAsync();
        var csv = """
            9999,حساب مخصص,Custom Account,Expense,false
            """;

        var first = await new CsvDataMigrationService(_db).ImportChartOfAccountsCsvAsync(_companyId, csv);
        var second = await new CsvDataMigrationService(_db).ImportChartOfAccountsCsvAsync(_companyId, csv);

        Assert.Equal(1, first.Imported);
        Assert.Equal(0, second.Imported);
        Assert.Equal(1, second.Skipped);
    }
}
