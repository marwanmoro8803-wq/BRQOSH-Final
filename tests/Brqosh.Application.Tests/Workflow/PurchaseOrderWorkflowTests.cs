using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Workflow;

public class PurchaseOrderWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;

    public PurchaseOrderWorkflowTests()
    {
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
            .InitializeAsync("شركة PO-WF", "310123456700010", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(_companyId, "مورد PO", "300000000000010");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }

    private PurchaseOrderWorkflowService Svc() =>
        new(_db, new WorkflowEngine(_db));

    [Fact]
    public async Task FullChain_Submit_Manager_Finance_ApprovesOrder()
    {
        await SetupAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9), "معدات");
        order.AddLine("طابعة", 1m, 2000m);
        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "buyer";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(order);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);

        SecurityContext.CurrentUsername = "manager";
        await Svc().ApproveAsync(order.Id, "PO-SHOULD-NOT-FINALIZE");
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);

        SecurityContext.CurrentUsername = "finance";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await Svc().ApproveAsync(order.Id, "PO-2026-010");
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        Assert.Equal("PO-2026-010", order.OrderNumber);
    }

    [Fact]
    public async Task Reject_StopsChain()
    {
        await SetupAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9));
        order.AddLine("صنف", 1m, 100m);
        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "buyer";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(order);

        SecurityContext.CurrentUsername = "manager";
        await Svc().RejectAsync(order.Id, "غير معتمد");

        Assert.Equal(PurchaseOrderStatus.Rejected, order.Status);
    }
}
