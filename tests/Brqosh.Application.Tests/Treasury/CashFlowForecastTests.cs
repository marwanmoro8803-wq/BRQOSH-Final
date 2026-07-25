using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class CashFlowForecastTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CashFlowForecastTests()
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
    public async Task Forecast_BuildsThirteenWeeks_FromOpenBalances()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة CF", "310123456700014", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل CF");
        var supplier = Supplier.Create(_companyId, "مورد CF");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 1));
        invoice.AddLine("بيع", 10m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 6, 1), "INV-EXT-01", true);
        purchase.AddLine("شراء", 5m, 200m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var forecast = await new CashFlowForecastService(_db)
            .ForecastAsync(_companyId, new DateOnly(2026, 7, 1));

        Assert.Equal(13, forecast.Weeks.Count);
        Assert.True(forecast.TotalProjectedInflows > 0m);
        Assert.True(forecast.TotalProjectedOutflows > 0m);
    }
}
