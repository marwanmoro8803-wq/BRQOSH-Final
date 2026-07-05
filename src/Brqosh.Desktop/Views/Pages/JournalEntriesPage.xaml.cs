using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class JournalEntriesPage : UserControl
{
    private const int PageSize = 100;

    public record EntryRow(string? EntryNumber, DateOnly EntryDate, string Description,
        decimal TotalDebit, decimal TotalCredit, string StatusAr);

    public JournalEntriesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        // Pagination من المصدر — لا نحمّل الجدول كاملاً مهما كبر
        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == AppServices.CompanyId)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
            .Take(PageSize)
            .ToListAsync();

        var total = await db.JournalEntries.CountAsync(e => e.CompanyId == AppServices.CompanyId);

        EntriesGrid.ItemsSource = entries.Select(e => new EntryRow(
            e.EntryNumber, e.EntryDate, e.Description,
            e.TotalDebit, e.TotalCredit, StatusAr(e.Status))).ToList();
        CountText.Text = $"إجمالي القيود: {total:N0} (يُعرض أحدث {Math.Min(PageSize, total)})";
    }

    private static string StatusAr(JournalEntryStatus status) => status switch
    {
        JournalEntryStatus.Posted => "مرحّل",
        JournalEntryStatus.Reversed => "معكوس",
        _ => "مسودة",
    };

    private async void OnNewEntry(object sender, RoutedEventArgs e)
    {
        var dialog = new JournalEntryDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }
}
