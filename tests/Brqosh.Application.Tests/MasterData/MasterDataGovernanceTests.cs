using Brqosh.Application.MasterData;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.MasterData;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.MasterData;

public class MasterDataGovernanceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public MasterDataGovernanceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة MDM", "310123456700013", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    private MasterDataGovernanceService Svc() => new(_db);

    [Fact]
    public async Task CheckDuplicateVat_DetectsCustomerAndSupplier()
    {
        await SetupAsync();

        _db.Customers.Add(Customer.Create(_companyId, "عميل أ", "310123456700099"));
        _db.Suppliers.Add(Supplier.Create(_companyId, "مورد ب", "310123456700088"));
        await _db.SaveChangesAsync();

        Assert.True(await Svc().CheckDuplicateVatAsync(
            _companyId, "310123456700099", null, MasterDataEntityKind.Customer));
        Assert.True(await Svc().CheckDuplicateVatAsync(
            _companyId, "310123456700088", null, MasterDataEntityKind.Supplier));
        Assert.False(await Svc().CheckDuplicateVatAsync(
            _companyId, "310123456700099", null, MasterDataEntityKind.Supplier));
    }

    [Fact]
    public async Task FindSimilarNames_ReturnsMatchesAboveThreshold()
    {
        await SetupAsync();

        _db.Customers.Add(Customer.Create(_companyId, "شركة الرياض التجارية"));
        _db.Suppliers.Add(Supplier.Create(_companyId, "مؤسسة الرياض"));
        await _db.SaveChangesAsync();

        var matches = await Svc().FindSimilarNamesAsync(_companyId, "شركة الرياض", 0.5);
        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.EntityKind == MasterDataEntityKind.Customer);
    }

    [Fact]
    public void Update_SetsModifiedAuditFields()
    {
        var customer = Customer.Create(1, "عميل", "310123456700001");
        customer.Update("عميل محدّث", "310123456700001", "0500000000", "الرياض", "auditor");

        Assert.Equal("auditor", customer.ModifiedBy);
        Assert.NotNull(customer.ModifiedAtUtc);
    }
}
