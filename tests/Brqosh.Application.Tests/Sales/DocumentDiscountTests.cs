using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (CEEF-230 · ط1-٢) <b>خصمُ السطر وخصمُ المستند — وأثرُهما الضريبيّ.</b>
///
/// <para><b>ما كان:</b> صفرُ ذكرٍ للخصم في النطاق كلِّه، وحسابان مبذوران في شجرة الحسابات
/// (<c>4103</c> و<c>5214</c>) <b>لا تمسّهما شيفرةٌ واحدة</b>. فمَن أراد خصماً خفّض سعرَ
/// الوحدة — <b>فيختفي أنّ خصماً وقع أصلاً</b>، ويخرج المستندُ بسعرٍ يخالف ما اتُّفق عليه.</para>
///
/// <para><b>والخطرُ الحقيقيّ في خصم المستند لا خصم السطر:</b> فاتورةٌ فيها وعاءان (أساسيّ
/// 15% وصفريّ 0%) وخصمٌ على الإجمالي — <b>وطرحُه من الإجمالي وحده يترك الوعاءَ الأساسيَّ
/// كما هو</b>، فتُحتسب الضريبةُ على مبلغٍ لم يُدفع ويُصرَّح للهيئة بأكثر من الواجب.</para>
/// </summary>
public class DocumentDiscountTests
{
    private const long Company = 1L;
    private const long Customer = 7L;

    // ── حسابُ السطر ─────────────────────────────────────────────────────────

    /// <summary><b>الوعاءُ الضريبيّ بعد الخصم لا قبله.</b></summary>
    [Fact]
    public void ALineDiscount_ReducesTheTaxableBase()
    {
        var invoice = Draft();
        invoice.AddLine("بضاعة", 10m, 100m, discountPercent: 10m);

        var line = invoice.Lines[0];
        Assert.Equal(1000m, line.GrossAmount);
        Assert.Equal(100m, line.DiscountAmount);
        Assert.Equal(900m, line.LineTotal);

        Assert.Equal(900m, invoice.Subtotal);
        Assert.Equal(135m, invoice.VatAmount);          // 900 × 15%
        Assert.Equal(1035m, invoice.GrandTotal());
    }

    /// <summary>
    /// <b>ومئةٌ بالمئة مقبولة، وما فوقها مرفوض.</b>
    ///
    /// <para>العيّنةُ المجّانية سطرٌ بخصمٍ كامل — ممارسةٌ قائمةٌ لا حالةٌ شاذّة. وما فوق
    /// المئة يقلب الوعاءَ سالباً.</para>
    /// </summary>
    [Fact]
    public void ADiscountAboveOneHundredPercent_IsRefused()
    {
        var invoice = Draft();
        invoice.AddLine("عيّنة", 1m, 50m, discountPercent: 100m);
        Assert.Equal(0m, invoice.Lines[0].LineTotal);

        Assert.Throws<AccountingException>(() =>
            invoice.AddLine("خطأ", 1m, 50m, discountPercent: 100.01m));
    }

    // ── خصمُ المستند: القياسُ المركزيّ ──────────────────────────────────────

    /// <summary>
    /// <b>خصمُ المستند يُوزَّع على الأوعية بنسبتها — لا يُطرح من الإجمالي.</b>
    ///
    /// <para>وعاءان: أساسيٌّ 600 وصفريٌّ 400، وخصمُ 100. فالحصص 60 و40، والوعاءُ الأساسيُّ
    /// يصير 540 وضريبتُه 81. <b>ولو طُرح الخصمُ من الإجمالي وحده لبقيت الضريبة 90</b> —
    /// تسعةُ ريالاتٍ تُصرَّح للهيئة على مبلغٍ لم يُدفع، في فاتورةٍ واحدة.</para>
    /// </summary>
    [Fact]
    public void ADocumentDiscount_IsSplitAcrossTaxCategoriesProRata()
    {
        var invoice = Draft();
        invoice.AddLine("خاضع", 6m, 100m);                                   // 600 @ 15%
        invoice.AddLine("صفريّ", 4m, 100m, vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-32");   // 400 @ 0%
        invoice.ApplyDocumentDiscount(100m);

        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(
            invoice.Lines, documentDiscount: invoice.DocumentDiscountAmount);

        var standard = subtotals.Single(s => s.Category == VatCategory.Standard);
        var zero = subtotals.Single(s => s.Category == VatCategory.ZeroRated);

        Assert.Equal(540m, standard.TaxableAmount);
        Assert.Equal(81m, standard.VatAmount);
        Assert.Equal(360m, zero.TaxableAmount);
        Assert.Equal(0m, zero.VatAmount);

        Assert.Equal(900m, invoice.Subtotal);
        Assert.Equal(81m, invoice.VatAmount);
    }

    /// <summary>
    /// <b>ومجموعُ الحصص يساوي الخصمَ بالضبط — والفلسُ الأخير مسكونٌ في وعاءٍ واحد.</b>
    ///
    /// <para><b>وثلاثةُ أوعيةٍ متساوية هي الحالةُ التي تفرّق</b>: خصمُ 10 على ثلاثة أوعية
    /// بالتساوي يُعطي 3.3333 لكلٍّ — والتقريبُ المستقلّ يجعل المجموع <b>9.99</b>، فيبقى
    /// فلسٌ لا وعاءَ له. والصوابُ 3.33 و3.33 و<b>3.34</b>.</para>
    ///
    /// <para><b>وقياسٌ بوعاءَين لا يكشف هذا</b>: حصّتاهما تُقرَّبان إحداهما لأعلى والأخرى
    /// لأدنى فيستقيم المجموعُ صدفةً. <b>وقد كتبتُ الاختبارَ بوعاءَين أوّلاً فنجت الطفرةُ</b>
    /// — والطفرةُ هي التي أخبرتني أنّ القياسَ كان زينة.</para>
    /// </summary>
    [Fact]
    public void TheAllocatedShares_SumExactlyToTheDiscount()
    {
        var invoice = Draft();
        invoice.AddLine("خاضع", 1m, 100m);
        invoice.AddLine("صفريّ", 1m, 100m, vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-32");
        invoice.AddLine("معفى", 1m, 100m, vatCategory: VatCategory.Exempt,
            taxExemptionReasonCode: "VATEX-SA-29");
        invoice.ApplyDocumentDiscount(10m);

        var allocation = LineDocumentTotalsCalculator.ComputeDiscountAllocation(invoice.Lines, 10m);

        Assert.Equal(3, allocation.Count);
        Assert.Equal(10m, allocation.Sum(a => a.Amount));
        Assert.Contains(allocation, a => a.Amount == 3.34m);
        Assert.Equal(290m, invoice.Subtotal);
    }

    /// <summary><b>ولا يتجاوز الخصمُ صافيَ السطور</b> — وفاتورةٌ بوعاءٍ سالبٍ إشعارُ دائن.</summary>
    [Fact]
    public void ADocumentDiscountBeyondTheLines_IsRefused()
    {
        var invoice = Draft();
        invoice.AddLine("بضاعة", 1m, 100m);

        var error = Assert.Throws<AccountingException>(() => invoice.ApplyDocumentDiscount(150m));
        Assert.Contains("يتجاوز صافي السطور", error.Message, StringComparison.Ordinal);
    }

    // ── الأثرُ في المستند الإلكترونيّ ───────────────────────────────────────

    /// <summary>
    /// <b>والمستندُ الإلكترونيّ يُعلن الخصم — ولا يُخفيه في سعرٍ مخفَّض.</b>
    ///
    /// <para>ثلاثُ معادلاتٍ تُفحص بالأرقام: <c>LineExtensionAmount</c> قبل الخصم،
    /// و<c>AllowanceTotalAmount</c> يساويه، و<c>TaxExclusiveAmount</c> فرقُهما. ومعها
    /// <b>مجموعُ الأوعية == TaxExclusiveAmount</b> — وهي التي تسقط إن لم يُوزَّع الخصم.</para>
    /// </summary>
    [Fact]
    public void TheUblDocument_DeclaresTheAllowanceAndItsArithmeticHolds()
    {
        var invoice = Draft();
        invoice.AddLine("خاضع", 6m, 100m, discountPercent: 5m);               // 600 − 30 = 570
        invoice.AddLine("صفريّ", 4m, 100m, vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-32");    // 400
        invoice.ApplyDocumentDiscount(97m);

        var xml = Parse(BuildUbl(invoice));
        var violations = UblStructureValidator.Validate(xml);
        Assert.True(violations.Count == 0, string.Join("\n", violations));

        var cac = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        var cbc = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        var monetary = xml.Root!.Element(cac + "LegalMonetaryTotal")!;
        Assert.Equal("970.00", monetary.Element(cbc + "LineExtensionAmount")!.Value);
        Assert.Equal("97.00", monetary.Element(cbc + "AllowanceTotalAmount")!.Value);
        Assert.Equal("873.00", monetary.Element(cbc + "TaxExclusiveAmount")!.Value);

        // ‏عنصرُ خصمٍ لكلّ وعاء — فالتوزيعُ مقروءٌ في المستند لا مستنتَجٌ من فرق الأرقام.
        var allowances = xml.Root.Elements(cac + "AllowanceCharge").ToList();
        Assert.Equal(2, allowances.Count);
        Assert.All(allowances, a => Assert.NotNull(a.Element(cac + "TaxCategory")));

        // ‏وخصمُ السطر يُعلَن بأصله ونسبته.
        var lineAllowance = xml.Root.Elements(cac + "InvoiceLine").First()
            .Element(cac + "AllowanceCharge")!;
        Assert.Equal("30.00", lineAllowance.Element(cbc + "Amount")!.Value);
        Assert.Equal("600.00", lineAllowance.Element(cbc + "BaseAmount")!.Value);
    }

    /// <summary>
    /// <b>والمِرصادُ يعضّ:</b> مستندٌ يُعلن خصماً ولا يُوزّعه على الأوعية يسقط.
    ///
    /// <para>وهذه الحالةُ بعينها هي ما كان سيقع لو طُرح الخصمُ من الإجمالي — <b>فالاختبارُ
    /// يُثبّت أنّ الحارسَ يراها</b>، لا أنّ الشيفرةَ الحالية سليمة.</para>
    /// </summary>
    [Fact]
    public void AnUnallocatedDiscount_IsCaughtByTheValidator()
    {
        var invoice = Draft();
        invoice.AddLine("خاضع", 6m, 100m);
        invoice.AddLine("صفريّ", 4m, 100m, vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-32");
        invoice.ApplyDocumentDiscount(100m);

        var xml = Parse(BuildUbl(invoice));
        var cac = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        var cbc = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        // ‏يُعاد الوعاءُ الأساسيُّ إلى ما قبل الخصم — أي «طُرح من الإجمالي ولم يُوزَّع».
        var standard = xml.Root!.Element(cac + "TaxTotal")!
            .Elements(cac + "TaxSubtotal")
            .First(s => s.Element(cac + "TaxCategory")!.Element(cbc + "ID")!.Value == "S");
        standard.Element(cbc + "TaxableAmount")!.Value = "600.00";

        var violations = UblStructureValidator.Validate(xml);
        Assert.Contains(violations, v => v.Contains("خصمٌ لم يُوزَّع على الأوعية", StringComparison.Ordinal));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static SalesInvoice Draft() =>
        SalesInvoice.CreateDraft(Company, Customer, new DateOnly(2026, 5, 1));

    /// <summary>‏والـBOM يُنزع: <c>Encoding.UTF8.GetString</c> يُبقيه فيسقط المحلّل.</summary>
    private static XDocument Parse(byte[] xml) =>
        XDocument.Parse(System.Text.Encoding.UTF8.GetString(xml).TrimStart('﻿'));

    private static byte[] BuildUbl(SalesInvoice invoice) =>
        ZatcaUblInvoiceBuilder.BuildXml(new ZatcaUblInvoiceBuilder.InvoiceData(
            "INV-1", Guid.Parse("11111111-1111-1111-1111-111111111111"),
            invoice.InvoiceDate, new TimeOnly(10, 0),
            "منشأة القياس", "310123456700003",
            "عميل", null,
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal(),
            invoice.Lines, "1", new string('A', 44), "QR",
            Seller: new ZatcaUblInvoiceBuilder.SellerIdentity(
                "منشأة القياس", "1010101010",
                new Brqosh.Domain.Companies.NationalAddress(
                    "1234", "الرياض", "العليا", "5678", "12345", "SA")),
            DocumentDiscount: invoice.DocumentDiscountAmount));
}
