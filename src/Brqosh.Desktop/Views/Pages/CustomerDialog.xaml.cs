using System.Windows;
using Brqosh.Domain.Sales;

namespace Brqosh.Desktop.Views.Pages;

public partial class CustomerDialog : Window
{
    public CustomerDialog()
    {
        InitializeComponent();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var customer = Customer.Create(
                AppServices.CompanyId,
                NameBox.Text,
                VatBox.Text,
                PhoneBox.Text,
                AddressBox.Text);

            await using var db = AppServices.CreateDb();
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (ArgumentException ex)
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
