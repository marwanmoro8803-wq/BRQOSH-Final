using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class ReceiptVoucherDialog : Window
{
    public record CustomerOption(long Id, string Display);

    public ReceiptVoucherDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
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

    private async void OnCustomerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (CustomerCombo.SelectedValue is not long customerId)
            return;

        await using var db = AppServices.CreateDb();
        var balance = await new CustomerBalanceQuery(db).ForCustomerAsync(AppServices.CompanyId, customerId);
        BalanceText.Text = $"الرصيد المستحق على العميل: {balance.Balance:N2}";
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ السند مطلوب.");
            if (CustomerCombo.SelectedValue is not long customerId)
                throw new AccountingException("اختر العميل.");
            if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                throw new AccountingException("أدخل مبلغاً صحيحاً.");

            var voucher = ReceiptVoucher.CreateDraft(
                AppServices.CompanyId, customerId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                amount,
                MethodCombo.SelectedIndex == 0 ? ReceiptMethod.Cash : ReceiptMethod.Bank,
                DescriptionBox.Text,
                ReferenceBox.Text);

            await using var db = AppServices.CreateDb();
            await new ReceiptVoucherPostingService(db).PostAsync(voucher);

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
