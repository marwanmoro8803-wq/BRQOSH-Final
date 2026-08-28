using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

public class SalesInvoiceCancellationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _productId;

    public SalesInvoiceCancellationTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task SetupWithSaleAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P001", "لابتوب", "قطعة", ProductType.Stockable, 1000m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _productId = product.Id;

        var purchase = PurchaseInvoice.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 7, 1), "SUP-1", true);
        purchase.AddLine("لابتوب", 10m, 1000m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 2));
        sale.AddLine("لابتوب", 2m, 1500m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(sale);
    }

    [Fact]
    public async Task CancelInvoice_ReversesJournal_AndReturnsStock()
    {
        await SetupWithSaleAsync();

        var invoice = await _db.SalesInvoices.SingleAsync();
        var inv = new InventoryService(_db);
        var wh = await inv.GetDefaultWarehouseAsync(_companyId);
        var onHandBefore = await inv.GetOnHandAsync(_companyId, _productId, wh.Id);
        Assert.Equal(8m, onHandBefore);

        await new SalesInvoiceCancellationService(_db).CancelAsync(
            _companyId, invoice.Id, new DateOnly(2026, 7, 3), "admin");

        var cancelled = await _db.SalesInvoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(SalesInvoiceStatus.Cancelled, cancelled.Status);

        var originalEntry = await _db.JournalEntries.SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Reversed, originalEntry.Status);

        var onHandAfter = await inv.GetOnHandAsync(_companyId, _productId, wh.Id);
        Assert.Equal(10m, onHandAfter);
    }

    [Fact]
    public async Task CancelInvoice_RejectedWhenPostedCreditNoteExists()
    {
        await SetupWithSaleAsync();

        var invoice = await _db.SalesInvoices.SingleAsync();
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 3), "مرتجع جزئي");
        note.AddLine("لابتوب", 1m, 1500m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new SalesInvoiceCancellationService(_db).CancelAsync(
                _companyId, invoice.Id, new DateOnly(2026, 7, 4), "admin"));
        Assert.Contains("إشعار دائن", ex.Message);
    }
}
