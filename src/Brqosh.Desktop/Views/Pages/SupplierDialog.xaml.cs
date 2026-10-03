using System.Windows;
using Brqosh.Desktop.Ui;
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
        PaymentTermsBox.Text = supplier.PaymentTermDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Title = "تعديل مورد";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var vat = VatBox.Text.Trim();
            if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
                throw new ArgumentException("الرقم الضريبي للمورد يجب أن يتكون من 15 رقماً.");

            // ‏(جاهزيّةُ الإطلاق · ح-٩٦) مهلةُ السداد — فارغُها نقد، وغيرُ العدد الصحيح يُقال باسمه؛ وحدُّ السنة في المجال.
            var paymentTerms = PaymentTermsInput.Parse(PaymentTermsBox.Text);

            await using var db = AppServices.CreateDb();
            Supplier supplier;
            if (_supplierId is long id)
            {
                supplier = await db.Suppliers.SingleAsync(s => s.Id == id);
                supplier.Update(NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
            }
            else
            {
                supplier = Supplier.Create(
                    AppServices.CompanyId, NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
                db.Suppliers.Add(supplier);
            }

            supplier.SetPaymentTerms(paymentTerms);

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
