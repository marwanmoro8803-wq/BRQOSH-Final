using System.Windows;
using System.Windows.Controls;
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

    /// <summary>
    /// (CEEF-229 · م1-١ب) حقولُ الفاتورة القياسية تظهر عند اكتمال الرقم الضريبيّ وحده.
    ///
    /// <para>فالعميلُ النقديّ (بلا رقم ضريبيّ) تُصدَر له فاتورةٌ مبسّطة لا تحتاج عنواناً،
    /// والكاشيرُ لا يسأل الزبونَ عن حيّه. أمّا صاحبُ الرقم الضريبيّ فتذهب فاتورتُه إلى
    /// المقاصّة قبل التسليم، وطرفُ مشتريها معرَّف.</para>
    ///
    /// <para>والحارسُ الأوّل شرطٌ لا احتياط: هذا المعالج يُطلَق أثناء <c>InitializeComponent</c>
    /// إن ضُبط نصُّ الحقل من XAML، واللوحةُ بعده في الشجرة — وهو العطل نفسه الذي أُغلق في م2-٥.</para>
    /// </summary>
    private void OnVatChanged(object sender, TextChangedEventArgs e) => ShowRegulatoryIfStandard();

    private void ShowRegulatoryIfStandard()
    {
        if (RegulatoryPanel is null) return;

        RegulatoryPanel.Visibility = VatBox.Text.Trim().Length == 15
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var vat = VatBox.Text.Trim();
            if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
                throw new ArgumentException("الرقم الضريبي للعميل يجب أن يتكون من 15 رقماً.");

            await using var db = AppServices.CreateDb();
            Customer saved;
            if (_customerId is long id)
            {
                saved = await db.Customers.SingleAsync(c => c.Id == id);
                saved.Update(NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
            }
            else
            {
                saved = Customer.Create(
                    AppServices.CompanyId, NameBox.Text, VatBox.Text, PhoneBox.Text, AddressBox.Text);
                db.Customers.Add(saved);
            }

            // (CEEF-229 · م1-١ب) هويةُ المشتري تُحفظ لصاحب الرقم الضريبيّ وحده — وفاتورتُه
            // تذهب إلى المقاصّة قبل التسليم، فطرفُها المشتري معرَّف.
            if (vat.Length == 15)
            {
                saved.UpdateRegulatoryIdentity(
                    RegistrationNameBox.Text,
                    Brqosh.Domain.Companies.NationalAddress.Create(
                        buildingNumber: BuildingNumberBox.Text,
                        streetName: StreetNameBox.Text,
                        districtName: DistrictNameBox.Text,
                        cityName: CityNameBox.Text,
                        postalZone: PostalZoneBox.Text,
                        countrySubentity: CountrySubentityBox.Text,
                        additionalNumber: AdditionalNumberBox.Text));

                // ‏والنقصُ يُقال باسمه هنا — لا يُكتشف بعد شهرٍ حين ترفض الهيئةُ الفاتورة.
                var gaps = saved.StandardInvoiceGaps();
                if (gaps.Count > 0)
                    throw new Brqosh.Domain.Accounting.AccountingException(
                        "بيانات الفاتورة القياسية ناقصة:\n- " + string.Join("\n- ", gaps));
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
