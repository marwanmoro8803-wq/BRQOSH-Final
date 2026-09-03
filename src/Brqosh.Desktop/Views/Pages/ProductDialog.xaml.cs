using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط1-٣د) <b>نمطُ التتبّع — و<c>SetTracking</c> كان بلا مستدعٍ في الواجهة كلِّها.</b>
///
/// <para><b>ما كان:</b> <c>Product.SetTracking</c> مبنيٌّ ومقيسٌ في الاختبارات، <b>ولا حقلَ
/// في أيّ شاشةٍ يصل إليه</b>. فصنفٌ لا يُعلَّم متتبَّعاً من الشاشة <b>لا تُدخَل له دفعةٌ ولا
/// صلاحية</b> — ولا يصلح لصرف الأقدم انتهاءً، ولا لصرف دواءٍ من زيارة، ولا لوصفة مطبخٍ
/// بمكوّنٍ متتبَّع. <b>وكلُّ ما بُني في <c>ط1-٣</c> و<c>ط3-٢ج</c> و<c>ط1-٣ج</c> كان خلف
/// بابٍ لا مفتاحَ له.</b></para>
///
/// <para><b>وتغييرُ النمط يُرفض وللصنف رصيدٌ قائم</b> — والنطاقُ يحرسه. <b>والرصيدُ يُقرأ
/// من المخزون لا يُفترَض صفراً</b>: صنفٌ عليه مئةُ حبّةٍ بلا دفعاتٍ ثمّ يُعلَّم متتبَّعاً
/// <b>يصير رصيدُه بلا هويّة</b> — لا يخرج ولا يُجرَد.</para>
/// </summary>
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
        TrackingCombo.SelectedIndex = (int)product.Tracking;
        Title = "تعديل صنف";
    }

    private InventoryTracking SelectedTracking => TrackingCombo.SelectedIndex switch
    {
        1 => InventoryTracking.Batch,
        2 => InventoryTracking.Serial,
        _ => InventoryTracking.None,
    };

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!NumericInput.TryParse(PriceBox.Text, out var price))
                throw new AccountingException("سعر البيع غير صالح.");

            var type = TypeCombo.SelectedIndex == 0 ? ProductType.Stockable : ProductType.Service;

            // ‏**وصنفٌ خدميٌّ لا يُتتبَّع**: لا مخزونَ له فلا دفعة. **وقبولٌ صامتٌ يُخزّن
            // ‏نمطاً لا أثرَ له**، ثمّ يُقرأ يوماً فيُرفض بيعُه بلا رقمِ دفعةٍ لا توجد.
            if (type == ProductType.Service && SelectedTracking != InventoryTracking.None)
                throw new AccountingException(
                    "الصنف الخدمي لا يُتتبَّع بدفعةٍ ولا برقمٍ تسلسليّ — لا مخزونَ له.");

            await using var db = AppServices.CreateDb();

            if (_productId is long id)
            {
                var product = await db.Products.SingleAsync(p => p.Id == id);
                product.Update(NameBox.Text, UnitBox.Text, type, price, BarcodeBox.Text);

                // ‏**والرصيدُ يُقرأ لا يُفترَض**: النطاقُ يرفض تغييرَ النمط ورصيدٌ قائم،
                // ‏**وتمريرُ صفرٍ هنا يُبطل حراستَه** فيصير رصيدٌ قائمٌ بلا هويّة.
                // ‏**والخدمةُ من الحقن لا بالبناء المباشر** — واصطلاحُ هذا المستودع
                // ‏مقيسٌ بحارسٍ (`DesktopDiGuardTests`).
                using var facade = new DesktopAccountingFacade();
                var onHand = await facade.GetService<InventoryService>()
                    .GetCompanyOnHandAsync(AppServices.CompanyId, id);

                product.SetTracking(SelectedTracking, onHand);
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

                var product = Product.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                    UnitBox.Text, type, price, BarcodeBox.Text);

                // ‏وصنفٌ جديدٌ رصيدُه صفرٌ بالضرورة.
                product.SetTracking(SelectedTracking, currentOnHand: 0m);
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
