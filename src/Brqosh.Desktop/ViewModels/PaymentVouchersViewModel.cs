using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق شاشة سندات الصرف خارج الشاشة: التحميل والعكس ومسار الاعتماد هنا، والشاشة تعرض وتسأل المستخدم فقط.
/// </summary>
public sealed class PaymentVouchersViewModel
{
    public IReadOnlyList<PaymentVouchersPage.VoucherRow> Rows { get; private set; } = [];
    public int PostedCount { get; private set; }
    public decimal PostedTotal { get; private set; }

    internal async Task LoadAsync(PagingState paging)
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.PaymentVouchers.Where(v => v.CompanyId == AppServices.CompanyId);
        paging.SetTotal(await baseQuery.CountAsync());

        var vouchers = await baseQuery
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync();

        var supplierIds = vouchers.Select(v => v.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var wfIds = vouchers.Where(v => v.WorkflowInstanceId.HasValue)
            .Select(v => v.WorkflowInstanceId!.Value).ToList();
        var workflows = wfIds.Count == 0
            ? new Dictionary<long, WorkflowInstance>()
            : await db.WorkflowInstances
                .Include(w => w.Approvals)
                .Where(w => wfIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id);

        PostedTotal = await baseQuery
            .Where(v => v.Status == PaymentVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        Rows = vouchers.Select(v =>
        {
            workflows.TryGetValue(v.WorkflowInstanceId ?? 0, out var wf);
            return new PaymentVouchersPage.VoucherRow(
                v.Id, v.VoucherNumber, v.VoucherDate,
                suppliers.GetValueOrDefault(v.SupplierId, "—"),
                v.Amount,
                v.Method == ReceiptMethod.Cash ? "نقداً" : "بنك",
                v.Reference, v.Description,
                PaymentVouchersPage.StatusLabel(v.Status), v.Status,
                PaymentVouchersPage.PipelineStepLabel(v.Status, wf), v.WorkflowInstanceId, v.JournalEntryId);
        }).ToList();

        PostedCount = await baseQuery.CountAsync(v => v.Status == PaymentVoucherStatus.Posted);
    }

    public async Task ReverseAsync(long id, DateOnly reversalDate)
    {
        using var facade = new DesktopAccountingFacade();
        var username = SecurityContext.ResolveUsername();
        await facade.GetService<VoucherReversalService>().ReversePaymentAsync(
            AppServices.CompanyId, id, reversalDate, username);
    }

    public async Task SubmitAsync(long id)
    {
        await using var db = AppServices.CreateDb();
        var voucher = await db.PaymentVouchers.SingleAsync(v => v.Id == id);
        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PaymentVoucherWorkflowService>().SubmitAsync(voucher);
    }

    public async Task ApproveAsync(long id)
    {
        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PaymentVoucherWorkflowService>().ApproveAsync(id);
    }

    public async Task RejectAsync(long id, string reason)
    {
        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PaymentVoucherWorkflowService>()
            .RejectAsync(id, reason);
    }
}
