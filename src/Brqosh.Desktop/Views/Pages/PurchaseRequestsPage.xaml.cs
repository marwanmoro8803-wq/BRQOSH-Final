using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseRequestsPage : UserControl
{
    public record RequestRow(
        long Id,
        string? RequestNumber,
        string RequestDate,
        string SupplierName,
        decimal Subtotal,
        string StatusAr,
        PurchaseRequestStatus Status,
        string PipelineStep,
        long? WorkflowInstanceId);

    private static readonly WorkflowApproverSlot[] PurchaseChain =
        [WorkflowApproverSlot.Manager, WorkflowApproverSlot.Finance];

    public PurchaseRequestsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (SubmitBtn, PermissionService.CanPostAccounting),
                (ApproveBtn, PermissionService.CanPostAccounting),
                (RejectBtn, PermissionService.CanPostAccounting),
                (ConvertPoBtn, PermissionService.CanPostAccounting));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var requests = await db.PurchaseRequests
            .Where(r => r.CompanyId == AppServices.CompanyId)
            .OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id)
            .Take(100)
            .ToListAsync();

        var supplierIds = requests.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var wfIds = requests.Where(r => r.WorkflowInstanceId.HasValue)
            .Select(r => r.WorkflowInstanceId!.Value).ToList();
        var workflows = wfIds.Count == 0
            ? new Dictionary<long, WorkflowInstance>()
            : await db.WorkflowInstances
                .Include(w => w.Approvals)
                .Where(w => wfIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id);

        Grid.ItemsSource = requests.Select(r =>
        {
            workflows.TryGetValue(r.WorkflowInstanceId ?? 0, out var wf);
            return new RequestRow(
                r.Id,
                r.RequestNumber ?? "—",
                r.RequestDate.ToString("yyyy-MM-dd"),
                suppliers.GetValueOrDefault(r.SupplierId, "—"),
                r.Subtotal,
                StatusLabel(r.Status),
                r.Status,
                PipelineStepLabel(r.Status, wf),
                r.WorkflowInstanceId);
        }).ToList();

        UiTestAutomation.PublishGridCount(Grid, requests.Count);
    }

    private static string StatusLabel(PurchaseRequestStatus status) => status switch
    {
        PurchaseRequestStatus.Draft => "مسودة",
        PurchaseRequestStatus.Submitted => "مُرسَل",
        PurchaseRequestStatus.Approved => "معتمد",
        PurchaseRequestStatus.Rejected => "مرفوض",
        PurchaseRequestStatus.ConvertedToPo => "→ PO",
        _ => status.ToString(),
    };

    private static string PipelineStepLabel(PurchaseRequestStatus status, WorkflowInstance? wf) => status switch
    {
        PurchaseRequestStatus.Draft => "—",
        PurchaseRequestStatus.Submitted => wf?.CurrentRequiredSlot(PurchaseChain) switch
        {
            WorkflowApproverSlot.Manager => "بانتظار المدير",
            WorkflowApproverSlot.Finance => "بانتظار المالية",
            _ => wf?.Status == WorkflowInstanceStatus.Approved ? "مكتمل" : "مُرسَل",
        },
        PurchaseRequestStatus.Approved => "جاهز للتحويل",
        PurchaseRequestStatus.ConvertedToPo => "أُنشئ PO",
        PurchaseRequestStatus.Rejected => "مرفوض",
        _ => "—",
    };

    private RequestRow? Selected => Grid.SelectedItem as RequestRow;

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new PurchaseRequestDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnView(object sender, RoutedEventArgs e) => await ViewSelectedAsync();
    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await ViewSelectedAsync();

    private async Task ViewSelectedAsync()
    {
        if (Selected is not { } row)
        {
            UiFeedback.ShowInfo(LocaleManager.T("اختر طلباً من الجدول.", "Select a request."));
            return;
        }

        await using var db = AppServices.CreateDb();
        var request = await db.PurchaseRequests.SingleAsync(r => r.Id == row.Id);
        var supplier = await db.Suppliers.SingleAsync(s => s.Id == request.SupplierId);
        var header = $"رقم: {request.RequestNumber ?? "—"}  |  {row.StatusAr}  |  {row.PipelineStep}  |  المورد: {supplier.NameAr}";
        new DocumentLinesViewDialog("طلب شراء", header, request.Lines.ToList(), request.Subtotal, 0m, request.Subtotal)
        {
            Owner = Window.GetWindow(this),
        }.ShowDialog();
    }

    private async void OnSubmit(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(SubmitAsync);

    private async Task SubmitAsync()
    {
        if (Selected is not { Status: PurchaseRequestStatus.Draft } row)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً في حالة مسودة.", "Select a draft request."));
            return;
        }

        await using var db = AppServices.CreateDb();
        var request = await db.PurchaseRequests.SingleAsync(r => r.Id == row.Id);
        var number = await DocumentNumberGenerator.NextAsync(
            db.PurchaseRequests.Where(r => r.CompanyId == AppServices.CompanyId).Select(r => r.RequestNumber),
            "PR", request.RequestDate.Year);

        using var scope = AppServices.CreateInfrastructureScope();
        var svc = scope.ServiceProvider.GetRequiredService<PurchaseRequestWorkflowService>();
        await svc.SubmitAsync(request, number);
        UiFeedback.ShowInfo(LocaleManager.T($"تم إرسال {number} للموافقة.", $"Submitted {number}."));
        await ReloadAsync();
    }

    private async void OnApprove(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ApproveAsync);

    private async Task ApproveAsync()
    {
        if (Selected is not { Status: PurchaseRequestStatus.Submitted } row)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً مُرسَلاً.", "Select a submitted request."));
            return;
        }

        using var scope = AppServices.CreateInfrastructureScope();
        var svc = scope.ServiceProvider.GetRequiredService<PurchaseRequestWorkflowService>();
        await svc.ApproveAsync(row.Id);
        UiFeedback.ShowInfo(LocaleManager.T("تمت خطوة الموافقة.", "Approval step recorded."));
        await ReloadAsync();
    }

    private async void OnReject(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RejectAsync);

    private async Task RejectAsync()
    {
        if (Selected is not { Status: PurchaseRequestStatus.Submitted } row)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً مُرسَلاً.", "Select a submitted request."));
            return;
        }

        var reason = UiDialogs.PromptText("رفض الطلب", "سبب الرفض:");
        if (string.IsNullOrWhiteSpace(reason))
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        var svc = scope.ServiceProvider.GetRequiredService<PurchaseRequestWorkflowService>();
        await svc.RejectAsync(row.Id, reason.Trim());
        UiFeedback.ShowInfo(LocaleManager.T("تم رفض الطلب.", "Request rejected."));
        await ReloadAsync();
    }

    private async void OnConvertPo(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ConvertPoAsync);

    private async Task ConvertPoAsync()
    {
        if (Selected is not { Status: PurchaseRequestStatus.Approved } row)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً معتمداً.", "Select an approved request."));
            return;
        }

        await using var db = AppServices.CreateDb();
        var request = await db.PurchaseRequests.SingleAsync(r => r.Id == row.Id);
        var orderNumber = await DocumentNumberGenerator.NextAsync(
            db.PurchaseOrders.Where(o => o.CompanyId == AppServices.CompanyId).Select(o => o.OrderNumber),
            "PO", request.RequestDate.Year);

        using var scope = AppServices.CreateInfrastructureScope();
        var svc = scope.ServiceProvider.GetRequiredService<PurchaseRequestWorkflowService>();
        var po = await svc.ConvertToPurchaseOrderAsync(row.Id, orderNumber);
        UiFeedback.ShowInfo(LocaleManager.T(
            $"تم إنشاء أمر الشراء {po.OrderNumber}.",
            $"Purchase order {po.OrderNumber} created."));
        await ReloadAsync();
    }
}
