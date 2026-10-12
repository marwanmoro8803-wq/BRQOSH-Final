using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Retail;
using QRCoder;

namespace Brqosh.Desktop.Views;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٣ · ‏`ui-ux-i18n-printing#8`) <b>إيصالُ نقطة البيع — يُعرض ويُطبع.</b>
///
/// <para>‏كان يعرض الاسمَ والرقمَ والمبالغَ والرمز ويُغلق فقط. صار يعرض <see cref="PosReceipt"/> كاملاً — رقمَ تسجيل البائع والتاريخَ
/// ‏والوقتَ والأصناف — <b>ويطبع لوحتَه وحدَها</b> بعرض الشريط الحراريّ. والمحتوى من <see cref="PosReceiptBuilder"/> (مقيس)،
/// ‏والنافذةُ تعرض ولا تحسب.</para>
/// </summary>
public partial class PosReceiptWindow : Window
{
    private readonly string _invoiceNumber;

    /// <param name="statusMessage">رسالةُ الإرسال للهيئة إن وُجدت — تُعرض حين لا تنبيهَ في الإيصال.</param>
    public PosReceiptWindow(PosReceipt receipt, string? statusMessage)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        InitializeComponent();
        _invoiceNumber = receipt.InvoiceNumber;

        CompanyText.Text = receipt.SellerName;
        VatNumberText.Text = LocaleManager.T("الرقم الضريبي: ", "VAT No.: ") + receipt.SellerVatNumber;
        InvoiceNumberText.Text = receipt.InvoiceNumber;
        IssuedAtText.Text = receipt.IssuedAt.ToString("yyyy-MM-dd HH:mm");
        LinesList.ItemsSource = receipt.Lines;
        SubtotalText.Text = LocaleManager.T("قبل الضريبة: ", "Before VAT: ") + $"{receipt.Subtotal:N2}";
        VatText.Text = LocaleManager.T("ضريبة القيمة المضافة: ", "VAT: ") + $"{receipt.VatAmount:N2}";
        TotalText.Text = LocaleManager.T("الإجمالي: ", "Total: ") + $"{receipt.Total:N2} " + LocaleManager.T("ر.س", "SAR");

        // ‏(ح-٥٣) ‏الرمزُ من القارئ الواحد؛ وما لم يُعتمد يُطبع بتنبيهٍ لا برمز.
        if (receipt.PrintedQr is { } qr && BuildQrImage(qr) is { } image)
        {
            QrImage.Source = image;
            QrImage.Visibility = Visibility.Visible;
        }

        ZatcaStatusText.Text = receipt.Notice ?? statusMessage ?? string.Empty;
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true)
            dialog.PrintVisual(ReceiptPanel, "BRQOSH " + _invoiceNumber);
    }

    private static BitmapImage? BuildQrImage(string tlvBase64)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(tlvBase64, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(4);
            using var ms = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
