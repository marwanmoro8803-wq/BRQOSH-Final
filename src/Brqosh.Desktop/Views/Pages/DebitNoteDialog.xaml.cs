using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class DebitNoteDialog : Window
{
    private record InvoiceOption(long Id, long SupplierId, string Display, long? ProductId, string LineDesc, decimal UnitPrice);

    public DebitNoteDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var invoices = await db.PurchaseInvoices
            .Where(i => i.CompanyId == AppServices.CompanyId && i.Status == PurchaseInvoiceStatus.Posted)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(100)
            .ToListAsync();
        var suppliers = await db.Suppliers
            .Where(s => invoices.Select(i => i.SupplierId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        InvoiceBox.ItemsSource = invoices.Select(i =>
        {
            var line = i.Lines.FirstOrDefault();
            return new InvoiceOption(
                i.Id, i.SupplierId,
                $"{i.InvoiceNumber} — {suppliers.GetValueOrDefault(i.SupplierId, "?")} — {i.GrandTotal():N2}",
                line?.ProductId, line?.Description ?? "تعديل", line?.UnitPrice ?? 0m);
        }).ToList();
        if (InvoiceBox.Items.Count > 0) InvoiceBox.SelectedIndex = 0;
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        try
        {
            if (InvoiceBox.SelectedItem is not InvoiceOption inv)
                throw new InvalidOperationException("اختر فاتورة.");
            if (DateBox.SelectedDate is null)
                throw new InvalidOperationException("التاريخ مطلوب.");
            if (!decimal.TryParse(QtyBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)
                && !decimal.TryParse(QtyBox.Text, out qty))
                throw new InvalidOperationException("كمية غير صالحة.");
            if (!decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                && !decimal.TryParse(PriceBox.Text, out price))
                price = inv.UnitPrice;

            var note = DebitNote.CreateDraft(
                AppServices.CompanyId, inv.SupplierId, inv.Id,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value),
                ReasonBox.Text);
            var desc = string.IsNullOrWhiteSpace(DescBox.Text) ? inv.LineDesc : DescBox.Text;
            note.AddLine(desc, qty, price, inv.ProductId);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<DebitNotePostingService>().PostAsync(note);
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
