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

/// <summary>‏إذنُ التسليم يصرف المخزونَ ويقيّد التكلفة، والفاتورةُ اللاحقةُ لا تصرف ثانية.</summary>
public class DeliveryNoteFromSalesOrderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId, _customerId, _productId;

    public DeliveryNoteFromSalesOrderTests()
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
    public async Task Delivery_IssuesStock_AndTheInvoiceDoesNotIssueAgain()
    {
        var order = await OrderAsync(6m);
        var inv = new Brqosh.Infrastructure.Inventory.InventoryService(_db);
        var orders = new SalesOrderService(_db);

        var note = await new DeliveryNoteService(_db).DeliverFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 3),
            new Dictionary<int, decimal> { [0] = 4m });
        Assert.NotNull(note.JournalEntryId);
        Assert.Equal(6m, await inv.GetCompanyOnHandAsync(_companyId, _productId));
        Assert.Equal(2m, await orders.ReservedQuantityAsync(_companyId, _productId));

        var invoice = await orders.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 4));
        Assert.True(invoice.StockIssuedByDelivery);
        Assert.Equal(4m, invoice.Lines.Single().Quantity);
        Assert.Equal(6m, await inv.GetCompanyOnHandAsync(_companyId, _productId));   // ‏لا صرفَ ثانٍ
        var entry = await _db.JournalEntries.AsNoTracking().Include(e => e.Lines).SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == "1107");

        var rest = await orders.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 5));
        Assert.False(rest.StockIssuedByDelivery);
        Assert.Equal(2m, rest.Lines.Single().Quantity);
        Assert.Equal(4m, await inv.GetCompanyOnHandAsync(_companyId, _productId));
        Assert.Equal(SalesOrderStatus.Invoiced, (await _db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);
    }

    [Fact]
    public async Task DeliveringMoreThanIsOpen_IsRefused()
    {
        var order = await OrderAsync(3m);
        await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            new DeliveryNoteService(_db).DeliverFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 3),
                new Dictionary<int, decimal> { [0] = 4m }));
    }
}
