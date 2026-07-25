using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

public class WorkOrderCancellationTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public WorkOrderCancellationTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task Cancel_Completed_WorkOrder_Reverses_Stock_And_Journals()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "مصنع", "300000000000099", BusinessProfile.Manufacturing);

        var finished = Product.Create(company.Id, "FG-X", "منتج", "قطعة", ProductType.Stockable, 0m);
        var raw = Product.Create(company.Id, "RM-X", "خامة", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, raw);
        await _db.SaveChangesAsync();

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(company.Id);
        var opening = StockMovement.CreateDraft(company.Id, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouse.Id, "افتتاح خامة");
        opening.AddLine(raw.Id, 100m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(company.Id, "BOM-X", "تجميع", finished.Id);
        bom.AddLine(raw.Id, 2m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(company.Id, bom.Id, new DateOnly(2026, 4, 1), 10m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        var service = new WorkOrderService(_db);
        await service.ReleaseAsync(company.Id, order.Id);
        await service.CompleteAsync(company.Id, order.Id, 10m, "admin");
        Assert.Equal(WorkOrderStatus.Completed, order.Status);

        Assert.Equal(80m, await new InventoryService(_db).GetOnHandAsync(company.Id, raw.Id, warehouse.Id));

        await service.CancelAsync(company.Id, order.Id, "admin", new DateOnly(2026, 4, 2));
        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);

        var rawOnHand = await new InventoryService(_db).GetOnHandAsync(company.Id, raw.Id, warehouse.Id);
        Assert.Equal(100m, rawOnHand);

        var originals = await _db.JournalEntries
            .Where(e => (e.SourceDocument == $"WorkOrderComplete:{order.Id}"
                      || e.SourceDocument == $"WorkOrderReceipt:{order.Id}")
                     && !e.Description.StartsWith("قيد عكسي"))
            .ToListAsync();
        Assert.Equal(2, originals.Count);
        Assert.All(originals, e => Assert.Equal(JournalEntryStatus.Reversed, e.Status));
        Assert.All(originals, e => Assert.NotNull(e.ReversalEntryId));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
