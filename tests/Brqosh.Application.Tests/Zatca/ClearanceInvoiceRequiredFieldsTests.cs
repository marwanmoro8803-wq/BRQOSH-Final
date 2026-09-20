using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨) <b>حقلان تشترطهما الهيئةُ كانا غائبَين عن المستند كلِّه.</b>
///
/// <para><b>تاريخُ التوريد</b> (<c>cac:Delivery/cbc:ActualDeliveryDate</c>، BR-KSA-11):
/// <b>وهو يُحدّد الفترةَ التي تُقرَّر فيها الضريبة</b> — لا بيانٌ وصفيّ. <b>ومبلغُ السطر
/// شاملَ الضريبة</b> (<c>cac:TaxTotal/cbc:RoundingAmount</c> في كلّ سطر،
/// BR-KSA-EN16931-11): كان الوعاءُ يحمل الضريبةَ وحدَها.</para>
///
/// <para><b>والغيابُ يُرفض في تصديق المخطّط</b> — أي قبل أن تُقرأ أرقامُ المستند أو يُفحص
/// توقيعُه، فلا حيلةَ تشغيليّةً تُنجّي منه.</para>
///
/// <para><b>وموضعُ <c>cac:Delivery</c> مُلزِمٌ لا اصطلاح:</b> متتالية UBL تضعه بعد طرف
/// المشتري وقبل <c>cac:TaxTotal</c>. ووضعُه في غيره يُرفض كما رُفض ترتيبٌ معكوسٌ في Z-6.</para>
/// </summary>
public class ClearanceInvoiceRequiredFieldsTests
{
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private static XElement Build(
        bool creditNote = false, DateOnly? supplyDate = null,
        IReadOnlyList<SalesInvoiceLine>? lines = null)
    {
        var data = new ZatcaUblInvoiceBuilder.InvoiceData(
            InvoiceNumber: "INV-2026-000001", Uuid: Guid.NewGuid(),
            InvoiceDate: new DateOnly(2026, 3, 14), IssueTime: new TimeOnly(12, 0, 0),
            SellerName: "منشأة الاختبار", SellerVat: "310123456700003",
            CustomerName: "عميل", CustomerVat: "311111111100003",
            Subtotal: 1000m, VatAmount: 150m, GrandTotal: 1150m,
            Lines: lines ?? [new SalesInvoiceLine("صنف", 1m, 1000m)],
            Icv: "1", PreviousInvoiceHash: "cHJldg==", QrTlvBase64: "AQE=",
            IsCreditNote: creditNote, IsSimplified: false,
            BillingReferenceInvoiceNumber: creditNote ? "INV-2026-000000" : null,
            SupplyDate: supplyDate);

        // ‏المستندُ يخرج ببادئة BOM، و`XElement.Parse` يرفضها — فيُقرأ مجرًى لا نصّاً.
        using var stream = new MemoryStream(ZatcaUblInvoiceBuilder.BuildXml(data));
        return XDocument.Load(stream).Root!;
    }

    // ── تاريخُ التوريد ──────────────────────────────────────────────────────

    /// <summary><b>القياسُ الأوّل:</b> الفاتورةُ تحمل تاريخَ توريدٍ، وهو يومُها حين لا يُمرَّر غيرُه.</summary>
    [Fact]
    public void AStandardInvoice_CarriesTheSupplyDate()
    {
        var delivery = Build().Element(Cac + "Delivery");

        Assert.NotNull(delivery);
        Assert.Equal("2026-03-14", delivery!.Element(Cbc + "ActualDeliveryDate")?.Value);
    }

    /// <summary>
    /// <b>وحين يُفصَل التوريدُ عن الإصدار يُكتب الحقيقيّ</b> — بضاعةٌ سُلّمت في شهرٍ
    /// وفُوتِرت في تاليه تُقرَّر ضريبتُها في فترة التوريد.
    /// </summary>
    [Fact]
    public void WhenSupplyDiffersFromIssue_TheSuppliedDateIsWritten()
    {
        var delivery = Build(supplyDate: new DateOnly(2026, 2, 27)).Element(Cac + "Delivery");

        Assert.Equal("2026-02-27", delivery?.Element(Cbc + "ActualDeliveryDate")?.Value);
    }

    /// <summary>
    /// <b>وموضعُه في المتتالية مُلزِمٌ لا اصطلاح:</b> <c>cac:Delivery</c> بعد طرف المشتري
    /// وقبل <c>cac:TaxTotal</c> و<c>cac:LegalMonetaryTotal</c>. ووضعُه في غير موضعه يُرفض
    /// في تصديق المخطّط — أي قبل أن تُقرأ أرقامُ المستند، كما وقع في Z-6.
    ///
    /// <para><b>وأمّا <c>cac:PaymentMeans</c> فلا يجتمع معه أصلاً</b> في هذا المنتَج:
    /// الأوّلُ للفواتير والثاني للإشعارات وحدَها. <b>وأوّلُ صياغةٍ لهذه الحالة افترضت
    /// اجتماعَهما فسقطت</b> — وأسقطها تأكيدُ التهيئة الذي وُضع لذلك، لا فحصُ الترتيب.</para>
    /// </summary>
    [Fact]
    public void Delivery_SitsInItsRequiredSequencePosition()
    {
        var names = Build().Elements().Select(e => e.Name.LocalName).ToList();

        var customer = names.IndexOf("AccountingCustomerParty");
        var delivery = names.IndexOf("Delivery");
        var taxTotal = names.IndexOf("TaxTotal");
        var monetary = names.IndexOf("LegalMonetaryTotal");

        Assert.True(delivery >= 0, "‏لا `Delivery` في المستند.");
        Assert.True(customer >= 0 && taxTotal >= 0 && monetary >= 0,
            "‏عنصرٌ مرجعيٌّ غائب — فالتهيئةُ لا تقيس الترتيب.");

        Assert.True(customer < delivery, "‏`Delivery` قبل طرف المشتري.");
        Assert.True(delivery < taxTotal, "‏`Delivery` بعد `TaxTotal`.");
        Assert.True(delivery < monetary, "‏`Delivery` بعد `LegalMonetaryTotal`.");
    }

    /// <summary>
    /// <b>شاهدُ ضبط:</b> الإشعارُ الدائنُ بلا تاريخِ توريد — فهو يُصحّح فاتورةً سابقةً
    /// ولا يُوثّق توريداً جديداً، ومرجعُه في <c>cac:BillingReference</c>.
    /// </summary>
    [Fact]
    public void ACreditNote_CarriesNoDeliveryDate()
    {
        Assert.Null(Build(creditNote: true).Element(Cac + "Delivery"));
    }

    // ── مبلغُ السطر شاملَ الضريبة ───────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الثاني:</b> كلُّ سطرٍ يحمل <c>RoundingAmount</c> = صافيه + ضريبته
    /// <b>بالقيمة لا بالوجود</b> — فقيمةٌ تخالف المجموعَ تُرفض كما يُرفض غيابُها.
    /// </summary>
    [Fact]
    public void EveryInvoiceLine_CarriesItsAmountIncludingVat()
    {
        var root = Build(lines:
        [
            new SalesInvoiceLine("صنف أوّل", 2m, 300m),
            new SalesInvoiceLine("صنف ثانٍ", 1m, 400m),
        ]);

        var lines = root.Elements(Cac + "InvoiceLine").ToList();
        Assert.Equal(2, lines.Count);

        foreach (var line in lines)
        {
            var net = decimal.Parse(line.Element(Cbc + "LineExtensionAmount")!.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            var tax = line.Element(Cac + "TaxTotal")!;
            var vat = decimal.Parse(tax.Element(Cbc + "TaxAmount")!.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            var rounding = tax.Element(Cbc + "RoundingAmount");

            Assert.True(rounding is not null,
                "‏سطرٌ بلا `RoundingAmount` — والهيئةُ تشترطه في كلّ سطر.");
            Assert.Equal(net + vat,
                decimal.Parse(rounding!.Value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// <b>وعملةُ المبلغ تُعلَن كأخواتها</b> — مبلغٌ بلا عملةٍ في مستندٍ متعدّد العملات
    /// يُقرأ بعملة المستند خطأً.
    /// </summary>
    [Fact]
    public void TheLineAmount_DeclaresItsCurrency()
    {
        var rounding = Build()
            .Elements(Cac + "InvoiceLine").First()
            .Element(Cac + "TaxTotal")!
            .Element(Cbc + "RoundingAmount");

        Assert.Equal("SAR", rounding?.Attribute("currencyID")?.Value);
    }
}
