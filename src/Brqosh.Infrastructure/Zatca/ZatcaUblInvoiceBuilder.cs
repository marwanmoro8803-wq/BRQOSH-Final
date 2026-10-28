using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
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
        /// خصمُ المستند — يخرج <c>AllowanceCharge</c> لكلّ وعاءٍ ضريبيّ.
        /// و<c>Subtotal</c> أعلاه <b>بعده</b>، فـ<c>LineExtensionAmount</c> يساوي مجموعَهما.
        /// </summary>
        decimal DocumentDiscount = 0m,

        /// <summary>
        /// رمزُ UN/ECE لكلّ سطر، بترتيب <c>Lines</c> نفسِه.
        ///
        /// <para><b>وكان ثابتاً <c>PCE</c> لكلّ سطرٍ في النظام</b> — فبيعُ خمسة كيلوجرامات
        /// يُبلَّغ للهيئة خمسَ قطع. <b>تصريحٌ مخالفٌ للواقع يمرّ على كلّ تصديقٍ بنيويّ</b>،
        /// لأنّ المخطّط يفحص وجودَ الرمز لا صدقَه.</para>
        /// </summary>
        IReadOnlyList<string>? LineUnitCodes = null,

        /// <summary> عملةُ المستند — والمبالغُ أعلاه بها.</summary>
        string CurrencyCode = "SAR",

        /// <summary>سعرُ الصرف إلى الريال يوم المستند.</summary>
        decimal ExchangeRate = 1m,

        /// <summary>
        /// الضريبةُ <b>بالريال</b> — وتُعلَن في <c>TaxTotal</c> ثانٍ حين تختلف العملتان
        /// (BT-111). <b>وهو إفصاحٌ تفرضه الهيئة</b>: الضريبةُ تُورَّد بالريال أيّاً كانت
        /// عملةُ الفاتورة.
        /// </summary>
        decimal? VatAmountInSar = null,

        /// <summary>
        /// <b>تاريخُ التوريد</b> — <c>cac:Delivery/cbc:ActualDeliveryDate</c>،
        /// وتشترطه الهيئةُ في الفاتورة الضريبيّة (BR-KSA-11).
        ///
        /// <para><b>و<c>null</c> تعني «يوم الفاتورة»</b>، وهو الأصلُ في بيعٍ يقع عند الإصدار —
        /// وأكثرُ فواتير المنشآت الصغيرة كذلك. <b>ولا يُخترع تاريخٌ آخر</b>.</para>
        ///
        /// <para><b>وحين يُفصَل التوريدُ عن الإصدار</b> — بضاعةٌ سُلّمت في شهرٍ وفُوتِرت في
        /// تاليه — <b>يلزم أن يُمرَّر التاريخُ الحقيقيّ</b>، فهو الذي يُحدّد فترةَ الإقرار.
        /// والمستندُ اليومَ لا يحمل حقلاً له: <b>بندٌ مفتوحٌ مُسمّى، لا صمتٌ عنه</b>.</para>
        /// </summary>
        DateOnly? SupplyDate = null,
        bool IsPrepayment = false,
        IReadOnlyList<PrepaymentReference>? Prepayments = null);

    /// <summary>
    /// فاتورةُ دفعةٍ مقدّمةٍ (386) تُسوّى في الفاتورة النهائيّة — كما في معيار
    /// XML (الإصدار 1.2، §9.5): رقمُها وتاريخُها ووقتُها (KSA-26/28/29) ووعاؤها وضريبتُها (KSA-31/32) بفئتها (KSA-33/34).
    /// </summary>
    public sealed record PrepaymentReference(
        string Id, DateOnly IssueDate, TimeOnly IssueTime, decimal TaxableAmount, decimal VatAmount);

/// <summary>
/// هوية البائع النظامية — السجل التجاريّ والاسم المسجَّل والعنوان الوطنيّ.
/// كانت غائبة عن العقد كلياً، فيخرج طرف البائع باسمٍ ورقمٍ ضريبيّ فقط: مستندٌ يسقط في
/// تصديق المخطّط قبل أن تُقرأ أرقامه.
/// </summary>
public sealed record SellerIdentity(
    string? RegistrationName,
    string? CommercialRegistrationNumber,
    Brqosh.Domain.Companies.NationalAddress Address);

/// <summary>
/// هويةُ المشتري النظامية — للفاتورة <b>القياسية</b> وحدها.
///
/// <para>ولا رقمَ سجلٍّ تجاريّ فيها: معرّفُ المشتري هو رقمُه الضريبيّ. أمّا المبسّطة (B2C)
/// فلا مشتريَ معرَّفاً فيها أصلاً — والكاشيرُ لا يسأل الزبونَ عن حيّه.</para>
/// </summary>
public sealed record BuyerIdentity(
    string? RegistrationName,
    Brqosh.Domain.Companies.NationalAddress Address);

    public static byte[] BuildXml(InvoiceData data)
    {
        // ‏(sector-modules#6) ‏386 فاتورةُ الدفعة المقدّمة — تُصدر يومَ قبضها، فهي نقطةُ استحقاق الضريبة.
        var typeCode = data.IsDebitNote ? "383" : data.IsCreditNote ? "381" : data.IsPrepayment ? "386" : "388";
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
            // ‏عملةُ المستند تتغيّر، **وعملةُ الضريبة ريالٌ أبداً** —
            // ‏فالضريبةُ تُورَّد بالريال أيّاً كانت عملةُ الفاتورة (BR-KSA-EN16931-09).
            El(Cbc, "DocumentCurrencyCode", data.CurrencyCode),
            El(Cbc, "TaxCurrencyCode", "SAR"));

        // ترتيب `xsd:sequence` في مخطّط UBL 2.1 مُلزِم — لا اصطلاح.
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

        // ‏ ‏**تاريخُ التوريد — وكان غائباً عن المستند كلِّه.**
        //
        // ‏الهيئةُ تشترط `cac:Delivery/cbc:ActualDeliveryDate` في الفاتورة الضريبيّة
        // ‏(BR-KSA-11)، **وهو يُحدّد الفترةَ التي تُقرَّر فيها الضريبة** — لا بيانٌ وصفيّ.
        //
        // ‏**وموضعُه في المتتالية مُلزِم**: `cac:Delivery` **قبل** `cac:PaymentMeans` الذي
        // ‏يكتبه `AddIssueReason` أدناه. ووضعُه بعده يُرفض في تصديق المخطّط — أي قبل أن
        // ‏تُقرأ أرقامُه، كما وقع في Z-6.
        AddDelivery(root, data);

        AddIssueReason(root, data);

        // cac:TaxSubtotal إلزاميّ لدى الهيئة (BR-CO-18/KSA): وعاء الضريبة + فئتها + نسبتها.
        // كان غائباً تماماً فتُرفض الفاتورة في الفحص رغم صحة التوقيع .
        //
        // وعاء لكل فئة ضريبية حاضرة في الفاتورة، لا وعاء واحد بنسبة مسمّرة:
        // فاتورة فيها سطر أساسيّ وآخر صفريّ تُنتج وعاءين — والنسبة المعلنة في كل وعاء
        // تطابق مبلغ ضريبته، فينتفي التناقض الذي كان يرفضه المدقّق (صفر مصنّف بنسبة 15).
        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(
            data.Lines, documentDiscount: data.DocumentDiscount);
        var taxTotal = new XElement(Cac + "TaxTotal",
            El(Cbc, "TaxAmount", F(data.VatAmount), ("currencyID", data.CurrencyCode)));

        foreach (var sub in subtotals)
        {
            taxTotal.Add(new XElement(Cac + "TaxSubtotal",
                El(Cbc, "TaxableAmount", F(sub.TaxableAmount), ("currencyID", data.CurrencyCode)),
                El(Cbc, "TaxAmount", F(sub.VatAmount), ("currencyID", data.CurrencyCode)),
                new XElement(Cac + "TaxCategory",
                    El(Cbc, "ID", sub.Category.UblCode()),
                    El(Cbc, "Percent", (sub.Rate * 100m).ToString("0.00", CultureInfo.InvariantCulture)),
                    // BR-KSA-CL-06: فئةٌ غير أساسية بلا سبب إعفاء تُرفض.
                    // والترتيبُ مُلزِم: ID ثمّ Percent ثمّ الرمز ثمّ النصّ ثمّ TaxScheme.
                    sub.Category == VatCategory.Standard || sub.ExemptionReasonCode is null
                        ? null
                        : El(Cbc, "TaxExemptionReasonCode", sub.ExemptionReasonCode),
                    sub.Category == VatCategory.Standard || sub.ExemptionReasonText is null
                        ? null
                        : El(Cbc, "TaxExemptionReason", sub.ExemptionReasonText),
                    new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))));
        }

        // ‏**خصمُ المستند: عنصرٌ لكلّ وعاءٍ ضريبيّ لا عنصرٌ واحد.**
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
                El(Cbc, "Amount", F(amount), ("currencyID", data.CurrencyCode)),
                new XElement(Cac + "TaxCategory",
                    El(Cbc, "ID", category.UblCode()),
                    El(Cbc, "Percent", (rate * 100m).ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))));
        }

        // ‏سعرُ الصرف ثمّ ضريبةٌ ثانيةٌ بالريال — **ولا يخرجان إلا عند
        // ‏اختلاف العملتين**. فمستندٌ بالريال يبقى حرفاً كما كان، ولا تتغيّر بصمةُ فاتورةٍ
        // ‏قائمة.
        if (data.CurrencyCode != "SAR")
        {
            root.Add(new XElement(Cac + "TaxExchangeRate",
                El(Cbc, "SourceCurrencyCode", data.CurrencyCode),
                El(Cbc, "TargetCurrencyCode", "SAR"),
                El(Cbc, "CalculationRate",
                    data.ExchangeRate.ToString("0.000000", CultureInfo.InvariantCulture))));
        }

        root.Add(taxTotal);

        if (data.CurrencyCode != "SAR")
        {
            // ‏**عنصرٌ فيه TaxAmount وحدَه** — لا أوعية: هو إفصاحُ المبلغ بعملة التوريد
            // ‏(BT-111)، لا تفصيلٌ ثانٍ للوعاء. وإضافةُ أوعيةٍ إليه تُخرج مستنداً
            // ‏يُضاعف الضريبةَ في عين المُصدِّق.
            root.Add(new XElement(Cac + "TaxTotal",
                El(Cbc, "TaxAmount",
                    F(data.VatAmountInSar ?? Math.Round(data.VatAmount * data.ExchangeRate, 2,
                        MidpointRounding.AwayFromZero)),
                    ("currencyID", "SAR"))));
        }

        // ‏`LineExtensionAmount` **قبل** الخصم و`TaxExclusiveAmount` بعده — والفرقُ بينهما
        // ‏هو `AllowanceTotalAmount` بعينه. وهذه المعادلةُ يفرضها المدقّق.
        var lineExtension = data.Subtotal + data.DocumentDiscount;

        // ‏(sector-modules#6) ‏**والدفعةُ المقدّمةُ تُسوّى في المستحقّ لا في التوريد** (§9.5): `TaxInclusiveAmount` الإقامةُ
        // ‏كلُّها بضريبتها، و`PrepaidAmount` = Σ(KSA-31 + KSA-32)، و`PayableAmount` الفرق.
        var prepaid = data.Prepayments?.Sum(p => p.TaxableAmount + p.VatAmount) ?? 0m;
        var monetary = new XElement(Cac + "LegalMonetaryTotal",
            El(Cbc, "LineExtensionAmount", F(lineExtension), ("currencyID", data.CurrencyCode)),
            El(Cbc, "TaxExclusiveAmount", F(data.Subtotal), ("currencyID", data.CurrencyCode)),
            El(Cbc, "TaxInclusiveAmount", F(data.GrandTotal), ("currencyID", data.CurrencyCode)),
            data.DocumentDiscount > 0m
                ? El(Cbc, "AllowanceTotalAmount", F(data.DocumentDiscount), ("currencyID", data.CurrencyCode))
                : null,
            prepaid > 0m ? El(Cbc, "PrepaidAmount", F(prepaid), ("currencyID", data.CurrencyCode)) : null,
            El(Cbc, "PayableAmount", F(data.GrandTotal - prepaid), ("currencyID", data.CurrencyCode)));
        root.Add(monetary);

        var idx = 1;
        foreach (var line in data.Lines)
        {
            var lineNet = line.LineTotal;
            // نسبة السطر من فئته: الصفريّ والمعفى ينتجان ضريبة صفراً ونسبة 0.00،
            // بدل ضرب كل سطر 15% ثم تصنيفه "O" بنسبة معلنة 15.00 — تناقض يرفضه المدقّق.
            var lineRate = line.VatCategory.RateFor(SalesInvoice.SaudiVatRate);
            var lineVat = Math.Round(lineNet * lineRate, 2, MidpointRounding.AwayFromZero);
            var invLine = new XElement(Cac + "InvoiceLine",
                El(Cbc, "ID", idx.ToString()),
                El(Cbc, "InvoicedQuantity", F(line.Quantity),
                    ("unitCode", data.LineUnitCodes is { } codes && idx - 1 < codes.Count
                        ? codes[idx - 1]
                        : UnitCatalog.FallbackUnCode)),
                El(Cbc, "LineExtensionAmount", F(lineNet), ("currencyID", data.CurrencyCode)),

                // ‏خصمُ السطر — و`BaseAmount` هو الإجماليُّ قبله، فيُقرأ
                // ‏السعرُ المعلَن والخصمُ معاً بدل سعرٍ مخفَّضٍ يُخفي أنّ خصماً وقع.
                line.DiscountAmount > 0m
                    ? new XElement(Cac + "AllowanceCharge",
                        El(Cbc, "ChargeIndicator", "false"),
                        El(Cbc, "AllowanceChargeReason", "خصم على السطر"),
                        El(Cbc, "MultiplierFactorNumeric",
                            line.DiscountPercent.ToString("0.00", CultureInfo.InvariantCulture)),
                        El(Cbc, "Amount", F(line.DiscountAmount), ("currencyID", data.CurrencyCode)),
                        El(Cbc, "BaseAmount", F(line.GrossAmount), ("currencyID", data.CurrencyCode)))
                    : null,

                // ‏ ‏**ومبلغُ السطر شاملَ الضريبة** — وكان الوعاءُ
                // ‏يحمل الضريبةَ وحدَها. تشترطه الهيئةُ في كلّ سطر (BR-KSA-EN16931-11)
                // ‏بقيمةٍ محدّدة: `LineExtensionAmount + TaxAmount`. **فليس حقلاً وصفيّاً
                // ‏يُملأ بأيّ رقم**: قيمةٌ تخالف المجموعَ تُرفض كما يُرفض غيابُه.
                new XElement(Cac + "TaxTotal",
                    El(Cbc, "TaxAmount", F(lineVat), ("currencyID", data.CurrencyCode)),
                    El(Cbc, "RoundingAmount", F(lineNet + lineVat), ("currencyID", data.CurrencyCode))),
                new XElement(Cac + "Item",
                    El(Cbc, "Name", line.Description),
                    new XElement(Cac + "ClassifiedTaxCategory",
                        El(Cbc, "ID", line.VatCategory.UblCode()),
                        El(Cbc, "Percent", (lineRate * 100m).ToString("0.00", CultureInfo.InvariantCulture)),
                        new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))),
                new XElement(Cac + "Price", El(Cbc, "PriceAmount", F(line.UnitPrice), ("currencyID", data.CurrencyCode))));
            root.Add(invLine);
            idx++;
        }

        // ‏(sector-modules#6) ‏**سطرُ تسويةٍ لكلّ دفعةٍ مقدّمة** (§9.5): مبالغُ التوريد فيه أصفار، ومرجعُ فاتورة 386 بمعرّفها
        // ‏وتاريخها ووقتها، وتفصيلُ وعائها وضريبتها في `TaxSubtotal` بفئة S ونسبة 15.
        foreach (var p in data.Prepayments ?? [])
        {
            var rate = (SalesInvoice.SaudiVatRate * 100m).ToString("0.00", CultureInfo.InvariantCulture);
            root.Add(new XElement(Cac + "InvoiceLine",
                El(Cbc, "ID", idx.ToString()),
                El(Cbc, "InvoicedQuantity", "0.00", ("unitCode", "PCE")),
                El(Cbc, "LineExtensionAmount", "0.00", ("currencyID", data.CurrencyCode)),
                new XElement(Cac + "DocumentReference",
                    El(Cbc, "ID", p.Id),
                    El(Cbc, "IssueDate", p.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    El(Cbc, "IssueTime", p.IssueTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
                    El(Cbc, "DocumentTypeCode", "386")),
                new XElement(Cac + "TaxTotal",
                    El(Cbc, "TaxAmount", "0.00", ("currencyID", data.CurrencyCode)),
                    El(Cbc, "RoundingAmount", "0.00", ("currencyID", data.CurrencyCode)),
                    new XElement(Cac + "TaxSubtotal",
                        El(Cbc, "TaxableAmount", F(p.TaxableAmount), ("currencyID", data.CurrencyCode)),
                        El(Cbc, "TaxAmount", F(p.VatAmount), ("currencyID", data.CurrencyCode)),
                        new XElement(Cac + "TaxCategory",
                            El(Cbc, "ID", "S"),
                            El(Cbc, "Percent", rate),
                            new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT"))))),
                new XElement(Cac + "Item",
                    El(Cbc, "Name", "Prepayment adjustment"),
                    new XElement(Cac + "ClassifiedTaxCategory",
                        El(Cbc, "ID", "S"),
                        El(Cbc, "Percent", rate),
                        new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))),
                new XElement(Cac + "Price", El(Cbc, "PriceAmount", "0.00", ("currencyID", data.CurrencyCode)))));
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
    /// سبب إصدار الإشعار الدائن/المدين.
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
    /// <summary>
    /// تاريخُ التوريد — <c>cac:Delivery/cbc:ActualDeliveryDate</c>.
    ///
    /// <para><b>ولا يُكتب في إشعارٍ دائنٍ أو مدين</b>: الإشعارُ يُصحّح فاتورةً سابقةً ولا
    /// يُوثّق توريداً جديداً، <b>ومرجعُه في <c>cac:BillingReference</c></b>.</para>
    /// </summary>
    private static void AddDelivery(XElement root, InvoiceData data)
    {
        if (data.IsCreditNote || data.IsDebitNote)
            return;

        var supply = data.SupplyDate ?? data.InvoiceDate;

        root.Add(new XElement(Cac + "Delivery",
            El(Cbc, "ActualDeliveryDate", supply.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
    }

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
    /// العدّاد التسلسليّ يُكتب <c>cbc:UUID</c> مباشرةً تحت المرجع الإضافيّ.
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
    /// <c>cac:Signature</c> في جسد الفاتورة — كان غائباً تماماً.
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
    /// طرف البائع كاملاً: السجل التجاريّ ثم الاسم ثم العنوان الوطنيّ ثم
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
    /// طرفُ المشتري — وترتيبُ عناصره يتبع المتتالية كما في طرف البائع.
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
