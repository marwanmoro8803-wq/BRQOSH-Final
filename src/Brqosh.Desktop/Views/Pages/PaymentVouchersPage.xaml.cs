using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PaymentVouchersPage : UserControl
{
    private readonly PagingState _paging = new(100);

    private static readonly WorkflowApproverSlot[] PaymentVoucherChain =
        [WorkflowApproverSlot.Finance, WorkflowApproverSlot.Administrator];

    public record VoucherRow(long Id, string? VoucherNumber, DateOnly VoucherDate, string SupplierName,
        decimal Amount, string MethodAr, string Reference, string Description, string StatusAr,
        PaymentVoucherStatus Status, string PipelineStep, long? WorkflowInstanceId, long? JournalEntryId);

    public PaymentVouchersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewVoucherBtn, PermissionService.CanPostAccounting),
                (SubmitBtn, PermissionService.CanPostAccounting),
                (ApproveBtn, PermissionService.CanPostAccounting),
                (RejectBtn, PermissionService.CanPostAccounting),
                (ReverseBtn, PermissionService.CanCancelOrReverse));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.PaymentVouchers.Where(v => v.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await baseQuery.CountAsync());

        var vouchers = await baseQuery
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
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

        var total = await baseQuery
            .Where(v => v.Status == PaymentVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        var rows = vouchers.Select(v =>
        {
            workflows.TryGetValue(v.WorkflowInstanceId ?? 0, out var wf);
            return new VoucherRow(
                v.Id, v.VoucherNumber, v.VoucherDate,
                suppliers.GetValueOrDefault(v.SupplierId, "—"),
                v.Amount,
                v.Method == ReceiptMethod.Cash ? "نقداً" : "بنك",
                v.Reference, v.Description,
                StatusLabel(v.Status), v.Status,
                PipelineStepLabel(v.Status, wf), v.WorkflowInstanceId, v.JournalEntryId);
        }).ToList();
        VouchersGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(VouchersGrid, rows.Count);
        CountText.Text = _paging.StatusText("السندات");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;

        var postedCount = await baseQuery.CountAsync(v => v.Status == PaymentVoucherStatus.Posted);
        VoucherSummaryText.Text =
            $"سندات: {_paging.TotalCount:N0} · مرحّل: {postedCount:N0} · إجمالي المدفوعات المرحّلة: {total:N2} {CurrencyLabel.Short}";
        UiTestAutomation.PublishSummary(VoucherSummaryText, VoucherSummaryText.Text);
    }

    private static string StatusLabel(PaymentVoucherStatus status) => status switch
    {
        PaymentVoucherStatus.Posted => LocaleManager.T("مرحّل", "Posted"),
        PaymentVoucherStatus.Reversed => LocaleManager.T("معكوس", "Reversed"),
        PaymentVoucherStatus.Submitted => LocaleManager.T("مُرسَل", "Submitted"),
        PaymentVoucherStatus.Rejected => LocaleManager.T("مرفوض", "Rejected"),
        _ => LocaleManager.T("مسودة", "Draft"),
    };

    private static string PipelineStepLabel(PaymentVoucherStatus status, WorkflowInstance? wf) => status switch
    {
        PaymentVoucherStatus.Draft => "—",
        PaymentVoucherStatus.Submitted => wf?.CurrentRequiredSlot(PaymentVoucherChain) switch
        {
            WorkflowApproverSlot.Finance => LocaleManager.T("بانتظار المالية", "Awaiting finance"),
            WorkflowApproverSlot.Administrator => LocaleManager.T("بانتظار المدير", "Awaiting manager"),
            _ => wf?.Status == WorkflowInstanceStatus.Approved ? "جاهز للترحيل" : "مُرسَل",
        },
        PaymentVoucherStatus.Rejected => LocaleManager.T("مرفوض", "Rejected"),
        _ => "—",
    };

    private VoucherRow? Selected => VouchersGrid.SelectedItem as VoucherRow;

    private async void OnPrev(object sender, RoutedEventArgs e)
    {
        _paging.Prev();
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        _paging.Next();
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnNewVoucher(object sender, RoutedEventArgs e)
    {
        var dialog = new PaymentVoucherDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _paging.Reset();
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
    }

    private void OnView(object sender, RoutedEventArgs e)
    {
        if (VouchersGrid.SelectedItem is not VoucherRow row)
        {
            UiDialog.Show("اختر سنداً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show($"سند صرف {row.VoucherNumber}\nالمورد: {row.SupplierName}\nالمبلغ: {row.Amount:N2}\n{row.Description}",
                "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        if (VouchersGrid.SelectedItem is not VoucherRow row)
        {
            UiDialog.Show("اختر سنداً للطباعة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportPaymentVoucherAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async void OnReverse(object sender, RoutedEventArgs e)
    {
        if (VouchersGrid.SelectedItem is not VoucherRow row)
        {
            UiDialog.Show("اختر سنداً للعكس.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status != PaymentVoucherStatus.Posted)
        {
            UiDialog.Show("لا يُعكس إلا سند مرحّل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (UiDialog.Show($"عكس سند الصرف {row.VoucherNumber}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ عكس سند الصرف:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<VoucherReversalService>().ReversePaymentAsync(
                AppServices.CompanyId, row.Id, reversalDate.Value, username);
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnSubmit(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(SubmitAsync);

    private async Task SubmitAsync()
    {
        if (Selected is not { Status: PaymentVoucherStatus.Draft } row)
        {
            UiDialog.Show("اختر سنداً في حالة مسودة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var voucher = await db.PaymentVouchers.SingleAsync(v => v.Id == row.Id);
        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PaymentVoucherWorkflowService>().SubmitAsync(voucher);
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnApprove(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ApproveAsync);

    private async Task ApproveAsync()
    {
        if (Selected is not { Status: PaymentVoucherStatus.Submitted } row)
        {
            UiDialog.Show("اختر سنداً مُرسَلاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PaymentVoucherWorkflowService>().ApproveAsync(row.Id);
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnReject(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RejectAsync);

    private async Task RejectAsync()
    {
        if (Selected is not { Status: PaymentVoucherStatus.Submitted } row)
        {
            UiDialog.Show("اختر سنداً مُرسَلاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reason = UiDialogs.PromptText("رفض سند الصرف", "سبب الرفض:");
        if (string.IsNullOrWhiteSpace(reason))
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PaymentVoucherWorkflowService>()
            .RejectAsync(row.Id, reason.Trim());
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }
}
