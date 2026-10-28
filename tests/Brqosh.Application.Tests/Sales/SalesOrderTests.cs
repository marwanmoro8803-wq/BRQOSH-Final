using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

public class SalesOrderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public SalesOrderTests()
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
            .InitializeAsync("شركة أوامر", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    [Fact]
    public async Task Confirm_assigns_order_number()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var order = SalesOrder.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 1));
        order.AddLine("منتج", 2m, 50m);
        await new SalesOrderService(_db).ConfirmAsync(order);

        Assert.StartsWith("SO", order.OrderNumber);
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public async Task CreateInvoiceFromOrder_posts_invoice_and_marks_invoiced()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var order = SalesOrder.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 1));
        order.AddLine("منتج", 1m, 200m);
        await new SalesOrderService(_db).ConfirmAsync(order);

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 5));

        Assert.NotNull(invoice.InvoiceNumber);
        var reloaded = await _db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, reloaded.Status);
        Assert.Equal(invoice.Id, reloaded.SalesInvoiceId);
    }

    [Fact]
    public async Task CreateFromQuotation_copies_lines()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var quote = SalesQuotation.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 1));
        quote.AddLine("بند", 3m, 10m);
        await new SalesQuotationService(_db).CreateAndSendAsync(quote);

        var order = await new SalesOrderService(_db).CreateFromQuotationAsync(_companyId, quote.Id, orderDate: new DateOnly(2026, 6, 10));

        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);
        Assert.Single(order.Lines);
    }
}
