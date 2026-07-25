using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

public class SubcontractRetentionWithholdTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public SubcontractRetentionWithholdTests()
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

    [Fact]
    public async Task Withhold_posts_ap_debit_and_retention_credit()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مقاولات", "300000000000005", BusinessProfile.Construction);

        var customer = Customer.Create(company.Id, "عميل", "300000000000006");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(company.Id, "مقاول باطن", "300000000000007");
        _db.Suppliers.Add(supplier);

        var project = ConstructionProject.Create(
            company.Id, "PRJ-SUB", "مشروع باطن", customer.Id, contractValue: 500_000m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);

        var agreement = SubcontractAgreement.Create(
            company.Id, project.Id, supplier.Id, "SUB-01", new DateOnly(2026, 7, 1), 200_000m, 10m);
        agreement.Activate();
        _db.SubcontractAgreements.Add(agreement);
        await _db.SaveChangesAsync();

        var entry = await new SubcontractRetentionWithholdService(_db).WithholdAsync(
            company.Id, agreement.Id, 20_000m, new DateOnly(2026, 7, 10), "محتجز أول", "admin");

        Assert.True(entry.IsBalanced);
        Assert.Equal(20_000m, entry.Lines.Single(l =>
            l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable).Debit);
        Assert.Equal(20_000m, entry.Lines.Single(l =>
            l.AccountCode == SaudiChartOfAccounts.SystemCodes.RetentionPayableSubcontractors).Credit);

        var summary = await new RetentionSummaryQuery(_db).ListAsync(company.Id);
        var row = summary.Single(r => r.ProjectId == project.Id);
        Assert.Equal(20_000m, row.SubcontractRetentionHeld);
        Assert.Equal(20_000m, row.SubcontractRetentionAvailable);
    }

    [Fact]
    public async Task SubcontractRetentionRelease_RestoresSupplierNetWithhold()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مقاولات", "300000000000005", BusinessProfile.Construction);

        var customer = Customer.Create(company.Id, "عميل", "300000000000006");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(company.Id, "مقاول باطن", "300000000000007");
        _db.Suppliers.Add(supplier);

        var project = ConstructionProject.Create(
            company.Id, "PRJ-SUB", "مشروع باطن", customer.Id, contractValue: 500_000m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);

        var agreement = SubcontractAgreement.Create(
            company.Id, project.Id, supplier.Id, "SUB-01", new DateOnly(2026, 7, 1), 200_000m, 10m);
        agreement.Activate();
        _db.SubcontractAgreements.Add(agreement);
        await _db.SaveChangesAsync();

        await new SubcontractRetentionWithholdService(_db).WithholdAsync(
            company.Id, agreement.Id, 50_000m, new DateOnly(2026, 7, 10), "محتجز", "admin");

        var beforeRelease = await new Brqosh.Infrastructure.Purchasing.SupplierBalanceQuery(_db)
            .ForSupplierAsync(company.Id, supplier.Id);
        Assert.Equal(-50_000m, beforeRelease.Balance);

        await new RetentionReleaseService(_db).ReleaseAsync(
            company.Id, project.Id, RetentionReleaseType.SubcontractRelease,
            20_000m, new DateOnly(2026, 7, 15), "إفراج جزئي", "admin");

        var afterRelease = await new Brqosh.Infrastructure.Purchasing.SupplierBalanceQuery(_db)
            .ForSupplierAsync(company.Id, supplier.Id);
        Assert.Equal(-30_000m, afterRelease.Balance);

        var netWithheld = await new SubcontractSupplierRetentionQuery(_db)
            .WithheldForSupplierAsync(company.Id, supplier.Id);
        Assert.Equal(30_000m, netWithheld);
    }
}
