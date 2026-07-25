using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Workflow;

public class PaymentVoucherWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;

    public PaymentVoucherWorkflowTests()
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
            .InitializeAsync("شركة PV-WF", "310123456700011", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(_companyId, "مورد PV", "300000000000011");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }

    private PaymentVoucherWorkflowService Svc() =>
        new(_db, new WorkflowEngine(_db));

    [Fact]
    public async Task FullChain_Submit_Finance_Admin_ThenPost()
    {
        await SetupAsync();

        var voucher = PaymentVoucher.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 9), 500m,
            ReceiptMethod.Cash, "سداد مورد");
        _db.PaymentVouchers.Add(voucher);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "clerk";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(voucher);
        Assert.Equal(PaymentVoucherStatus.Submitted, voucher.Status);

        SecurityContext.CurrentUsername = "finance";
        await Svc().ApproveAsync(voucher.Id);
        Assert.Equal(PaymentVoucherStatus.Submitted, voucher.Status);

        SecurityContext.CurrentUsername = "admin";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await Svc().ApproveAsync(voucher.Id);

        await Svc().EnsureWorkflowApprovedAsync(voucher);
        await new PaymentVoucherPostingService(_db).PostAsync(voucher);
        Assert.Equal(PaymentVoucherStatus.Posted, voucher.Status);
    }

    [Fact]
    public async Task Post_BeforeWorkflowApproval_IsBlocked()
    {
        await SetupAsync();

        var voucher = PaymentVoucher.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 9), 250m,
            ReceiptMethod.Cash, "سداد");
        _db.PaymentVouchers.Add(voucher);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "clerk";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(voucher);

        await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(
            () => new PaymentVoucherPostingService(_db).PostAsync(voucher));
    }
}
