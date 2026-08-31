using Brqosh.Infrastructure.Platform;
using System.Globalization;

using Brqosh.Infrastructure.Inventory;
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

                // (CEEF-229 · م2-٢٧) فحصُ الكود قبل الحفظ — وإلا تحدّثت قاعدة البيانات بلغتها:
                // انتهاكُ تفرّدٍ يُعرض نصُّه الإنجليزيّ في حوارٍ عربيّ، والمالك لا يرى الصنف
                // الحاجز في أيّ قائمة (قد يكون محذوفاً) ولا يفهم لماذا رُفض كوده.
                var availability = await new CodeAvailabilityQuery(db)
                    .CheckAsync(CodeAvailabilityScope.Product, AppServices.CompanyId, CodeBox.Text);
                if (CodeAvailabilityQuery.Explain(availability, CodeAvailabilityScope.Product, CodeBox.Text.Trim())
                    is string reason)
                    throw new AccountingException(reason);

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

