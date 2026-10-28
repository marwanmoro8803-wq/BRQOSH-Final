using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>‏خصمُ السطر وخصمُ المستند وعملةُ أمر البيع تنتقل إلى فواتيره — والجزئيّةُ تأخذ نصيبَها والمُكمِلةُ الباقي.</summary>
public class SalesOrderDiscountAndCurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public SalesOrderDiscountAndCurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task LineAndDocumentDiscount_AndCurrency_FlowToTheInvoices()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة خصم", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var order = SalesOrder.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 2));
        order.AddLine("خدمة", 10m, 100m, discountPercent: 10m);   // ‏صافي 900
        order.ApplyDocumentDiscount(100m);
        order.SetCurrency("USD", 3.75m);
        Assert.Equal(800m, order.Subtotal);
        order = await new SalesOrderService(_db).ConfirmAsync(order);

        var reloaded = await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(("USD", 3.75m, 100m), (reloaded.CurrencyCode, reloaded.ExchangeRate, reloaded.DocumentDiscountAmount));
        Assert.Equal(10m, reloaded.Lines.Single().DiscountPercent);

        var orders = new SalesOrderService(_db);
        var first = await orders.CreatePartialInvoiceFromOrderAsync(company.Id, order.Id, new DateOnly(2026, 7, 3),
            new Dictionary<int, decimal> { [0] = 3m });
        Assert.Equal(("USD", 3.75m), (first.CurrencyCode, first.ExchangeRate));
        Assert.Equal(10m, first.Lines.Single().DiscountPercent);
        Assert.Equal(30m, first.DocumentDiscountAmount);

        var rest = await orders.CreateInvoiceFromOrderAsync(company.Id, order.Id, new DateOnly(2026, 7, 4));
        Assert.Equal(70m, rest.DocumentDiscountAmount);
    }
}
