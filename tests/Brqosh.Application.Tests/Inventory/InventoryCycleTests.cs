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

namespace Brqosh.Application.Tests.Inventory;

public class InventoryCycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _customerId;
    private long _productId;

    public InventoryCycleTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P001", "لابتوب", "قطعة", ProductType.Stockable, 1500m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _customerId = customer.Id;
        _productId = product.Id;
    }

    [Fact]
    public async Task PurchaseThenSale_UpdatesStockAndCogs()
    {
        await SetupAsync();

        var purchase = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 1), "SUP-1", true);
        purchase.AddLine("لابتوب", 10m, 1000m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var inv = new InventoryService(_db);
        var wh = await inv.GetDefaultWarehouseAsync(_companyId);
        var onHand = await inv.GetOnHandAsync(_companyId, _productId, wh.Id);
        Assert.Equal(10m, onHand);

        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.Equal(1000m, product.WeightedAverageCost);

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 2));
        sale.AddLine("لابتوب", 3m, 1500m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        onHand = await inv.GetOnHandAsync(_companyId, _productId, wh.Id);
        Assert.Equal(7m, onHand);

        var entry = await _db.JournalEntries.SingleAsync(e => e.SourceDocument!.StartsWith("SalesInvoice:"));
        var cogsLine = entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold);
        Assert.Equal(3000m, cogsLine.Debit);
    }

    [Fact]
    public async Task Sale_InsufficientStock_IsRejected()
    {
        await SetupAsync();

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 2));
        sale.AddLine("لابتوب", 1m, 1500m, _productId);

        await Assert.ThrowsAsync<AccountingException>(() => new SalesInvoicePostingService(_db).PostAsync(sale));
    }

    [Fact]
    public async Task PurchaseInvoice_Stockable_PostsToInventoryAccount()
    {
        await SetupAsync();

        var purchase = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 1), "SUP-2", true);
        purchase.AddLine("لابتوب", 2m, 500m, _productId);
        var posted = await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(1000m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.Inventory).Debit);
    }

    [Fact]
    public async Task CreditNote_CogsReversal_UsesOriginalSaleUnitCost_NotCurrentWac()
    {
        await SetupAsync();

        var firstPurchase = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 1), "SUP-A", true);
        firstPurchase.AddLine("لابتوب", 10m, 1000m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(firstPurchase);

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 2));
        sale.AddLine("لابتوب", 3m, 1500m, _productId);
        var postedSale = await new SalesInvoicePostingService(_db).PostAsync(sale);

        var secondPurchase = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 3), "SUP-B", true);
        secondPurchase.AddLine("لابتوب", 10m, 2000m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(secondPurchase);

        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.NotEqual(1000m, product.WeightedAverageCost);

        var note = CreditNote.CreateDraft(_companyId, _customerId, postedSale.Id, new DateOnly(2026, 7, 4), "مرتجع");
        note.AddLine("مرتجع", 1m, 1500m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var entry = await _db.JournalEntries
            .Where(e => e.SourceDocument!.StartsWith("CreditNote:"))
            .SingleAsync();
        var cogsLine = entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold);
        Assert.Equal(1000m, cogsLine.Credit);
    }
}
