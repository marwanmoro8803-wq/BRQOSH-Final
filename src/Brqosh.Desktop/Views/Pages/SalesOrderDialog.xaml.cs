using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesOrderDialog : Window
{
    public SalesOrderDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await using var db = AppServices.CreateDb();
            CustomerCombo.ItemsSource = await db.Customers
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .ToListAsync();
            if (CustomerCombo.Items.Count > 0)
                CustomerCombo.SelectedIndex = 0;
        };
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CustomerCombo.SelectedItem is not Customer customer)
                throw new AccountingException("اختر عميلاً.");

            if (!NumericInput.TryParse(QuantityBox.Text, out var qty) || qty <= 0)
                throw new AccountingException("الكمية غير صالحة.");

            if (!NumericInput.TryParse(UnitPriceBox.Text, out var price) || price < 0)
                throw new AccountingException("السعر غير صالح.");

            var description = DescriptionBox.Text.Trim();
            if (description.Length == 0)
                throw new AccountingException("الوصف مطلوب.");

            var order = SalesOrder.CreateDraft(
                AppServices.CompanyId, customer.Id, DateOnly.FromDateTime(DateTime.Today));
            order.AddLine(description, qty, price);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<SalesOrderService>().ConfirmAsync(order);

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
