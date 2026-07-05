using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CreditNotesPage : UserControl
{
    public record NoteRow(string? NoteNumber, DateOnly NoteDate, string CustomerName,
        string InvoiceNumber, string Reason, decimal GrandTotal);

    public CreditNotesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var notes = await db.CreditNotes
            .Where(n => n.CompanyId == AppServices.CompanyId)
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.Id)
            .Take(100)
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
            n.NoteNumber, n.NoteDate,
            customers.GetValueOrDefault(n.CustomerId, "—"),
            invoices.GetValueOrDefault(n.OriginalInvoiceId, "—"),
            n.Reason, n.GrandTotal)).ToList();
    }

    private async void OnNewNote(object sender, RoutedEventArgs e)
    {
        var dialog = new CreditNoteDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }
}
