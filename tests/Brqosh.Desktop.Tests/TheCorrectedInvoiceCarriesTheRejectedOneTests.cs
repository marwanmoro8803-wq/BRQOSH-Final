using System.Windows.Threading;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Sales;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#8</c>) <b>شاشةُ الفاتورة المصحَّحة تبني ما كانت عليه المرفوضة</b> — من البيانات إلى الكيان الذي
/// يُرحَّل، عبر <see cref="SalesInvoiceDialog.BuildInvoiceFromRows"/> نفسِه الذي يستدعيه زرُّ الترحيل.
///
/// <para><b>وما يُقاس هنا ما كانت الشاشةُ تُسقطه</b>: وحدةُ السطر ودفعتُه لم تكن تمرّ من الشبكة إلى الفاتورة — وفاتورةٌ أُدخلت هنا لا
/// تحملهما فلم يظهر ذلك. <b>ونسخُ «٢ كرتون» بلا وحدته يُرحّل «٢ حبّة»</b>.</para>
/// </summary>
public class TheCorrectedInvoiceCarriesTheRejectedOneTests
{
    private static readonly Dispatcher UiDispatcher = WpfTestHost.Dispatcher;

    private static readonly CorrectedInvoiceSeed Seed = new(
        CustomerId: 7, CurrencyCode: "USD", ExchangeRate: 3.75m, WarehouseId: null, DocumentDiscountAmount: 5m,
        Lines:
        [
            new SalesInvoiceLine("كرتون مياه", 2m, 40m, 11, VatCategory.Standard,
                BatchNumber: "B-7", DiscountPercent: 10m, UnitCode: "CTN"),
            new SalesInvoiceLine("تصدير", 1m, 100m, null, VatCategory.ZeroRated, "VATEX-SA-32", null),
        ]);

    [Fact]
    public void TheSeededDialog_BuildsAnInvoiceWithTheRejectedLinesUnitsBatchesAndCurrency()
    {
        var invoice = UiDispatcher.Invoke(() =>
            new SalesInvoiceDialog(Seed).BuildInvoiceFromRows(1, Seed.CustomerId, new DateOnly(2026, 3, 10)));

        Assert.Equal(("USD", 3.75m), (invoice.CurrencyCode, invoice.ExchangeRate));
        Assert.Equal(5m, invoice.DocumentDiscountAmount);
        Assert.Collection(invoice.Lines,
            l => Assert.Equal(("كرتون مياه", 2m, 40m, (long?)11, "B-7", 10m, "CTN"),
                (l.Description, l.Quantity, l.UnitPrice, l.ProductId, l.BatchNumber, l.DiscountPercent, l.UnitCode)),
            l => Assert.Equal((VatCategory.ZeroRated, "VATEX-SA-32"), (l.VatCategory, l.TaxExemptionReasonCode)));
    }

    /// <summary><b>وفاتورةٌ تُدخل باليد لا تتغيّر</b>: صفٌّ بلا وحدةٍ ولا دفعةٍ يبقى كما كان — وحدةُ الصنف ولا دفعة.</summary>
    [Fact]
    public void AHandEnteredRow_StillHasNoUnitAndNoBatch()
    {
        var invoice = UiDispatcher.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            dialog.RowsForTests.Add(new SalesInvoiceDialog.LineRow { Description = "خدمة", Quantity = 1m, UnitPrice = 50m });
            return dialog.BuildInvoiceFromRows(1, 7, new DateOnly(2026, 3, 10));
        });

        var line = Assert.Single(invoice.Lines);
        Assert.Equal(((string?)null, (string?)null), (line.UnitCode, line.BatchNumber));
    }
}
