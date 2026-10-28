using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>‏فوترةُ أمر البيع على دفعات: المتبقّي لكلّ سطر، ولا تجاوز، والإلغاءُ يُعيد الكمّية.</summary>
public class PartialInvoicingOfASalesOrderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public PartialInvoicingOfASalesOrderTests()
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

    private async Task<SalesOrder> OrderAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة أوامر", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var order = SalesOrder.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 1));
        order.AddLine("خدمة أ", 10m, 100m);
        order.AddLine("خدمة ب", 4m, 50m);
        return await new SalesOrderService(_db).ConfirmAsync(order);
    }

    [Fact]
    public async Task TwoPartialInvoices_CompleteTheOrder()
    {
        var order = await OrderAsync();
        var svc = new SalesOrderService(_db);

        var first = await svc.CreatePartialInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 5),
            new Dictionary<int, decimal> { [0] = 4m });
        Assert.Single(first.Lines);
        Assert.Equal(4m, first.Lines[0].Quantity);
        Assert.Equal(SalesOrderStatus.PartInvoiced, (await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);

        var rest = await svc.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 6));
        Assert.Equal(6m, rest.Lines.Single(l => l.Description == "خدمة أ").Quantity);
        Assert.Equal(4m, rest.Lines.Single(l => l.Description == "خدمة ب").Quantity);
        var stored = await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, stored.Status);
        Assert.Equal(rest.Id, stored.SalesInvoiceId);
    }

    [Fact]
    public async Task MoreThanTheRemainder_IsRefused()
    {
        var order = await OrderAsync();
        var svc = new SalesOrderService(_db);
        await svc.CreatePartialInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 5),
            new Dictionary<int, decimal> { [1] = 3m });

        await Assert.ThrowsAsync<AccountingException>(() => svc.CreatePartialInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 6, 6), new Dictionary<int, decimal> { [1] = 2m }));
    }

    [Fact]
    public async Task CancellingAPartialInvoice_ReturnsItsQuantity()
    {
        var order = await OrderAsync();
        var svc = new SalesOrderService(_db);
        var first = await svc.CreatePartialInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 5),
            new Dictionary<int, decimal> { [0] = 10m, [1] = 4m });
        Assert.Equal(SalesOrderStatus.Invoiced, (await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);

        await new SalesInvoiceCancellationService(_db).CancelAsync(_companyId, first.Id, new DateOnly(2026, 6, 7), "admin");

        var stored = await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Confirmed, stored.Status);
        var again = await svc.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 8));
        Assert.Equal(10m, again.Lines.Single(l => l.Description == "خدمة أ").Quantity);
    }
}
