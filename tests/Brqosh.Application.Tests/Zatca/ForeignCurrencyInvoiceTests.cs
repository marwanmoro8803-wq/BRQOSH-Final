using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-230 · ط1-٤) <b>فاتورةٌ بعملةٍ أجنبية.</b>
///
/// <para><b>ما كان:</b> أربعةُ أعمدةِ عملةٍ في القاعدة كلِّها — على المنشأة والمجموعة
/// والحساب البنكيّ — <b>ولا واحدٌ على مستند</b>. فلا فاتورةَ ولا سندَ ولا قيدَ يقبل
/// عملةً غيرَ عملة الدفاتر.</para>
///
/// <para><b>ومُولِّد الفوترة كان يُثبّت <c>SAR</c> في عشرة مواضع</b>. فلو أُدخلت عملةٌ
/// أجنبيةٌ يوماً لخرج مستندٌ <b>يَسِم مبالغَ دولاريةً بالريال</b> — تحريفٌ يمرّ على
/// التصديق البنيويّ لأنّ المخطّط يفحص وجودَ الرمز لا صدقَه.</para>
///
/// <para><b>والقاعدةُ الحاكمة:</b> المستندُ يخرج بعملته، <b>والضريبةُ تُورَّد بالريال
/// أيّاً كانت</b> — فيلزمها إفصاحان: سعرُ الصرف، ومبلغُ الضريبة بالريال (BT-111).</para>
/// </summary>
public class ForeignCurrencyInvoiceTests
{
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    // ── النطاق ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: المستندُ بعملته والدفاترُ بالريال.</b>
    ///
    /// <para>ألفُ دولارٍ بسعرِ 3.75 — فالعميلُ مدينٌ بألفِ دولار، <b>والأستاذُ يحمل
    /// 3,750</b>. وهما رقمان لشيءٍ واحد، ولا يُغني أحدُهما عن الآخر.</para>
    /// </summary>
    [Fact]
    public void ADocumentInForeignCurrency_KeepsBothAmounts()
    {
        var invoice = Draft();
        invoice.SetCurrency("USD", 3.75m);
        invoice.AddLine("خدمة", 1m, 1000m);

        Assert.Equal(1000m, invoice.ForeignSubtotal);
        Assert.Equal(150m, invoice.ForeignVatAmount);
        Assert.Equal(1150m, invoice.ForeignGrandTotal);

        Assert.Equal(3750m, invoice.Subtotal);
        Assert.Equal(562.50m, invoice.VatAmount);
        Assert.Equal(4312.50m, invoice.GrandTotal());
    }

    /// <summary>
    /// <b>وفاتورةٌ بالريال لا يتغيّر فيها رقمٌ واحد.</b>
    ///
    /// <para>وهو القياسُ الذي يحمي كلَّ قاعدةٍ قائمة: <b>الأجنبيُّ يساوي الريالَ حرفاً</b>
    /// بسعرِ صرفٍ واحد، فلا تنكسر هويّةٌ ولا يتحرّك رصيد.</para>
    /// </summary>
    [Fact]
    public void ARiyalDocument_IsUnchanged()
    {
        var invoice = Draft();
        invoice.AddLine("خدمة", 2m, 500m);

        Assert.Equal("SAR", invoice.CurrencyCode);
        Assert.Equal(1m, invoice.ExchangeRate);
        Assert.Equal(invoice.Subtotal, invoice.ForeignSubtotal);
        Assert.Equal(invoice.VatAmount, invoice.ForeignVatAmount);
    }

    /// <summary><b>ورمزُ عملةٍ غيرُ صالحٍ يُرفض، وسعرٌ غيرُ موجبٍ كذلك.</b></summary>
    [Theory]
    [InlineData("US", 3.75)]
    [InlineData("USDD", 3.75)]
    [InlineData("USD", 0)]
    [InlineData("USD", -1)]
    public void AnInvalidCurrencyOrRate_IsRefused(string code, decimal rate) =>
        Assert.Throws<AccountingException>(() => Draft().SetCurrency(code, rate));

    // ── المستندُ الإلكترونيّ ────────────────────────────────────────────────

    /// <summary>
    /// <b>والمستندُ يُعلن عملتَه وسعرَها وضريبتَه بالريال.</b>
    ///
    /// <para>ثلاثةُ إفصاحات: <c>DocumentCurrencyCode</c> بالعملة، و<c>TaxCurrencyCode</c>
    /// <b>ريالٌ أبداً</b>، و<c>TaxExchangeRate</c>، و<c>TaxTotal</c> ثانٍ فيه المبلغُ
    /// بالريال وحدَه.</para>
    /// </summary>
    [Fact]
    public void TheUblDocument_DeclaresCurrencyRateAndSarTax()
    {
        var invoice = Draft();
        invoice.SetCurrency("USD", 3.75m);
        invoice.AddLine("خدمة", 1m, 1000m);

        var xml = Build(invoice);
        Assert.True(UblStructureValidator.Validate(xml).Count == 0,
            string.Join("\n", UblStructureValidator.Validate(xml)));

        Assert.Equal("USD", xml.Root!.Element(Cbc + "DocumentCurrencyCode")!.Value);
        Assert.Equal("SAR", xml.Root.Element(Cbc + "TaxCurrencyCode")!.Value);

        var rate = xml.Root.Element(Cac + "TaxExchangeRate")!;
        Assert.Equal("USD", rate.Element(Cbc + "SourceCurrencyCode")!.Value);
        Assert.Equal("SAR", rate.Element(Cbc + "TargetCurrencyCode")!.Value);

        // ‏مبالغُ المستند بعملته…
        var monetary = xml.Root.Element(Cac + "LegalMonetaryTotal")!;
        Assert.Equal("1150.00", monetary.Element(Cbc + "PayableAmount")!.Value);
        Assert.Equal("USD", monetary.Element(Cbc + "PayableAmount")!.Attribute("currencyID")!.Value);

        // ‏…والضريبةُ بالريال في عنصرٍ مستقلٍّ بلا أوعية.
        var sarTotal = xml.Root.Elements(Cac + "TaxTotal")
            .Single(t => t.Element(Cbc + "TaxAmount")!.Attribute("currencyID")!.Value == "SAR");
        Assert.Equal("562.50", sarTotal.Element(Cbc + "TaxAmount")!.Value);
        Assert.Empty(sarTotal.Elements(Cac + "TaxSubtotal"));
    }

    /// <summary>
    /// <b>ومستندٌ بالريال لا يحمل شيئاً من ذلك.</b>
    ///
    /// <para>عنصرُ صرفٍ في فاتورةٍ محلّية <b>يُقرأ تحويلاً لم يقع</b>، و<c>TaxTotal</c>
    /// ثانٍ يُقرأ ضريبةً مضاعفة.</para>
    /// </summary>
    [Fact]
    public void ARiyalDocument_CarriesNoExchangeElements()
    {
        var invoice = Draft();
        invoice.AddLine("خدمة", 1m, 1000m);

        var xml = Build(invoice);
        Assert.True(UblStructureValidator.Validate(xml).Count == 0,
            string.Join("\n", UblStructureValidator.Validate(xml)));

        Assert.Null(xml.Root!.Element(Cac + "TaxExchangeRate"));
        Assert.Single(xml.Root.Elements(Cac + "TaxTotal"));
    }

    // ── المِرصادُ يعضّ ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>ومستندٌ يُعلن ضريبةً بالريال لا تطابق سعرَه المُعلَن يسقط.</b>
    ///
    /// <para>وهو العطلُ الذي لا يكشفه فحصُ الوجود: العنصرُ موجودٌ، والرمزُ صحيح،
    /// <b>والمبلغُ يخالف الحساب</b>.</para>
    /// </summary>
    [Fact]
    public void AMismatchedSarTax_IsCaught()
    {
        var invoice = Draft();
        invoice.SetCurrency("USD", 3.75m);
        invoice.AddLine("خدمة", 1m, 1000m);

        var xml = Build(invoice);
        var sarTotal = xml.Root!.Elements(Cac + "TaxTotal")
            .Single(t => t.Element(Cbc + "TaxAmount")!.Attribute("currencyID")!.Value == "SAR");
        sarTotal.Element(Cbc + "TaxAmount")!.Value = "600.00";

        Assert.Contains(UblStructureValidator.Validate(xml),
            v => v.Contains("تخالف ضريبةَ المستند × سعرَ الصرف", StringComparison.Ordinal));
    }

    /// <summary><b>وعملةُ الضريبة إن لم تكن ريالاً يسقط المستند.</b></summary>
    [Fact]
    public void ANonSarTaxCurrency_IsCaught()
    {
        var invoice = Draft();
        invoice.AddLine("خدمة", 1m, 1000m);

        var xml = Build(invoice);
        xml.Root!.Element(Cbc + "TaxCurrencyCode")!.Value = "USD";

        Assert.Contains(UblStructureValidator.Validate(xml),
            v => v.Contains("TaxCurrencyCode يجب أن يكون SAR", StringComparison.Ordinal));
    }

    /// <summary><b>وفاتورةٌ أجنبيةٌ بلا إفصاحِ الريال تسقط (BT-111).</b></summary>
    [Fact]
    public void AForeignInvoiceWithoutTheSarDisclosure_IsCaught()
    {
        var invoice = Draft();
        invoice.SetCurrency("EUR", 4.10m);
        invoice.AddLine("خدمة", 1m, 200m);

        var xml = Build(invoice);
        xml.Root!.Elements(Cac + "TaxTotal")
            .Single(t => t.Element(Cbc + "TaxAmount")!.Attribute("currencyID")!.Value == "SAR")
            .Remove();

        Assert.Contains(UblStructureValidator.Validate(xml),
            v => v.Contains("بلا TaxTotal بالريال", StringComparison.Ordinal));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static SalesInvoice Draft() =>
        SalesInvoice.CreateDraft(1L, 7L, new DateOnly(2026, 5, 1));

    private static XDocument Build(SalesInvoice invoice) =>
        XDocument.Parse(System.Text.Encoding.UTF8.GetString(
            ZatcaUblInvoiceBuilder.BuildXml(new ZatcaUblInvoiceBuilder.InvoiceData(
                "INV-1", Guid.Parse("22222222-2222-2222-2222-222222222222"),
                invoice.InvoiceDate, new TimeOnly(10, 0),
                "منشأة القياس", "310123456700003", "عميل", null,
                invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.ForeignGrandTotal,
                invoice.Lines, "1", new string('A', 44), "QR",
                Seller: new ZatcaUblInvoiceBuilder.SellerIdentity(
                    "منشأة القياس", "1010101010",
                    new Brqosh.Domain.Companies.NationalAddress(
                        "1234", "الرياض", "العليا", "5678", "12345", "SA")),
                CurrencyCode: invoice.CurrencyCode,
                ExchangeRate: invoice.ExchangeRate,
                VatAmountInSar: invoice.VatAmount))).TrimStart('﻿'));
}
