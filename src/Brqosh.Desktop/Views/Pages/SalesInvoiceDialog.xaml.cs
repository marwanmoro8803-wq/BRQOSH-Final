using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesInvoiceDialog : Window
{
    public record CustomerOption(long Id, string Display);

    public class LineRow
    {
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    private readonly ObservableCollection<LineRow> _rows = [];

    public SalesInvoiceDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        var customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count > 0)
            CustomerCombo.SelectedIndex = 0;
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
                throw new AccountingException("تاريخ الفاتورة مطلوب.");
            if (CustomerCombo.SelectedValue is not long customerId)
                throw new AccountingException("اختر العميل.");

            var invoice = SalesInvoice.CreateDraft(
                AppServices.CompanyId, customerId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value));

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
                invoice.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice);

            await using var db = AppServices.CreateDb();
            await new SalesInvoicePostingService(db).PostAsync(invoice);

            DialogResult = true;
            Close();
        }
        catch (AccountingException ex)
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
