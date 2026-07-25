using System.Windows;
using Brqosh.Desktop.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class JournalEntryViewDialog : Window
{
    public record LineRow(string AccountCode, string? Description, decimal Debit, decimal Credit);

    private long _entryId;

    public JournalEntryViewDialog(long entryId)
    {
        InitializeComponent();
        _entryId = entryId;
        Loaded += async (_, _) => await LoadAsync(entryId);
    }

    private async Task LoadAsync(long entryId)
    {
        await using var db = AppServices.CreateDb();
        var entry = await db.JournalEntries
            .SingleAsync(e => e.CompanyId == AppServices.CompanyId && e.Id == entryId);

        TitleText.Text = $"قيد {entry.EntryNumber ?? entry.Id.ToString()}";
        var status = entry.Status switch
        {
            JournalEntryStatus.Posted => "مرحّل",
            JournalEntryStatus.Reversed => "معكوس",
            _ => "مسودة",
        };
        HeaderText.Text = $"التاريخ: {entry.EntryDate:yyyy-MM-dd}  |  الحالة: {status}  |  {entry.Description}";
        if (!string.IsNullOrWhiteSpace(entry.SourceDocument))
            HeaderText.Text += $"\nالمستند المصدر: {entry.SourceDocument}";

        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.NameAr);

        var rows = entry.Lines.Select(l =>
        {
            var name = accounts.GetValueOrDefault(l.AccountId, "");
            var desc = string.IsNullOrWhiteSpace(l.Description) ? name : l.Description;
            return new LineRow(l.AccountCode, desc, l.Debit, l.Credit);
        }).ToList();

        LinesGrid.ItemsSource = rows;
        TotalsText.Text = $"مجموع المدين: {entry.TotalDebit:N2}  |  مجموع الدائن: {entry.TotalCredit:N2}";
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportJournalEntryAsync(AppServices.CompanyId, _entryId, username),
            this,
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
