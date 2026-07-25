using System.Windows;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CustomerDialog : Window
{
    private readonly long? _customerId;

    public CustomerDialog()
    {
        InitializeComponent();
    }

    public CustomerDialog(Customer customer) : this()
    {
        _customerId = customer.Id;
        NameBox.Text = customer.NameAr;
        VatBox.Text = customer.VatRegistrationNumber;
        PhoneBox.Text = customer.Phone;
        AddressBox.Text = customer.Address;
        Title = "تعديل عميل";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var vat = VatBox.Text.Trim();
            if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
                throw new ArgumentException("الرقم الضريبي للعميل يجب أن يتكون من 15 رقماً.");

            await using var db = AppServices.CreateDb();
            if (_customerId is long id)
            {
                var customer = await db.Customers.SingleAsync(c => c.Id == id);
                customer.Update(NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
            }
            else
            {
                var customer = Customer.Create(
                    AppServices.CompanyId, NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
                db.Customers.Add(customer);
            }

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
