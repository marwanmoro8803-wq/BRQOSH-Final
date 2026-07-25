using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

public class TransactionAtomicityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public TransactionAtomicityTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000001", BusinessProfile.Retail).GetAwaiter().GetResult().Id;
    }

    [Fact]
    public async Task Sales_invoice_post_succeeds_atomically()
    {
        var customer = Customer.Create(_companyId, "عميل", "300000000000002");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P1", "صنف", "PCS", ProductType.Stockable, salePrice: 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await new InventoryService(_db).PostDocumentMovementAsync(_companyId,
            (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id,
            StockMovementType.Opening, new DateOnly(2026, 1, 1), "رصيد", "OPEN",
            [(product.Id, 100m, 10m)]);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 1, 15));
        invoice.AddLine("بيع", 2m, 50m, product.Id);

        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(SalesInvoiceStatus.Posted, invoice.Status);
        Assert.True(await _db.JournalEntries.AnyAsync());
        Assert.True(await _db.StockMovements.CountAsync() >= 2);
    }

    [Fact]
    public async Task Insufficient_stock_rolls_back_invoice_and_journal()
    {
        var customer = Customer.Create(_companyId, "عميل", "300000000000003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P2", "صنف2", "PCS", ProductType.Stockable, salePrice: 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 1, 20));
        invoice.AddLine("بيع", 5m, 50m, product.Id);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new SalesInvoicePostingService(_db).PostAsync(invoice));

        Assert.Equal(SalesInvoiceStatus.Draft, invoice.Status);
        Assert.False(await _db.SalesInvoices.AnyAsync());
        Assert.False(await _db.JournalEntries.AnyAsync());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
