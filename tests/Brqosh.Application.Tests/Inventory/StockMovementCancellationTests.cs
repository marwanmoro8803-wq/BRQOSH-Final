using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

public class StockMovementCancellationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _productId;

    public StockMovementCancellationTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;

        var wh = Warehouse.Create(_companyId, "WH-1", "رئيسي", isDefault: true);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Warehouses.Add(wh);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _warehouseId = wh.Id;
        _productId = product.Id;
    }

    [Fact]
    public async Task CancelAdjustmentIn_ReversesJournalAndQuantity()
    {
        await SetupAsync();

        var movement = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 7, 1),
            StockMovementType.AdjustmentIn, _warehouseId, "زيادة جرد");
        movement.AddLine(_productId, 10m, 5m);
        await new StockMovementPostingService(_db).PostAsync(movement);

        var onHandBefore = await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId);
        Assert.Equal(10m, onHandBefore);

        await new StockMovementCancellationService(_db).CancelAsync(
            _companyId, movement.Id, new DateOnly(2026, 7, 2), "admin");

        var cancelled = await _db.StockMovements.SingleAsync(m => m.Id == movement.Id);
        Assert.Equal(StockMovementStatus.Cancelled, cancelled.Status);

        var onHandAfter = await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId);
        Assert.Equal(0m, onHandAfter);

        var reversal = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status == JournalEntryStatus.Posted)
            .OrderByDescending(e => e.Id)
            .FirstAsync();
        Assert.True(reversal.IsBalanced);
    }
}
