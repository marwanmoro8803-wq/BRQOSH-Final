using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class CreditNotesPage : UserControl
{
    private readonly PagingState _paging = new(100);

    public record NoteRow(long Id, string? NoteNumber, DateOnly NoteDate, string CustomerName,
        string InvoiceNumber, string Reason, decimal GrandTotal, string StatusAr, CreditNoteStatus Status,
        long? JournalEntryId);

    public CreditNotesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewNoteBtn, PermissionService.CanPostAccounting),
                (CancelNoteBtn, PermissionService.CanCancelOrReverse));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.CreditNotes.Where(n => n.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await baseQuery.CountAsync());

        var notes = await baseQuery
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.Id)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
            .ToListAsync();

        var customerIds = notes.Select(n => n.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var invoiceIds = notes.Select(n => n.OriginalInvoiceId).Distinct().ToList();
        var invoices = await db.SalesInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber ?? "—");

        NotesGrid.ItemsSource = notes.Select(n => new NoteRow(
            n.Id, n.NoteNumber, n.NoteDate,
            customers.GetValueOrDefault(n.CustomerId, "—"),
            invoices.GetValueOrDefault(n.OriginalInvoiceId, "—"),
            n.Reason, n.GrandTotal(),
            n.Status switch
            {
                CreditNoteStatus.Posted => "مرحّل",
                CreditNoteStatus.Cancelled => "ملغاة",
                _ => "مسودة",
            },
            n.Status, n.JournalEntryId)).ToList();
        UiTestAutomation.PublishGridCount(NotesGrid, notes.Count);
        CountText.Text = _paging.StatusText("الإشعارات");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;

        var posted = await baseQuery.CountAsync(n => n.Status == CreditNoteStatus.Posted);
        var postedTotal = await baseQuery
            .Where(n => n.Status == CreditNoteStatus.Posted)
            .SumGrandTotalAsync();
        NoteSummaryText.Text =
            $"إشعارات: {_paging.TotalCount:N0} · مرحّل: {posted:N0} · إجمالي: {postedTotal:N2} ر.س";
    }

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

    private async void OnNewNote(object sender, RoutedEventArgs e)
    {
        var dialog = new CreditNoteDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _paging.Reset();
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
    }

    private async void OnView(object sender, RoutedEventArgs e) => await ViewSelectedAsync();

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await ViewSelectedAsync();

    private async Task ViewSelectedAsync()
    {
        if (NotesGrid.SelectedItem is not NoteRow row)
        {
            MessageBox.Show("اختر إشعاراً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var note = await db.CreditNotes.SingleAsync(n => n.Id == row.Id);
        var header = $"رقم: {note.NoteNumber ?? "—"}  |  التاريخ: {note.NoteDate:yyyy-MM-dd}  |  السبب: {note.Reason}";
        new DocumentLinesViewDialog("إشعار دائن", header, note.Lines.ToList(),
            note.Subtotal, note.VatAmount, note.GrandTotal()) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (NotesGrid.SelectedItem is not NoteRow row)
        {
            MessageBox.Show("اختر إشعاراً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            MessageBox.Show("لا يوجد قيد محاسبي مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        if (NotesGrid.SelectedItem is not NoteRow row)
        {
            MessageBox.Show("اختر إشعاراً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportCreditNoteAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async void OnCancelNote(object sender, RoutedEventArgs e)
    {
        if (NotesGrid.SelectedItem is not NoteRow row)
        {
            MessageBox.Show("اختر إشعاراً للإلغاء.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status != CreditNoteStatus.Posted)
        {
            MessageBox.Show("لا يُلغى إلا إشعار دائن مرحّل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"إلغاء إشعار الدائن {row.NoteNumber}؟ سيتم عكس القيد والمخزون.", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء إشعار الدائن وقيد العكس:");
        if (cancelDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<CreditNoteCancellationService>().CancelAsync(
                AppServices.CompanyId, row.Id, cancelDate.Value, username);
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
