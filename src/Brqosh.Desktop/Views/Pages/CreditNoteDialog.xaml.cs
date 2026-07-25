using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CreditNoteDialog : Window
{
    public record InvoiceOption(long Id, long CustomerId, decimal GrandTotal, string Display);

    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];

    public CreditNoteDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadInvoicesAsync();
    }

    private async Task LoadInvoicesAsync()
    {
        await using var db = AppServices.CreateDb();
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == AppServices.CompanyId && i.Status == SalesInvoiceStatus.Posted)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Take(200)
            .ToListAsync();

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        InvoiceCombo.ItemsSource = invoices.Select(i => new InvoiceOption(
            i.Id, i.CustomerId, i.GrandTotal(),
            $"{i.InvoiceNumber} — {customers.GetValueOrDefault(i.CustomerId, "؟")} — {i.GrandTotal():N2}")).ToList();
    }

    private async void OnInvoiceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (InvoiceCombo.SelectedItem is not InvoiceOption option)
            return;

        await using var db = AppServices.CreateDb();
        var credited = (await db.CreditNotes
            .Where(n => n.CompanyId == AppServices.CompanyId
                        && n.OriginalInvoiceId == option.Id
                        && n.Status == CreditNoteStatus.Posted)
            .ToListAsync())
            .SumGrandTotal();

        RemainingText.Text = (option.GrandTotal - credited).ToString("N2");
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateTotals);

    private void UpdateTotals()
    {
        var subtotal = _rows.Sum(r => r.LineTotal);
        var vat = Math.Round(subtotal * SalesInvoice.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
        SubtotalText.Text = subtotal.ToString("N2");
        VatText.Text = vat.ToString("N2");
        TotalText.Text = (subtotal + vat).ToString("N2");
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ الإشعار مطلوب.");
            if (InvoiceCombo.SelectedItem is not InvoiceOption option)
                throw new AccountingException("اختر الفاتورة الأصلية.");

            var note = CreditNote.CreateDraft(
                AppServices.CompanyId, option.CustomerId, option.Id,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                ReasonBox.Text);

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
                note.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<CreditNotePostingService>().PostAsync(note);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
