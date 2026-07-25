using System.Windows;
using Brqosh.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SupplierDialog : Window
{
    private readonly long? _supplierId;

    public SupplierDialog() => InitializeComponent();

    public SupplierDialog(Supplier supplier) : this()
    {
        _supplierId = supplier.Id;
        NameBox.Text = supplier.NameAr;
        VatBox.Text = supplier.VatRegistrationNumber;
        PhoneBox.Text = supplier.Phone;
        AddressBox.Text = supplier.Address;
        Title = "تعديل مورد";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var vat = VatBox.Text.Trim();
            if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
                throw new ArgumentException("الرقم الضريبي للمورد يجب أن يتكون من 15 رقماً.");

            await using var db = AppServices.CreateDb();
            if (_supplierId is long id)
            {
                var supplier = await db.Suppliers.SingleAsync(s => s.Id == id);
                supplier.Update(NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
            }
            else
            {
                var supplier = Supplier.Create(
                    AppServices.CompanyId, NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
                db.Suppliers.Add(supplier);
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
