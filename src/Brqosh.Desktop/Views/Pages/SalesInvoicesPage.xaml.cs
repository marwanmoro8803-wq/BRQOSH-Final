using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesInvoicesPage : UserControl
{
    private readonly PagingState _paging = new(100);

    public record InvoiceRow(long Id, string? InvoiceNumber, DateOnly InvoiceDate, string CustomerName,
        decimal Subtotal, decimal VatAmount, decimal GrandTotal, string StatusAr, SalesInvoiceStatus Status,
        long? JournalEntryId);

    public SalesInvoicesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewInvoiceBtn, PermissionService.CanPostAccounting),
                (CancelInvoiceBtn, PermissionService.CanCancelOrReverse));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.SalesInvoices.Where(i => i.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await baseQuery.CountAsync());

        var invoices = await baseQuery
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
            .ToListAsync();

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var rows = invoices.Select(i => new InvoiceRow(
            i.Id, i.InvoiceNumber, i.InvoiceDate,
            customers.GetValueOrDefault(i.CustomerId, "—"),
            i.Subtotal, i.VatAmount, i.GrandTotal(),
            i.Status switch
            {
                SalesInvoiceStatus.Posted => LocaleManager.T("مرحّلة", "Posted"),
                SalesInvoiceStatus.Cancelled => LocaleManager.T("ملغاة", "Cancelled"),
                _ => LocaleManager.T("مسودة", "Draft"),
            }, i.Status, i.JournalEntryId)).ToList();
        InvoicesGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(InvoicesGrid, rows.Count);
        CountText.Text = _paging.StatusText("الفواتير");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;
        await RenderKpisAsync(baseQuery);
    }

    private async Task RenderKpisAsync(IQueryable<SalesInvoice> baseQuery)
    {
        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var postedQuery = baseQuery.Where(i =>
            i.Status == SalesInvoiceStatus.Posted && i.InvoiceDate >= monthStart);

        var monthCount = await postedQuery.CountAsync();
        var monthTotal = await postedQuery.SumGrandTotalAsync();
        var vatTotal = await postedQuery.SumVatAsync();

        KpiGrid.Children.Clear();
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("فواتير الشهر", "Month invoices"), "INV", monthCount, "#2563EB", KpiValueKind.Count));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("إيراد الشهر", "Month revenue"), "RV", monthTotal, "#0D9488", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("ضريبة الشهر", "Month VAT"), "VT", vatTotal, "#7C3AED", KpiValueKind.Currency));
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
        var dialog = new SalesInvoiceDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _paging.Reset();
            await ReloadAsync();
        }
    }

    private async void OnView(object sender, RoutedEventArgs e) => await ViewSelectedAsync();

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await ViewSelectedAsync();

    private async Task ViewSelectedAsync()
    {
        if (InvoicesGrid.SelectedItem is not InvoiceRow row)
        {
            UiFeedback.ShowInfo("اختر فاتورة من الجدول.");
            return;
        }

        await using var db = AppServices.CreateDb();
        var invoice = await db.SalesInvoices.SingleAsync(i => i.Id == row.Id);
        var header = $"رقم: {invoice.InvoiceNumber ?? "—"}  |  التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}  |  العميل: {row.CustomerName}  |  الحالة: {row.StatusAr}";
        new DocumentLinesViewDialog("فاتورة مبيعات", header, invoice.Lines.ToList(),
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

        if (row.Status != SalesInvoiceStatus.Posted)
        {
            UiFeedback.ShowWarning("لا يُلغى إلا فاتورة مرحّلة.");
            return;
        }

        if (!UiFeedback.Confirm($"إلغاء الفاتورة {row.InvoiceNumber}؟ سيتم عكس القيد وإرجاع المخزون."))
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء الفاتورة وقيد العكس:");
        if (cancelDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<SalesInvoiceCancellationService>().CancelAsync(
                AppServices.CompanyId, row.Id, cancelDate.Value, username);
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

        if (row.Status != SalesInvoiceStatus.Posted)
        {
            UiFeedback.ShowInfo("الطباعة متاحة للفواتير المرحّلة فقط.");
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportSalesInvoiceAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(Preview: true, OfferSaveAs: true));
    }
}
