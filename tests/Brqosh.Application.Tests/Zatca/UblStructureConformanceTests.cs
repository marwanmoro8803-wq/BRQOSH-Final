using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · ح-٣ و Z-6) مخرَج مولّد UBL يطابق متتالية المخطّط وعناصره الإلزامية.
///
/// <para><b>ثلاثة أعطال كانت تُسقط كل مستند في تصديق المخطّط — قبل أن يُقرأ رقمٌ أو يُفحص توقيع:</b>
/// <c>cac:BillingReference</c> بعد <c>cac:AdditionalDocumentReference</c> والمتتالية تعكسهما ·
/// العدّاد التسلسليّ يُكتب مرفقاً ثنائياً بدل <c>cbc:UUID</c> · و<c>cac:Signature</c> غائب تماماً
/// من جسد الفاتورة.</para>
///
/// <para><b>ولماذا لم يكشفها شيء:</b> <c>ZatcaGoldenTests</c> يقرأ عناصر بعينها بالاسم — ولا
/// يسأل عن <b>ترتيبها</b> ولا عن حضور ما لم يخطر له. وهو حارسٌ صادق ضمن نطاقه، لكن اسمه
/// «Golden» أوهم بتغطيةٍ شاملة. أداةُ القياس هنا تفحص البنية كلها لا قيمةً بعينها.</para>
///
/// <para><b>حدُّ الأداة مُصرَّح به:</b> تغطّي ما رُمِّز فيها — المتتالية والعناصر الإلزامية وصيغة
/// المراجع والأوعية الضريبية. ليست XSD رسمياً، ولا تُغني عن فحص الهيئة (م4).</para>
/// </summary>
public class UblStructureConformanceTests
{
    private static ZatcaUblInvoiceBuilder.InvoiceData Data(
        bool isCreditNote = false, bool isDebitNote = false, bool isSimplified = true,
        string? billingReference = null, IReadOnlyList<SalesInvoiceLine>? lines = null,
        string? issueReason = "مرتجع بضاعة",
        ZatcaUblInvoiceBuilder.SellerIdentity? seller = null) =>
        new(
            InvoiceNumber: "INV-2026-000001",
            Uuid: Guid.NewGuid(),
            InvoiceDate: new DateOnly(2026, 3, 14),
            SellerName: "منشأة الاختبار",
            SellerVat: "310123456700003",
            CustomerName: isSimplified ? "" : "عميل",
            CustomerVat: isSimplified ? null : "311111111100003",
            Subtotal: 1000m,
            VatAmount: 150m,
            GrandTotal: 1150m,
            Lines: lines ?? [new SalesInvoiceLine("صنف", 1m, 1000m)],
            Icv: "42",
            PreviousInvoiceHash: "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==",
            QrTlvBase64: "AQ5UZXN0AgsxMjM0NTY3ODkwMQ==",
            IsCreditNote: isCreditNote,
            IsDebitNote: isDebitNote,
            IsSimplified: isSimplified,
            BillingReferenceInvoiceNumber: billingReference,
            BillingReferenceUuid: billingReference is null ? null : Guid.NewGuid(),
            IssueReason: issueReason,
            Seller: seller ?? CompleteSeller,
            // ‏(م1-١ب) المشتري يُملأ للقياسية وحدها — والمبسّطة بلا مشترٍ معرَّف.
            Buyer: isSimplified ? null : CompleteBuyer);

    /// <summary>بائعٌ مكتمل البيانات — الحدّ الأدنى الذي تقبله الهيئة.</summary>
    /// <summary>
    /// (CEEF-229 · م1-١ب) مشترٍ مكتمل — تقابله <see cref="CompleteSeller"/>.
    /// وغيابُه كان يمرّ لأنّ المُصدِّق لم يكن يفحص طرفَ المشتري إطلاقاً.
    /// </summary>
    private static readonly ZatcaUblInvoiceBuilder.BuyerIdentity CompleteBuyer = new(
        RegistrationName: "مؤسسة العميل التجارية",
        Address: Brqosh.Domain.Companies.NationalAddress.Create(
            buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
            cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
            additionalNumber: "8765"));

    private static readonly ZatcaUblInvoiceBuilder.SellerIdentity CompleteSeller = new(
        RegistrationName: "منشأة الاختبار للتجارة",
        CommercialRegistrationNumber: "1010123456",
        Address: Brqosh.Domain.Companies.NationalAddress.Create(
            buildingNumber: "1234", streetName: "طريق الملك فهد", districtName: "العليا",
            cityName: "الرياض", postalZone: "12345", countrySubentity: "منطقة الرياض",
            additionalNumber: "5678"));

    private static XDocument Build(ZatcaUblInvoiceBuilder.InvoiceData data)
    {
        using var stream = new MemoryStream(ZatcaUblInvoiceBuilder.BuildXml(data));
        return XDocument.Load(stream);
    }

    // ── مطابقة الأنواع الأربعة ──────────────────────────────────────────────

    /// <summary><b>القياس الأساسيّ.</b> فاتورة مبسّطة تخرج مطابقة بلا مخالفة واحدة.</summary>
    [Fact]
    public void SimplifiedInvoice_ConformsToStructure()
    {
        var violations = UblStructureValidator.Validate(Build(Data()));

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>والقياسية (B2B) كذلك — طرف المشتري لا يكسر المتتالية.</summary>
    [Fact]
    public void StandardInvoice_ConformsToStructure()
    {
        var violations = UblStructureValidator.Validate(Build(Data(isSimplified: false)));

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// الإشعار الدائن — وهو النوع الذي كان الترتيب ينكسر عليه، إذ لا يُضاف
    /// <c>BillingReference</c> إلا له وللمدين.
    /// </summary>
    [Fact]
    public void CreditNote_ConformsToStructure()
    {
        var violations = UblStructureValidator.Validate(
            Build(Data(isCreditNote: true, billingReference: "INV-2026-000001")));

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>والإشعار المدين.</summary>
    [Fact]
    public void DebitNote_ConformsToStructure()
    {
        var violations = UblStructureValidator.Validate(
            Build(Data(isDebitNote: true, billingReference: "PI-2026-000001")));

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>فاتورة مختلطة الفئات: وعاءان ضريبيان، وكلاهما مكتمل البنية.</summary>
    [Fact]
    public void MixedVatCategories_ConformToStructure()
    {
        var lines = new List<SalesInvoiceLine>
        {
            new("صنف أساسيّ", 1m, 1000m),
            new("صادرات", 1m, 500m, null, VatCategory.ZeroRated, "VATEX-SA-32", "Export of goods"),
        };

        var violations = UblStructureValidator.Validate(Build(Data(lines: lines)));

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    // ── الأعطال الثلاثة، كلٌّ بقياسه ────────────────────────────────────────

    /// <summary>الترتيب: المرجع الأصليّ يسبق المراجع الإضافية.</summary>
    [Fact]
    public void BillingReference_PrecedesAdditionalDocumentReferences()
    {
        var doc = Build(Data(isCreditNote: true, billingReference: "INV-2026-000001"));
        var names = doc.Root!.Elements().Select(e => e.Name.LocalName).ToList();

        Assert.True(
            names.IndexOf("BillingReference") < names.IndexOf("AdditionalDocumentReference"),
            "BillingReference بعد المراجع الإضافية — عكسُ المتتالية");
    }

    /// <summary>العدّاد قيمةٌ نصّية في <c>cbc:UUID</c>، لا مرفقٌ ثنائيّ.</summary>
    [Fact]
    public void Icv_IsWrittenAsUuid_NotAttachment()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var icv = Build(Data()).Root!
            .Elements(cac + "AdditionalDocumentReference")
            .Single(r => r.Element(cbc + "ID")?.Value == "ICV");

        Assert.Equal("42", icv.Element(cbc + "UUID")?.Value);
        Assert.Null(icv.Element(cac + "Attachment"));
    }

    /// <summary><c>cac:Signature</c> حاضر بمعرّفه وأسلوبه المعياريَّين.</summary>
    [Fact]
    public void UblSignature_IsDeclaredInTheInvoiceBody()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var signature = Build(Data()).Root!.Element(cac + "Signature");

        Assert.NotNull(signature);
        Assert.Equal("urn:oasis:names:specification:ubl:signature:Invoice",
            signature!.Element(cbc + "ID")?.Value);
        Assert.Equal("urn:oasis:names:specification:ubl:dsig:enveloped:xades",
            signature.Element(cbc + "SignatureMethod")?.Value);
    }

    // ── حارسٌ للحارس: المُصدِّق يعرف كيف يقول «لا» ───────────────────────────

    /// <summary>
    /// <b>أهمّ اختبار في الملف.</b> مُصدِّقٌ لا يرفض شيئاً يمرّ على كل شيء — وهذا بعينه ما
    /// أسقط حرّاساً سابقة في هذا المستودع. تُعاد الأعطال الثلاثة على مستندٍ صحيح، ويُشترط
    /// أن يكشفها المُصدِّق باسمها.
    /// </summary>
    [Fact]
    public void Validator_DetectsEachOfTheThreeDefects()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        // (أ) التوقيع محذوف
        var noSignature = Build(Data());
        noSignature.Root!.Element(cac + "Signature")!.Remove();
        Assert.Contains(UblStructureValidator.Validate(noSignature), v => v.Contains("Signature"));

        // (ب) العدّاد أُعيد مرفقاً
        var icvAsAttachment = Build(Data());
        var icv = icvAsAttachment.Root!.Elements(cac + "AdditionalDocumentReference")
            .Single(r => r.Element(cbc + "ID")?.Value == "ICV");
        icv.Element(cbc + "UUID")!.Remove();
        icv.Add(new XElement(cac + "Attachment",
            new XElement(cbc + "EmbeddedDocumentBinaryObject", "42")));
        var icvViolations = UblStructureValidator.Validate(icvAsAttachment);
        Assert.Contains(icvViolations, v => v.Contains("ICV"));

        // (ج) الترتيب مقلوب
        var reordered = Build(Data(isCreditNote: true, billingReference: "INV-1"));
        var billing = reordered.Root!.Element(cac + "BillingReference")!;
        billing.Remove();
        reordered.Root.Add(billing);
        Assert.Contains(UblStructureValidator.Validate(reordered), v => v.Contains("ترتيب"));
    }

    /// <summary>
    /// وحارسٌ ثانٍ للحارس: المُصدِّق يكشف نقص الأوعية الضريبية وعناصرها — لئلا يكون رفضُه
    /// محصوراً بالأعطال الثلاثة التي بُني لها.
    /// </summary>
    [Fact]
    public void Validator_DetectsMissingTaxSubtotalParts()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var doc = Build(Data());
        var category = doc.Root!.Element(cac + "TaxTotal")!
            .Element(cac + "TaxSubtotal")!.Element(cac + "TaxCategory")!;
        category.Element(cbc + "Percent")!.Remove();

        Assert.Contains(UblStructureValidator.Validate(doc), v => v.Contains("Percent"));
    }

    /// <summary>وإشعارٌ دائن بلا مرجعٍ للفاتورة الأصلية يُرفض — وهو إلزاميّ نظاميّ.</summary>
    [Fact]
    public void CreditNoteWithoutBillingReference_IsRejected()
    {
        var violations = UblStructureValidator.Validate(Build(Data(isCreditNote: true)));

        Assert.Contains(violations, v => v.Contains("BillingReference"));
    }

    /// <summary>
    /// (Z-5) سبب إصدار الإشعار يصل XML. النطاق يفرضه ويحفظه — وكان يضيع في التوليد.
    /// </summary>
    [Fact]
    public void CreditNote_CarriesItsIssueReason()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var doc = Build(Data(isCreditNote: true, billingReference: "INV-1", issueReason: "مرتجع بضاعة تالفة"));
        var means = doc.Root!.Element(cac + "PaymentMeans");

        Assert.NotNull(means);
        Assert.Equal("مرتجع بضاعة تالفة", means!.Element(cbc + "InstructionNote")?.Value);
    }

    /// <summary>وإشعارٌ بلا سبب يُرفض — الإلزام محروس لا موثَّق فقط.</summary>
    [Fact]
    public void CreditNoteWithoutReason_IsRejected()
    {
        var violations = UblStructureValidator.Validate(
            Build(Data(isCreditNote: true, billingReference: "INV-1", issueReason: null)));

        Assert.Contains(violations, v => v.Contains("سبب إصدار"));
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> الفاتورة العادية لا تحمل سطر السبب — فالإضافة لم تُدخل عنصراً
    /// دخيلاً على كل مستند.
    /// </summary>
    [Fact]
    public void PlainInvoice_HasNoPaymentMeansLine()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        Assert.Null(Build(Data()).Root!.Element(cac + "PaymentMeans"));
    }
}
