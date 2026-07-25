using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

public class WacCalculationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly long _productId;

    public WacCalculationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);

        var init = new DatabaseInitializer(_db).InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail).GetAwaiter().GetResult();
        _companyId = init.Id;

        var wh = Warehouse.Create(_companyId, "WH-1", "رئيسي", isDefault: true);
        _db.Warehouses.Add(wh);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        _db.SaveChanges();
        _warehouseId = wh.Id;
        _productId = product.Id;
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    [Fact]
    public async Task TwoReceiptLines_SameProduct_ComputesCorrectWac()
    {
        var inv = new InventoryService(_db);
        await inv.PostDocumentMovementAsync(_companyId, _warehouseId, StockMovementType.Receipt,
            new DateOnly(2026, 1, 1), "شراء 1", "TEST-1",
            [(_productId, 10m, 100m), (_productId, 10m, 200m)]);

        await _db.SaveChangesAsync();
        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.Equal(150m, product.WeightedAverageCost);
    }

    [Fact]
    public async Task CancelPurchaseInvoice_ReversesWac()
    {
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var invoice = PurchaseInvoice.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 2, 1), "PI-1", true);
        invoice.AddLine("صنف", 10m, 100m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.Equal(100m, product.WeightedAverageCost);

        await new PurchaseInvoiceCancellationService(_db).CancelAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 2), "admin");

        product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.Equal(0m, product.WeightedAverageCost);
    }
}
