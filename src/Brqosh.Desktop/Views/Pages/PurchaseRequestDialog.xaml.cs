using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseRequestDialog : Window
{
    public record SupplierOption(long Id, string Display);

    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];

    public PurchaseRequestDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadSuppliersAsync();
    }

    private async Task LoadSuppliersAsync()
    {
        await using var db = AppServices.CreateDb();
        SupplierCombo.ItemsSource = await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .Select(s => new SupplierOption(s.Id, s.NameAr))
            .ToListAsync();
        if (SupplierCombo.Items.Count > 0)
            SupplierCombo.SelectedIndex = 0;
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(() => TotalText.Text = _rows.Sum(r => r.LineTotal).ToString("N2"));

    private async void OnSaveDraft(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ الطلب مطلوب.");
            if (SupplierCombo.SelectedValue is not long supplierId)
                throw new AccountingException("اختر المورد.");

            var request = PurchaseRequest.CreateDraft(
                AppServices.CompanyId,
                supplierId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                JustificationBox.Text);

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description)))
                request.AddLine(row.Description!, row.Quantity, row.UnitPrice, row.ProductId);

            if (request.Lines.Count == 0)
                throw new AccountingException("أضف سطراً واحداً على الأقل.");

            await using var db = AppServices.CreateDb();
            db.PurchaseRequests.Add(request);
            await db.SaveChangesAsync();

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
