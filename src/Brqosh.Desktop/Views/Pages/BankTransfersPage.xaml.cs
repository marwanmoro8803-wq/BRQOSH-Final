using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class BankTransfersPage : UserControl
{
    public record TransferRow(long Id, string? EntryNumber, DateOnly EntryDate, string Description,
        decimal TotalDebit, decimal TotalCredit, string? SourceDocument);

    public BankTransfersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewTransferBtn, PermissionService.CanPostAccounting));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var entries = await db.JournalEntries
            .Where(j => j.CompanyId == AppServices.CompanyId
                && j.SourceDocument != null
                && j.SourceDocument.StartsWith("BankTransfer:"))
            .OrderByDescending(j => j.EntryDate).ThenByDescending(j => j.Id)
            .Take(200)
            .ToListAsync();

        var rows = entries.Select(j => new TransferRow(
            j.Id,
            j.EntryNumber,
            j.EntryDate,
            j.Description,
            j.TotalDebit,
            j.TotalCredit,
            j.SourceDocument)).ToList();

        TransfersGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(TransfersGrid, rows.Count);
        CountText.Text = $"{rows.Count:N0} تحويل";
        SummaryText.Text = rows.Count == 0
            ? "لا توجد تحويلات مسجّلة — استخدم «تحويل بنكي» لإنشاء أول قيد."
            : $"آخر {rows.Count:N0} تحويل بنكي مرحّل.";
    }

    private async void OnNewTransfer(object sender, RoutedEventArgs e)
    {
        var dialog = new BankTransferDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private void OnViewJournal(object sender, RoutedEventArgs e) => ViewSelectedJournal();

    private void OnGridDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => ViewSelectedJournal();

    private void ViewSelectedJournal()
    {
        if (TransfersGrid.SelectedItem is not TransferRow row)
        {
            MessageBox.Show("اختر تحويلاً من القائمة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new JournalEntryViewDialog(row.Id) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }
}
