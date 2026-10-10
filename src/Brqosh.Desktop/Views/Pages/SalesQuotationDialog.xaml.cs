using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesQuotationDialog : Window
{
    // ‏(جاهزيّةُ الإطلاق · ح-١١٥ · ‏`order-to-cash-procure-to-pay-inventory#8`) ‏**صفوفُ فاتورة المبيعات نفسُها** —
    // ‏`LineRow` و`ProductOption` و`TotalsOf` — فلا يُنسخ منطقٌ ولا تنحرف شاشةٌ عن أخرى.
    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];

    public SalesQuotationDialog()
    {
        InitializeComponent();
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            await using var db = AppServices.CreateDb();
            CustomerCombo.ItemsSource = await db.Customers
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .ToListAsync();
            if (CustomerCombo.Items.Count > 0)
                CustomerCombo.SelectedIndex = 0;
            ProductColumn.ItemsSource = await db.Products
                .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
                .OrderBy(p => p.Code)
                .Select(p => new SalesInvoiceDialog.ProductOption(
                    p.Id, $"{p.Code} — {p.NameAr}", p.SalePrice, p.NameAr))
                .ToListAsync();
        };
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateTotals);

    private void UpdateTotals()
    {
        var (subtotal, vat) = SalesInvoiceDialog.TotalsOf(_rows, 0m);
        SubtotalText.Text = subtotal.ToString("N2");
        VatText.Text = vat.ToString("N2");
        TotalText.Text = (subtotal + vat).ToString("N2");
    }

    /// <summary>الوصلةُ بين الشبكة والنطاق — الصنفُ والثلاثيُّ الضريبيّ يعبران إلى كلّ سطر.</summary>
    internal SalesQuotation BuildQuotationFromRows(long companyId, long customerId, DateOnly quotationDate)
    {
        var quotation = SalesQuotation.CreateDraft(companyId, customerId, quotationDate);
        foreach (var row in _rows.Where(r =>
                     !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
        {
            quotation.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice, row.ProductId,
                row.VatCategory, row.TaxExemptionReasonCode, row.TaxExemptionReason);
        }
        return quotation;
    }

    internal ObservableCollection<SalesInvoiceDialog.LineRow> RowsForTests => _rows;

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (CustomerCombo.SelectedItem is not Customer customer)
                throw new AccountingException("اختر عميلاً.");

            var quotation = BuildQuotationFromRows(
                AppServices.CompanyId, customer.Id, DateOnly.FromDateTime(DateTime.Today));

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<SalesQuotationService>().CreateAndSendAsync(quotation);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
