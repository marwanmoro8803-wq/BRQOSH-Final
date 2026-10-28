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

/// <summary>‏حجزُ المخزون عند تأكيد أمر البيع، والإفراجُ بالفوترة والإلغاء، والفاتورةُ المسودة.</summary>
public class SalesOrderReservationAndDraftInvoiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId, _customerId, _productId;

    public SalesOrderReservationAndDraftInvoiceTests()
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

    private async Task<SalesOrder> OrderAsync(decimal qty)
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة حجز", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P001", "لابتوب", "قطعة", ProductType.Stockable, 1500m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _productId = product.Id;

        var purchase = PurchaseInvoice.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 7, 1), "SUP-1", true);
        purchase.AddLine("لابتوب", 10m, 1000m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var order = SalesOrder.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 2));
        order.AddLine("لابتوب", qty, 1500m, _productId);
        return await new SalesOrderService(_db).ConfirmAsync(order);
    }

    [Fact]
    public async Task Confirm_Reserves_AndInvoicing_Releases()
    {
        var order = await OrderAsync(6m);
        var svc = new SalesOrderService(_db);
        Assert.Equal(6m, await svc.ReservedQuantityAsync(_companyId, _productId));
        Assert.Equal(4m, await svc.AvailableToSellAsync(_companyId, _productId));

        await svc.CreatePartialInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 3),
            new Dictionary<int, decimal> { [0] = 2m });
        Assert.Equal(4m, await svc.ReservedQuantityAsync(_companyId, _productId));
    }

    [Fact]
    public async Task CancellingTheOrder_ReleasesItsReservation()
    {
        var order = await OrderAsync(3m);
        var svc = new SalesOrderService(_db);
        await svc.CancelAsync(_companyId, order.Id);
        Assert.Equal(0m, await svc.ReservedQuantityAsync(_companyId, _productId));
    }

    [Fact]
    public async Task DraftInvoice_IsSavedUnposted_AndCountsAsInvoiced()
    {
        var order = await OrderAsync(2m);
        var invoice = await new SalesOrderService(_db).CreatePartialInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 7, 3), null, postNow: false);

        var stored = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(SalesInvoiceStatus.Draft, stored.Status);
        Assert.Null(stored.JournalEntryId);
        Assert.Equal(SalesOrderStatus.Invoiced, (await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);
    }
}
