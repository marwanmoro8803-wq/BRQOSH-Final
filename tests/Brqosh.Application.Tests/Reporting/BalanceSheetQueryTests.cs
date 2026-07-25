using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class BalanceSheetQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public BalanceSheetQueryTests()
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
    public async Task BalanceSheet_IncludesCurrentYearEarnings_AndBalances()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await new BalanceSheetQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 7, 31));

        Assert.Contains(result.Equity, e => e.AccountCode == "3999");
        Assert.True(result.IsBalanced, $"Assets={result.TotalAssets} L+E={result.TotalLiabilitiesAndEquity}");
    }
}
