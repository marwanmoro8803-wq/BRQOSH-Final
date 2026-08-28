using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

public class StockTransferTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _wh1;
    private readonly long _wh2;
    private readonly long _productId;

    public StockTransferTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000001", BusinessProfile.Manufacturing).GetAwaiter().GetResult();
        _companyId = company.Id;

        var whA = Warehouse.Create(_companyId, "WH-A", "مستودع أ", isDefault: true);
        var whB = Warehouse.Create(_companyId, "WH-B", "مستودع ب");
        _db.Warehouses.AddRange(whA, whB);

        var product = Product.Create(_companyId, "TR-P", "صنف تحويل", "PCS", ProductType.Stockable);
        _db.Products.Add(product);
        _db.SaveChanges();

        _wh1 = whA.Id;
        _wh2 = whB.Id;
        _productId = product.Id;
    }

    [Fact]
    public async Task Transfer_via_posting_service_persists_movement_and_balances()
    {
        var inv = new InventoryService(_db);
        var date = new DateOnly(2026, 3, 1);

        await inv.PostDocumentMovementAsync(_companyId, _wh1, StockMovementType.Receipt, date,
            "رصيد", "OPEN", [(_productId, 100m, 10m)]);
        await _db.SaveChangesAsync();

        var movement = StockMovement.CreateDraft(_companyId, date, StockMovementType.Transfer,
            _wh1, "تحويل", _wh2);
        movement.AddLine(_productId, 40m, 10m);

        await new StockMovementPostingService(_db).PostAsync(movement);

        Assert.Equal(StockMovementStatus.Posted, movement.Status);
        Assert.NotNull(movement.BranchId);
        Assert.True(await _db.StockMovements.AnyAsync(m => m.Type == StockMovementType.Transfer));

        var onHandSource = await inv.GetOnHandAsync(_companyId, _productId, _wh1);
        var onHandDest = await inv.GetOnHandAsync(_companyId, _productId, _wh2);
        Assert.Equal(60m, onHandSource);
        Assert.Equal(40m, onHandDest);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
