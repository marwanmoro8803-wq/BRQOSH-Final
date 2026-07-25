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

public class PurchaseRequestWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;

    public PurchaseRequestWorkflowTests()
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
            .InitializeAsync("شركة WF", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(_companyId, "مورد WF", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }

    private PurchaseRequestWorkflowService Svc() =>
        new(_db, new WorkflowEngine(_db));

    [Fact]
    public async Task FullChain_Submit_Manager_Finance_ConvertToPo()
    {
        await SetupAsync();

        var request = PurchaseRequest.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9), "معدات مكتب");
        request.AddLine("طابعة", 2m, 1500m);
        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "employee";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(request, "PR-2026-001");

        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);

        SecurityContext.CurrentUsername = "manager";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().ApproveAsync(request.Id, "موافقة مدير");

        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);

        SecurityContext.CurrentUsername = "finance";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await Svc().ApproveAsync(request.Id, "موافقة مالية");

        Assert.Equal(PurchaseRequestStatus.Approved, request.Status);

        var po = await Svc().ConvertToPurchaseOrderAsync(request.Id, "PO-2026-001");
        Assert.Equal(PurchaseOrderStatus.Approved, po.Status);
        Assert.Equal(PurchaseRequestStatus.ConvertedToPo, request.Status);
        Assert.Equal(po.Id, request.PurchaseOrderId);
    }

    [Fact]
    public async Task Reject_StopsChain()
    {
        await SetupAsync();

        var request = PurchaseRequest.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9));
        request.AddLine("صنف", 1m, 100m);
        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "employee";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(request, "PR-REJ-01");

        SecurityContext.CurrentUsername = "manager";
        await Svc().RejectAsync(request.Id, "غير معتمد");

        Assert.Equal(PurchaseRequestStatus.Rejected, request.Status);
    }

    [Fact]
    public async Task SelfApproval_IsBlocked()
    {
        await SetupAsync();

        var request = PurchaseRequest.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9));
        request.AddLine("صنف", 1m, 50m);
        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "same-user";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(request, "PR-SELF-01");

        await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(
            () => Svc().ApproveAsync(request.Id));
    }
}
