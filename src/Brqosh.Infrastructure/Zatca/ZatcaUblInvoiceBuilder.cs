using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>بناء UBL 2.1 XML للفاتورة الضريبية — متوافق مع ZATCA (reporting:1.0).</summary>
public static class ZatcaUblInvoiceBuilder
{
    private static readonly XNamespace Ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public record InvoiceData(
        string InvoiceNumber,
        Guid Uuid,
        DateOnly InvoiceDate,
        TimeOnly IssueTime,
        string SellerName,
        string SellerVat,
        string? CustomerName,
        string? CustomerVat,
        decimal Subtotal,
        decimal VatAmount,
        decimal GrandTotal,
        IReadOnlyList<SalesInvoiceLine> Lines,
        string Icv,
        string PreviousInvoiceHash,
        string QrTlvBase64,
        bool IsCreditNote = false,
        bool IsDebitNote = false,
        bool IsSimplified = true,
        string? BillingReferenceInvoiceNumber = null,
        Guid? BillingReferenceUuid = null,
        string? IssueReason = null,
        SellerIdentity? Seller = null,
        BuyerIdentity? Buyer = null,

        /// <summary>
        /// (CEEF-230 · ط1-٢) خصمُ المستند — يخرج <c>AllowanceCharge</c> لكلّ وعاءٍ ضريبيّ.
        /// و<c>Subtotal</c> أعلاه <b>بعده</b>، فـ<c>LineExtensionAmount</c> يساوي مجموعَهما.
        /// </summary>
        decimal DocumentDiscount = 0m);

/// <summary>
/// (CEEF-229 · Z-10) هوية البائع النظامية — السجل التجاريّ والاسم المسجَّل والعنوان الوطنيّ.
/// كانت غائبة عن العقد كلياً، فيخرج طرف البائع باسمٍ ورقمٍ ضريبيّ فقط: مستندٌ يسقط في
/// تصديق المخطّط قبل أن تُقرأ أرقامه.
/// </summary>
public sealed record SellerIdentity(
    string? RegistrationName,
    string? CommercialRegistrationNumber,
    Brqosh.Domain.Companies.NationalAddress Address);

/// <summary>
/// (CEEF-229 · م1-١ب) هويةُ المشتري النظامية — للفاتورة <b>القياسية</b> وحدها.
///
/// <para>ولا رقمَ سجلٍّ تجاريّ فيها: معرّفُ المشتري هو رقمُه الضريبيّ. أمّا المبسّطة (B2C)
/// فلا مشتريَ معرَّفاً فيها أصلاً — والكاشيرُ لا يسأل الزبونَ عن حيّه.</para>
/// </summary>
public sealed record BuyerIdentity(
    string? RegistrationName,
    Brqosh.Domain.Companies.NationalAddress Address);

    public static byte[] BuildXml(InvoiceData data)
    {
        var typeCode = data.IsDebitNote ? "383" : data.IsCreditNote ? "381" : "388";
        var typeName = data.IsSimplified ? "0200000" : "0100000";

        var root = new XElement(Ubl + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            El(Cbc, "ProfileID", "reporting:1.0"),
            El(Cbc, "ID", data.InvoiceNumber),
            El(Cbc, "UUID", data.Uuid.ToString()),
            El(Cbc, "IssueDate", data.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            El(Cbc, "IssueTime", data.IssueTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            InvoiceTypeCodeEl(typeCode, typeName),
            El(Cbc, "DocumentCurrencyCode", "SAR"),
            El(Cbc, "TaxCurrencyCode", "SAR"));

        // (CEEF-229 · Z-6) ترتيب `xsd:sequence` في مخطّط UBL 2.1 مُلزِم — لا اصطلاح.
        //
        // متتالية Invoice تضع `cac:BillingReference` **قبل** `cac:AdditionalDocumentReference`،
        // ثم `cac:Signature` بعدهما وقبل طرفَي البائع والمشتري. وكان الترتيب معكوساً والتوقيع
        // غائباً — فيُرفض المستند في **تصديق المخطّط**، أي قبل أن تُقرأ أرقامه أو يُفحص توقيعه.
        // لا حيلة تشغيلية تُنجّي منه.
        if ((data.IsCreditNote || data.IsDebitNote) &&
            !string.IsNullOrWhiteSpace(data.BillingReferenceInvoiceNumber))
            AddBillingReference(root, data.BillingReferenceInvoiceNumber!, data.BillingReferenceUuid);

        AddIcvRef(root, data.Icv);
        AddDocRef(root, "PIH", data.PreviousInvoiceHash);
        AddDocRef(root, "QR", data.QrTlvBase64);

        AddUblSignature(root);

        AddSupplier(root, data.SellerName, data.SellerVat, data.Seller);
        if (!string.IsNullOrWhiteSpace(data.CustomerName) || !string.IsNullOrWhiteSpace(data.CustomerVat))
            AddCustomer(root, data.CustomerName, data.CustomerVat, data.Buyer);

        AddIssueReason(root, data);

        // cac:TaxSubtotal إلزاميّ لدى الهيئة (BR-CO-18/KSA): وعاء الضريبة + فئتها + نسبتها.
        // كان غائباً تماماً فتُرفض الفاتورة في الفحص رغم صحة التوقيع (ع-Z2، CEEF-202).
        //
        // (CEEF-207) وعاء لكل فئة ضريبية حاضرة في الفاتورة، لا وعاء واحد بنسبة مسمّرة:
        // فاتورة فيها سطر أساسيّ وآخر صفريّ تُنتج وعاءين — والنسبة المعلنة في كل وعاء
        // تطابق مبلغ ضريبته، فينتفي التناقض الذي كان يرفضه المدقّق (صفر مصنّف بنسبة 15).
        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(
            data.Lines, documentDiscount: data.DocumentDiscount);
        var taxTotal = new XElement(Cac + "TaxTotal",
            El(Cbc, "TaxAmount", F(data.VatAmount), ("currencyID", "SAR")));

        foreach (var sub in subtotals)
        {
            taxTotal.Add(new XElement(Cac + "TaxSubtotal",
                El(Cbc, "TaxableAmount", F(sub.TaxableAmount), ("currencyID", "SAR")),
                El(Cbc, "TaxAmount", F(sub.VatAmount), ("currencyID", "SAR")),
                new XElement(Cac + "TaxCategory",
                    El(Cbc, "ID", sub.Category.UblCode()),
                    El(Cbc, "Percent", (sub.Rate * 100m).ToString("0.00", CultureInfo.InvariantCulture)),
                    // (CEEF-229 · م1-١ب) BR-KSA-CL-06: فئةٌ غير أساسية بلا سبب إعفاء تُرفض.
                    // والترتيبُ مُلزِم: ID ثمّ Percent ثمّ الرمز ثمّ النصّ ثمّ TaxScheme.
                    sub.Category == VatCategory.Standard || sub.ExemptionReasonCode is null
                        ? null
                        : El(Cbc, "TaxExemptionReasonCode", sub.ExemptionReasonCode),
                    sub.Category == VatCategory.Standard || sub.ExemptionReasonText is null
                        ? null
                        : El(Cbc, "TaxExemptionReason", sub.ExemptionReasonText),
                    new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))));
        }

        // (CEEF-230 · ط1-٢) ‏**خصمُ المستند: عنصرٌ لكلّ وعاءٍ ضريبيّ لا عنصرٌ واحد.**
        //
        // ‏لأنّ `cac:AllowanceCharge` على مستوى المستند يحمل `cac:TaxCategory` — فخصمٌ
        // ‏واحدٌ على فاتورةٍ فيها وعاءان لا يستطيع أن يُعلن فئتَه. **والتوزيعُ يصير
        // ‏مقروءاً في المستند** لا مستنتَجاً من فرق الأرقام. وموضعُه قبل `TaxTotal`
        // ‏بحكم متتالية المخطّط.
        foreach (var (category, rate, amount) in
                 LineDocumentTotalsCalculator.ComputeDiscountAllocation(
                     data.Lines, data.DocumentDiscount))
        {
            root.Add(new XElement(Cac + "AllowanceCharge",
                El(Cbc, "ChargeIndicator", "false"),
                El(Cbc, "AllowanceChargeReason", "خصم على إجمالي المستند"),
                El(Cbc, "Amount", F(amount), ("currencyID", "SAR")),
                new XElement(Cac + "TaxCategory",
                    El(Cbc, "ID", category.UblCode()),
                    El(Cbc, "Percent", (rate * 100m).ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))));
        }

        root.Add(taxTotal);

        // ‏`LineExtensionAmount` **قبل** الخصم و`TaxExclusiveAmount` بعده — والفرقُ بينهما
        // ‏هو `AllowanceTotalAmount` بعينه. وهذه المعادلةُ يفرضها المدقّق.
        var lineExtension = data.Subtotal + data.DocumentDiscount;
        var monetary = new XElement(Cac + "LegalMonetaryTotal",
            El(Cbc, "LineExtensionAmount", F(lineExtension), ("currencyID", "SAR")),
            El(Cbc, "TaxExclusiveAmount", F(data.Subtotal), ("currencyID", "SAR")),
            El(Cbc, "TaxInclusiveAmount", F(data.GrandTotal), ("currencyID", "SAR")),
            data.DocumentDiscount > 0m
                ? El(Cbc, "AllowanceTotalAmount", F(data.DocumentDiscount), ("currencyID", "SAR"))
                : null,
            El(Cbc, "PayableAmount", F(data.GrandTotal), ("currencyID", "SAR")));
        root.Add(monetary);

        var idx = 1;
        foreach (var line in data.Lines)
        {
            var lineNet = line.LineTotal;
            // (CEEF-207) نسبة السطر من فئته: الصفريّ والمعفى ينتجان ضريبة صفراً ونسبة 0.00،
            // بدل ضرب كل سطر 15% ثم تصنيفه "O" بنسبة معلنة 15.00 — تناقض يرفضه المدقّق.
            var lineRate = line.VatCategory.RateFor(SalesInvoice.SaudiVatRate);
            var lineVat = Math.Round(lineNet * lineRate, 2, MidpointRounding.AwayFromZero);
            var invLine = new XElement(Cac + "InvoiceLine",
                El(Cbc, "ID", idx.ToString()),
                El(Cbc, "InvoicedQuantity", F(line.Quantity), ("unitCode", "PCE")),
                El(Cbc, "LineExtensionAmount", F(lineNet), ("currencyID", "SAR")),

                // (CEEF-230 · ط1-٢) ‏خصمُ السطر — و`BaseAmount` هو الإجماليُّ قبله، فيُقرأ
                // ‏السعرُ المعلَن والخصمُ معاً بدل سعرٍ مخفَّضٍ يُخفي أنّ خصماً وقع.
                line.DiscountAmount > 0m
                    ? new XElement(Cac + "AllowanceCharge",
                        El(Cbc, "ChargeIndicator", "false"),
                        El(Cbc, "AllowanceChargeReason", "خصم على السطر"),
                        El(Cbc, "MultiplierFactorNumeric",
                            line.DiscountPercent.ToString("0.00", CultureInfo.InvariantCulture)),
                        El(Cbc, "Amount", F(line.DiscountAmount), ("currencyID", "SAR")),
                        El(Cbc, "BaseAmount", F(line.GrossAmount), ("currencyID", "SAR")))
                    : null,

                new XElement(Cac + "TaxTotal", El(Cbc, "TaxAmount", F(lineVat), ("currencyID", "SAR"))),
                new XElement(Cac + "Item",
                    El(Cbc, "Name", line.Description),
                    new XElement(Cac + "ClassifiedTaxCategory",
                        El(Cbc, "ID", line.VatCategory.UblCode()),
                        El(Cbc, "Percent", (lineRate * 100m).ToString("0.00", CultureInfo.InvariantCulture)),
                        new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))),
                new XElement(Cac + "Price", El(Cbc, "PriceAmount", F(line.UnitPrice), ("currencyID", "SAR"))));
            root.Add(invLine);
            idx++;
        }

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);
        return ms.ToArray();
    }

    private static XElement InvoiceTypeCodeEl(string code, string name) =>
        new(Cbc + "InvoiceTypeCode", new XAttribute("name", name), code);

    private static string F(decimal v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static XElement El(XNamespace ns, string name, object value, params (string Key, string Val)[] attrs)
    {
        var el = new XElement(ns + name, value);
        foreach (var (k, v) in attrs) el.SetAttributeValue(k, v);
        return el;
    }

    /// <summary>
    /// (CEEF-229 · Z-5) سبب إصدار الإشعار الدائن/المدين.
    ///
    /// <para>النطاق يفرض السبب ويحفظه: <c>CreditNote.Reason</c> إلزاميّ و<c>CreateDraft</c> يرفض
    /// الفارغ، والعمود <c>varchar(512) NOT NULL</c>. ثم لا يصل XML إطلاقاً — لا العقد يحمله ولا
    /// المولّد يمرّره. فمعلومةٌ إلزامية <b>موجودة في قاعدة البيانات</b> تضيع في التوليد؛ الإصلاح
    /// تمريرُ حقلٍ لا استحداثُ بيانات.</para>
    ///
    /// <para><b>حدُّ ثقة مُصرَّح به:</b> إلزام بيان السبب مؤكَّد. أمّا كتابته في
    /// <c>cac:PaymentMeans/cbc:InstructionNote</c> فهو الموضع الشائع في أمثلة الهيئة و
    /// SDK — <b>ولم أتحقّق منه في نصّ المواصفة</b>. يُراجَع نصّاً قبل الإرسال الحقيقيّ (م4-٤).
    /// وموضعه في المتتالية بعد طرفَي البائع والمشتري وقبل <c>cac:TaxTotal</c> — يحرسه ح-٣.</para>
    /// </summary>
    private static void AddIssueReason(XElement root, InvoiceData data)
    {
        if (!data.IsCreditNote && !data.IsDebitNote)
            return;
        if (string.IsNullOrWhiteSpace(data.IssueReason))
            return;

        root.Add(new XElement(Cac + "PaymentMeans",
            El(Cbc, "PaymentMeansCode", "10"),
            El(Cbc, "InstructionNote", data.IssueReason.Trim())));
    }

    /// <summary>
    /// (CEEF-229 · Z-6) العدّاد التسلسليّ يُكتب <c>cbc:UUID</c> مباشرةً تحت المرجع الإضافيّ.
    ///
    /// كان يُكتب مرفقاً ثنائياً (<c>EmbeddedDocumentBinaryObject</c> بـ<c>filename="icv.txt"</c>)
    /// كما يُكتب PIH وQR — لكنّ المرفق مخصَّص لهذين وحدهما، والعدّاد قيمةٌ نصّية لا مستند مرفق.
    /// </summary>
    private static void AddIcvRef(XElement root, string icv)
    {
        root.Add(new XElement(Cac + "AdditionalDocumentReference",
            El(Cbc, "ID", "ICV"),
            El(Cbc, "UUID", icv)));
    }

    /// <summary>
    /// (CEEF-229 · Z-6) <c>cac:Signature</c> في جسد الفاتورة — كان غائباً تماماً.
    ///
    /// المخطّط يفرض إعلان التوقيع داخل المستند، مستقلاً عن كتلة <c>ds:Signature</c> التي
    /// تُغرَس في <c>ext:UBLExtensions</c>. غيابُه يُسقط المستند في تصديق المخطّط.
    /// </summary>
    private static void AddUblSignature(XElement root)
    {
        root.Add(new XElement(Cac + "Signature",
            El(Cbc, "ID", "urn:oasis:names:specification:ubl:signature:Invoice"),
            El(Cbc, "SignatureMethod", "urn:oasis:names:specification:ubl:dsig:enveloped:xades")));
    }

    private static void AddDocRef(XElement root, string id, string value)
    {
        var reference = new XElement(Cac + "AdditionalDocumentReference", El(Cbc, "ID", id));
        if (!string.IsNullOrEmpty(value))
        {
            var attachment = new XElement(Cac + "Attachment",
                new XElement(Cbc + "EmbeddedDocumentBinaryObject",
                    new XAttribute("mimeCode", "text/plain"),
                    new XAttribute("filename", id.ToLowerInvariant() + ".txt"),
                    value));
            reference.Add(attachment);
        }
        root.Add(reference);
    }

    private static void AddBillingReference(XElement root, string invoiceNumber, Guid? uuid)
    {
        var docRef = new XElement(Cac + "BillingReference",
            new XElement(Cac + "InvoiceDocumentReference", El(Cbc, "ID", invoiceNumber)));
        if (uuid is Guid g)
            docRef.Element(Cac + "InvoiceDocumentReference")!.Add(El(Cbc, "UUID", g.ToString()));
        root.Add(docRef);
    }

    /// <summary>
    /// (CEEF-229 · Z-10) طرف البائع كاملاً: السجل التجاريّ ثم الاسم ثم العنوان الوطنيّ ثم
    /// الرقم الضريبيّ ثم الاسم المسجَّل — <b>بهذا الترتيب</b>، فمتتالية <c>cac:Party</c> تفرضه.
    ///
    /// كان يخرج باسمٍ ورقمٍ ضريبيّ فقط. والحقول الناقصة تُتخطّى بدل أن تُكتب فارغة: عنصرٌ
    /// إلزاميّ بقيمة فارغة يُرفض كما يُرفض غيابه، والغيابُ يقوله المُصدِّق بوضوح.
    /// </summary>
    private static void AddSupplier(XElement root, string name, string vat, SellerIdentity? seller)
    {
        var party = new XElement(Cac + "Party");

        if (!string.IsNullOrWhiteSpace(seller?.CommercialRegistrationNumber))
        {
            party.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID",
                    new XAttribute("schemeID", "CRN"),
                    seller.CommercialRegistrationNumber)));
        }

        party.Add(new XElement(Cac + "PartyName", El(Cbc, "Name", name)));

        if (seller?.Address is { IsComplete: true } address)
            party.Add(PostalAddress(address));

        party.Add(PartyTax(vat));

        if (!string.IsNullOrWhiteSpace(seller?.RegistrationName))
        {
            party.Add(new XElement(Cac + "PartyLegalEntity",
                El(Cbc, "RegistrationName", seller.RegistrationName)));
        }

        root.Add(new XElement(Cac + "AccountingSupplierParty", party));
    }

    /// <summary>العنوان الوطنيّ بترتيب متتالية <c>cac:PostalAddress</c>.</summary>
    private static XElement PostalAddress(Brqosh.Domain.Companies.NationalAddress a)
    {
        var address = new XElement(Cac + "PostalAddress",
            El(Cbc, "StreetName", a.StreetName),
            El(Cbc, "BuildingNumber", a.BuildingNumber));

        if (!string.IsNullOrWhiteSpace(a.AdditionalNumber))
            address.Add(El(Cbc, "PlotIdentification", a.AdditionalNumber));

        address.Add(
            El(Cbc, "CitySubdivisionName", a.DistrictName),
            El(Cbc, "CityName", a.CityName),
            El(Cbc, "PostalZone", a.PostalZone),
            El(Cbc, "CountrySubentity", a.CountrySubentity),
            new XElement(Cac + "Country", El(Cbc, "IdentificationCode", a.CountryCode)));

        return address;
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب) طرفُ المشتري — وترتيبُ عناصره يتبع المتتالية كما في طرف البائع.
    ///
    /// <para>كان يخرج باسمٍ ورقمٍ ضريبيّ فقط، بينما طرفُ البائع أخذ سجلَّه واسمَه المسجَّل
    /// وعنوانَه الوطنيّ في Z-10. <b>ولم يكشف اللاتماثلَ شيء</b> لأنّ المُصدِّق نفسه كان يفحص
    /// البائعَ فحصاً كاملاً ولا يفحص المشتري إطلاقاً.</para>
    ///
    /// <para>والحقولُ الناقصة تُتخطّى بدل أن تُكتب فارغة: عنصرٌ إلزاميّ بقيمة فارغة يُرفض كما
    /// يُرفض غيابه، والغيابُ يقوله المُصدِّق بوضوح.</para>
    /// </summary>
    private static void AddCustomer(XElement root, string? name, string? vat, BuyerIdentity? buyer)
    {
        var partyEl = new XElement(Cac + "Party");

        if (!string.IsNullOrWhiteSpace(name))
            partyEl.Add(new XElement(Cac + "PartyName", El(Cbc, "Name", name)));

        if (buyer?.Address is { IsComplete: true } address)
            partyEl.Add(PostalAddress(address));

        if (!string.IsNullOrWhiteSpace(vat))
            partyEl.Add(PartyTax(vat));

        if (!string.IsNullOrWhiteSpace(buyer?.RegistrationName))
        {
            partyEl.Add(new XElement(Cac + "PartyLegalEntity",
                El(Cbc, "RegistrationName", buyer.RegistrationName)));
        }

        root.Add(new XElement(Cac + "AccountingCustomerParty", partyEl));
    }

    private static XElement PartyTax(string vat) =>
        new(Cac + "PartyTaxScheme",
            El(Cbc, "CompanyID", vat),
            new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")));
}
