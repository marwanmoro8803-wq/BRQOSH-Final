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
using Brqosh.Desktop.ViewModels;

namespace Brqosh.Desktop.Views.Pages;

public partial class PaymentVouchersPage : UserControl
{
    private readonly PagingState _paging = new(100);
    private readonly PaymentVouchersViewModel _vm = new();

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
        await _vm.LoadAsync(_paging);
        VouchersGrid.ItemsSource = _vm.Rows;
        UiTestAutomation.PublishGridCount(VouchersGrid, _vm.Rows.Count);
        CountText.Text = _paging.StatusText("السندات");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;

        VoucherSummaryText.Text =
            $"سندات: {_paging.TotalCount:N0} · مرحّل: {_vm.PostedCount:N0} · إجمالي المدفوعات المرحّلة: {_vm.PostedTotal:N2} {CurrencyLabel.Short}";
        UiTestAutomation.PublishSummary(VoucherSummaryText, VoucherSummaryText.Text);
    }

    internal static string StatusLabel(PaymentVoucherStatus status) => status switch
    {
        PaymentVoucherStatus.Posted => LocaleManager.T("مرحّل", "Posted"),
        PaymentVoucherStatus.Reversed => LocaleManager.T("معكوس", "Reversed"),
        PaymentVoucherStatus.Submitted => LocaleManager.T("مُرسَل", "Submitted"),
        PaymentVoucherStatus.Rejected => LocaleManager.T("مرفوض", "Rejected"),
        _ => LocaleManager.T("مسودة", "Draft"),
    };

    internal static string PipelineStepLabel(PaymentVoucherStatus status, WorkflowInstance? wf) => status switch
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
            await _vm.ReverseAsync(row.Id, reversalDate.Value);
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

        await _vm.SubmitAsync(row.Id);
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

        await _vm.ApproveAsync(row.Id);
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

        await _vm.RejectAsync(row.Id, reason.Trim());
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }
}
