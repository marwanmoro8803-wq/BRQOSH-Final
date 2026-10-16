using Brqosh.Desktop.Ui;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class JournalEntriesPage : UserControl
{
    private readonly PagingState _paging = new(100);

    public record EntryRow(long Id, string? EntryNumber, DateOnly EntryDate, string Description,
        decimal TotalDebit, decimal TotalCredit, string StatusAr, JournalEntryStatus Status);

    public JournalEntriesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewEntryBtn, PermissionService.CanPostAccounting),
                (ReverseBtn, PermissionService.CanCancelOrReverse));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.JournalEntries.Where(e => e.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await baseQuery.CountAsync());

        var entries = await baseQuery
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
            .ToListAsync();

        var rows = entries.Select(e => new EntryRow(
            e.Id, e.EntryNumber, e.EntryDate, e.Description,
            e.TotalDebit, e.TotalCredit, StatusAr(e.Status), e.Status)).ToList();
        EntriesGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(EntriesGrid, rows.Count);
        CountText.Text = _paging.StatusText("القيود");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;

        var postedCount = await baseQuery.CountAsync(e => e.Status == JournalEntryStatus.Posted);
        EntrySummaryText.Text =
            $"قيود: {_paging.TotalCount:N0} · مرحّل: {postedCount:N0} · معروض: {rows.Count:N0}";
    }

    private static string StatusAr(JournalEntryStatus status) => status switch
    {
        JournalEntryStatus.Posted => LocaleManager.T("مرحّل", "Posted"),
        JournalEntryStatus.Reversed => LocaleManager.T("معكوس", "Reversed"),
        _ => LocaleManager.T("مسودة", "Draft"),
    };

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

    private async void OnNewEntry(object sender, RoutedEventArgs e)
    {
        var dialog = new JournalEntryDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _paging.Reset();
            await ReloadAsync();
        }
    }

    private void OnView(object sender, RoutedEventArgs e) => ViewSelected();

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => ViewSelected();

    private void ViewSelected()
    {
        if (EntriesGrid.SelectedItem is not EntryRow row)
        {
            UiFeedback.ShowInfo("اختر قيداً من الجدول.");
            return;
        }

        new JournalEntryViewDialog(row.Id) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private async void OnReverse(object sender, RoutedEventArgs e)
    {
        if (EntriesGrid.SelectedItem is not EntryRow row)
        {
            UiFeedback.ShowInfo("اختر قيداً للعكس.");
            return;
        }

        if (row.Status != JournalEntryStatus.Posted)
        {
            UiFeedback.ShowWarning("لا يُعكس إلا قيد مرحّل.");
            return;
        }

        if (!UiFeedback.Confirm($"عكس القيد {row.EntryNumber}؟"))
            return;

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ قيد العكس (يجب أن تكون الفترة مفتوحة):");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<JournalReversalService>().ReverseAsync(
                AppServices.CompanyId, row.Id, reversalDate.Value, username);
            await ReloadAsync();
            UiFeedback.ShowSuccess("تم عكس القيد.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }
}
