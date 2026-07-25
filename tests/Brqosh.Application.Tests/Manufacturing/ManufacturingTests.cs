using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

public class ManufacturingTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public ManufacturingTests()
    {
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Bom_and_work_order_release()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("مصنع", "300000000000010",
            Brqosh.Domain.Modules.BusinessProfile.Manufacturing);

        var finished = Brqosh.Domain.Inventory.Product.Create(company.Id, "FG1", "منتج نهائي", "قطعة",
            Brqosh.Domain.Inventory.ProductType.Stockable, 100m);
        var raw = Brqosh.Domain.Inventory.Product.Create(company.Id, "RM1", "مادة خام", "كجم",
            Brqosh.Domain.Inventory.ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, raw);
        await _db.SaveChangesAsync();

        var bom = BillOfMaterials.Create(company.Id, "BOM-01", "تجميع منتج", finished.Id);
        bom.AddLine(raw.Id, 2m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(company.Id, bom.Id, new DateOnly(2026, 4, 1), 10m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        await new WorkOrderService(_db).ReleaseAsync(company.Id, order.Id);
        Assert.Equal(WorkOrderStatus.Released, order.Status);
        Assert.NotNull(order.OrderNumber);
    }

    [Fact]
    public async Task Payroll_run_posts_balanced()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000011",
            Brqosh.Domain.Modules.BusinessProfile.General);

        var run = PayrollRun.CreateDraft(company.Id, 2026, 4, new DateOnly(2026, 4, 30));
        run.AddLine("أحمد", 8000m, 2000m, 500m, 400m, 400m);

        await new PayrollPostingService(_db).PostAsync(run, "admin");

        Assert.Equal(PayrollRunStatus.Posted, run.Status);
        Assert.NotNull(run.JournalEntryId);
    }

    [Fact]
    public async Task Payroll_run_can_be_reversed()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000012",
            Brqosh.Domain.Modules.BusinessProfile.General);

        var run = PayrollRun.CreateDraft(company.Id, 2026, 5, new DateOnly(2026, 5, 31));
        run.AddLine("سارة", 6000m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        await new PayrollReversalService(_db).ReverseAsync(
            company.Id, run.Id, new DateOnly(2026, 6, 1), "admin");

        Assert.Equal(PayrollRunStatus.Reversed, run.Status);
        var entries = await _db.JournalEntries.Where(e => e.SourceDocument == $"Payroll:{run.PeriodCode}").ToListAsync();
        Assert.Equal(2, entries.Count);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
