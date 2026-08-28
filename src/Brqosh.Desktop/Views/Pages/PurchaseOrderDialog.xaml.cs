using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseOrderDialog : Window
{
    public record SupplierOption(long Id, string Display);

    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];

    public PurchaseOrderDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => { await LoadSuppliersAsync(); await LoadProductsAsync(); };
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

    /// <summary>(CEEF-229 · م2-٢٠) أصنافُ عمود الاختيار — لم يكن للحوار قائمة أصناف أصلاً.</summary>
    private async Task LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        var products = await db.Products
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new SalesInvoiceDialog.ProductOption(p.Id, p.Code + " — " + p.NameAr, p.SalePrice, p.NameAr))
            .ToListAsync();
        ProductColumn.ItemsSource = products;
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(() => TotalText.Text = _rows.Sum(r => r.LineTotal).ToString("N2"));

    private async void OnApprove(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ الأمر مطلوب.");
            if (SupplierCombo.SelectedValue is not long supplierId)
                throw new AccountingException("اختر المورد.");

            DateOnly? expected = ExpectedDateBox.SelectedDate is null
                ? null
                : DateOnly.FromDateTime(ExpectedDateBox.SelectedDate.Value);

            var order = PurchaseOrder.CreateDraft(
                AppServices.CompanyId, supplierId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                notes: "", expectedDelivery: expected);

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description)))
                order.AddLine(row.Description!, row.Quantity, row.UnitPrice, row.ProductId, row.VatCategory);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<PurchaseOrderService>().ApproveAsync(order);

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
