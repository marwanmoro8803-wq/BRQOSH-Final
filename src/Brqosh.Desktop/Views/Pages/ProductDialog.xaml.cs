using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProductDialog : Window
{
    private readonly long? _productId;

    public ProductDialog() => InitializeComponent();

    public ProductDialog(Product product) : this()
    {
        _productId = product.Id;
        CodeBox.Text = product.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = product.NameAr;
        UnitBox.Text = product.Unit;
        TypeCombo.SelectedIndex = product.Type == ProductType.Stockable ? 0 : 1;
        PriceBox.Text = product.SalePrice.ToString(CultureInfo.InvariantCulture);
        BarcodeBox.Text = product.Barcode;
        Title = "تعديل صنف";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!NumericInput.TryParse(PriceBox.Text, out var price))
                throw new AccountingException("سعر البيع غير صالح.");

            var type = TypeCombo.SelectedIndex == 0 ? ProductType.Stockable : ProductType.Service;
            await using var db = AppServices.CreateDb();

            if (_productId is long id)
            {
                var product = await db.Products.SingleAsync(p => p.Id == id);
                product.Update(NameBox.Text, UnitBox.Text, type, price, BarcodeBox.Text);
            }
            else
            {
                var product = Product.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text, UnitBox.Text,
                    type, price, BarcodeBox.Text);
                db.Products.Add(product);
            }

            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
