using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;
using QRCoder;

namespace Brqosh.Desktop.Views;

public partial class PosReceiptWindow : Window
{
    public PosReceiptWindow(SalesInvoice invoice, ZatcaEInvoiceDocument? zatca, string? zatcaMessage, string companyName)
    {
        InitializeComponent();
        InvoiceNumberText.Text = invoice.InvoiceNumber ?? "—";
        TotalText.Text = $"{invoice.GrandTotal():N2} ر.س";
        VatText.Text = $"ضريبة: {invoice.VatAmount:N2} ر.س";
        SubtotalText.Text = $"قبل الضريبة: {invoice.Subtotal:N2} ر.س";
        CompanyText.Text = companyName;

        if (zatca is not null)
        {
            // ‏(جاهزيّةُ الإطلاق · ح-٥٣) **الرمزُ من القارئ الواحد** — بيعُ نقطةٍ لعميلٍ مسجَّلٍ
            // ‏مستندُ تخليص، وما لم يُعتمد منه لا يُعرض برمزٍ بل بتنبيه. والنصُّ داخل اللوحة
            // ‏نفسِها، فتبقى ظاهرةً ويُطوى الرمزُ وحدَه.
            var mark = ZatcaHandOverCopy.MarkFor(zatca, () => null);
            ZatcaStatusText.Text = mark.ClearanceNotice ?? zatcaMessage ?? zatca.ComplianceStatus.ToString();
            if (mark.QrTlvBase64 is { } qr)
                QrImage.Source = BuildQrImage(qr);
            else
                QrImage.Visibility = Visibility.Collapsed;
            QrPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ZatcaStatusText.Text = "ZATCA غير مفعّلة — فاتورة محلية فقط";
        }
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
