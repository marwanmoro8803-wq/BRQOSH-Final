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
    public async Task Cancelling_ReturnsStockAndReservation_ButNotOnceInvoiced()
    {
        var order = await OrderAsync(6m);
        var inv = new Brqosh.Infrastructure.Inventory.InventoryService(_db);
        var orders = new SalesOrderService(_db);
        var notes = new DeliveryNoteService(_db);

        var first = await notes.DeliverFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 3),
            new Dictionary<int, decimal> { [0] = 2m });
        var cancelled = await notes.CancelAsync(_companyId, first.Id, new DateOnly(2026, 7, 3));
        Assert.Equal(DeliveryNoteStatus.Cancelled, cancelled.Status);
        Assert.Equal(10m, await inv.GetCompanyOnHandAsync(_companyId, _productId));
        Assert.Equal(6m, await orders.ReservedQuantityAsync(_companyId, _productId));
        await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            notes.CancelAsync(_companyId, first.Id, new DateOnly(2026, 7, 3)));

        var second = await notes.DeliverFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 4),
            new Dictionary<int, decimal> { [0] = 6m });
        Assert.Equal(4m, await inv.GetCompanyOnHandAsync(_companyId, _productId));
        await orders.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 5));
        await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            notes.CancelAsync(_companyId, second.Id, new DateOnly(2026, 7, 5)));
    }

    [Fact]
    public async Task BatchTrackedProduct_IsDeliveredFromTheNamedBatch()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة دفعات", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        var product = Product.Create(_companyId, "B001", "دواء", "علبة", ProductType.Stockable, 50m);
        product.SetTracking(InventoryTracking.Batch, 0m);
        _db.AddRange(supplier, customer, product);
        await _db.SaveChangesAsync();

        var purchase = PurchaseInvoice.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 7, 1), "SUP-B", true);
        purchase.AddLine("دواء", 10m, 20m, product.Id, batchNumber: "L1", expiryDate: new DateOnly(2027, 1, 1));
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);
        var order = SalesOrder.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 7, 2));
        order.AddLine("دواء", 3m, 50m, product.Id);
        order = await new SalesOrderService(_db).ConfirmAsync(order);
        var notes = new DeliveryNoteService(_db);

        await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            notes.DeliverFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 3)));   // ‏بلا دفعة
        var note = await notes.DeliverFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 7, 3),
            batchNumbers: new Dictionary<int, string> { [0] = "L1" });
        Assert.Equal(3m, note.Lines.Single().Quantity);
        Assert.Equal(7m, await new Brqosh.Infrastructure.Inventory.InventoryService(_db).GetCompanyOnHandAsync(_companyId, product.Id));

        await notes.CancelAsync(_companyId, note.Id, new DateOnly(2026, 7, 4));
        Assert.Equal(10m, await new Brqosh.Infrastructure.Inventory.InventoryService(_db).GetCompanyOnHandAsync(_companyId, product.Id));
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
