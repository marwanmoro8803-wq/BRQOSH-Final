using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.ViewModels;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseInvoicesPage : UserControl
{
    private readonly PagingState _paging = new(100);
    private readonly PurchaseInvoicesViewModel _vm = new();

    public record InvoiceRow(long Id, string? InvoiceNumber, string SupplierReference, DateOnly InvoiceDate,
        string SupplierName, decimal Subtotal, decimal VatAmount, decimal GrandTotal, string DeductibleAr,
        string StatusAr, PurchaseInvoiceStatus Status, long? JournalEntryId);

    public PurchaseInvoicesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewInvoiceBtn, PermissionService.CanPostAccounting),
                (CancelInvoiceBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await _vm.LoadAsync(_paging);
        InvoicesGrid.ItemsSource = _vm.Rows;
        UiTestAutomation.PublishGridCount(InvoicesGrid, _vm.Rows.Count);
        CountText.Text = _paging.StatusText("الفواتير");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;
        RenderKpis();
    }

    private void RenderKpis()
    {
        KpiGrid.Children.Clear();
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("فواتير الشهر", "Month invoices"), "PI", _vm.MonthCount, "#2563EB", KpiValueKind.Count));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("مشتريات الشهر", "Month purchases"), "PP", _vm.MonthTotal, "#DC2626", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("ضريبة الشهر", "Month VAT"), "VT", _vm.VatTotal, "#7C3AED", KpiValueKind.Currency));
    }

    private async void OnPrev(object sender, RoutedEventArgs e)
    {
        _paging.Prev();
        await ReloadAsync();
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        _paging.Next();
        await ReloadAsync();
    }

    private async void OnNewInvoice(object sender, RoutedEventArgs e)
    {
        var dialog = new PurchaseInvoiceDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _paging.Reset();
            await ReloadAsync();
        }
    }

    private async void OnView(object sender, RoutedEventArgs e) => await UiFeedback.RunViewSafeAsync(ViewSelectedAsync, "PurchaseInvoicesPage.View");

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await UiFeedback.RunViewSafeAsync(ViewSelectedAsync, "PurchaseInvoicesPage.View");

    private async Task ViewSelectedAsync()
    {
        if (InvoicesGrid.SelectedItem is not InvoiceRow row)
        {
            UiFeedback.ShowSelectionRequired(LocaleManager.T("اختر فاتورة من الجدول أولاً ثم اضغط «عرض».", "Select an invoice first, then press View."));
            return;
        }

        var invoice = await _vm.FindAsync(row.Id);
        if (invoice is null)
        {
            UiFeedback.ShowRecordMissing(LocaleManager.T("لم تعد هذه الفاتورة موجودة — حدّث الجدول وحاول مجدداً.", "This invoice no longer exists — refresh the list and try again."));
            return;
        }
        var header = $"رقم: {invoice.InvoiceNumber ?? "—"}  |  مرجع المورد: {invoice.SupplierInvoiceReference}  |  التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}  |  المورد: {row.SupplierName}";
        new DocumentLinesViewDialog("فاتورة مشتريات", header, invoice.Lines.ToList(),
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal()) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceRow row)
        {
            UiFeedback.ShowInfo("اختر فاتورة من الجدول.");
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiFeedback.ShowInfo("لا يوجد قيد محاسبي مرتبط.");
    }

    private async void OnCancelInvoice(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceRow row)
        {
            UiFeedback.ShowInfo("اختر فاتورة للإلغاء.");
            return;
        }

        if (row.Status != PurchaseInvoiceStatus.Posted)
        {
            UiFeedback.ShowWarning("لا يُلغى إلا فاتورة مرحّلة.");
            return;
        }

        if (!UiFeedback.Confirm($"إلغاء فاتورة المشتريات {row.InvoiceNumber}؟ سيتم عكس القيد والمخزون."))
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء فاتورة المشتريات وقيد العكس:");
        if (cancelDate is null)
            return;

        try
        {
            await _vm.CancelAsync(row.Id, cancelDate.Value);
            await ReloadAsync();
            UiFeedback.ShowSuccess("تم إلغاء الفاتورة.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceRow row)
        {
            UiFeedback.ShowInfo("اختر فاتورة من الجدول.");
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportPurchaseInvoiceAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }
}
