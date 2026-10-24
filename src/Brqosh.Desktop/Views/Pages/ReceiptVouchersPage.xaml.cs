using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ReceiptVouchersPage : UserControl
{
    private readonly PagingState _paging = new(100);

    public record VoucherRow(long Id, string? VoucherNumber, DateOnly VoucherDate, string CustomerName,
        decimal Amount, string MethodAr, string Reference, string Description, string StatusAr,
        ReceiptVoucherStatus Status, long? JournalEntryId);

    public ReceiptVouchersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewVoucherBtn, PermissionService.CanPostAccounting),
                (ReverseBtn, PermissionService.CanCancelOrReverse));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.ReceiptVouchers.Where(v => v.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await baseQuery.CountAsync());

        var vouchers = await baseQuery
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
            .ToListAsync();

        var customerIds = vouchers.Select(v => v.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var total = await baseQuery
            .Where(v => v.Status == ReceiptVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        var rows = vouchers.Select(v => new VoucherRow(
            v.Id, v.VoucherNumber, v.VoucherDate,
            customers.GetValueOrDefault(v.CustomerId, "—"),
            v.Amount,
            v.Method == ReceiptMethod.Cash ? "نقداً" : "بنك",
            v.Reference, v.Description,
            StatusLabel(v.Status), v.Status, v.JournalEntryId)).ToList();
        VouchersGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(VouchersGrid, rows.Count);
        CountText.Text = _paging.StatusText("السندات");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;

        var postedCount = await baseQuery.CountAsync(v => v.Status == ReceiptVoucherStatus.Posted);
        VoucherSummaryText.Text =
            $"سندات: {_paging.TotalCount:N0} · مرحّل: {postedCount:N0} · إجمالي التحصيلات المرحّلة: {total:N2} {CurrencyLabel.Short}";
        UiTestAutomation.PublishSummary(VoucherSummaryText, VoucherSummaryText.Text);
    }

    private static string StatusLabel(ReceiptVoucherStatus status) => status switch
    {
        ReceiptVoucherStatus.Posted => LocaleManager.T("مرحّل", "Posted"),
        ReceiptVoucherStatus.Reversed => LocaleManager.T("معكوس", "Reversed"),
        _ => LocaleManager.T("مسودة", "Draft"),
    };

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
        var dialog = new ReceiptVoucherDialog { Owner = Window.GetWindow(this) };
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
            UiDialog.Show($"سند قبض {row.VoucherNumber}\nالعميل: {row.CustomerName}\nالمبلغ: {row.Amount:N2}\n{row.Description}",
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
            s => s.ExportReceiptVoucherAsync(AppServices.CompanyId, row.Id, username),
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

        if (row.Status != ReceiptVoucherStatus.Posted)
        {
            UiDialog.Show("لا يُعكس إلا سند مرحّل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (UiDialog.Show($"عكس سند القبض {row.VoucherNumber}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ عكس سند القبض:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<VoucherReversalService>().ReverseReceiptAsync(
                AppServices.CompanyId, row.Id, reversalDate.Value, username);
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
