using System.Globalization;
﻿using System.Xml.Linq;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · ح-٣) مُصدِّق بنيويّ لفاتورة UBL 2.1 بامتدادات هيئة الزكاة والضريبة والجمارك.
///
/// <para><b>لماذا مُصدِّقٌ مكتوب لا XSD:</b> لا مخطّطات XSD في هذا المستودع، ولا يصحّ جلبُ حزمة
/// مخطّطات من الشبكة إلى مستودع عميل. فبديلُها ترميزُ القواعد التي تهمّ <b>صراحةً وبالعربية</b>:
/// متتالية <c>xsd:sequence</c>، والعناصر الإلزامية، وصيغة كل مرجع إضافيّ. المكسب أنه مقروء
/// ويقول <b>ما</b> انكسر لا «فشل التحقق»، والثمن أنه يغطّي ما رُمِّز فيه وحده — وهذا مُصرَّح به.</para>
///
/// <para><b>ما يمسكه فعلاً:</b> ثلاثة أعطال مؤكَّدة كانت تُسقط كل مستند في تصديق المخطّط
/// <b>قبل</b> أن يُفحص توقيعه: ترتيب <c>cac:BillingReference</c> بعد المراجع الإضافية،
/// وكتابة العدّاد مرفقاً بدل <c>cbc:UUID</c>، وغياب <c>cac:Signature</c>.</para>
///
/// <para><b>الاستعمال:</b> <c>Validate</c> تُعيد قائمة مخالفات — فارغةٌ تعني مطابقةً لما رُمِّز.</para>
/// </summary>
public static class UblStructureValidator
{
    private static readonly XNamespace Ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>
    /// متتالية أبناء <c>Invoice</c> كما يفرضها مخطّط UBL 2.1 — الترتيب مُلزِم لا اصطلاحيّ.
    /// أيّ عنصر غير مذكور هنا يُتجاوَز في فحص الترتيب (لا نمنع ما لا نعرف).
    /// </summary>
    private static readonly string[] ChildOrder =
    [
        "UBLExtensions",
        "UBLVersionID", "CustomizationID", "ProfileID", "ProfileExecutionID",
        "ID", "CopyIndicator", "UUID", "IssueDate", "IssueTime", "DueDate",
        "InvoiceTypeCode", "Note", "TaxPointDate",
        "DocumentCurrencyCode", "TaxCurrencyCode",
        "PricingCurrencyCode", "PaymentCurrencyCode", "PaymentAlternativeCurrencyCode",
        "AccountingCostCode", "AccountingCost", "LineCountNumeric", "BuyerReference",
        "InvoicePeriod", "OrderReference", "BillingReference",
        "DespatchDocumentReference", "ReceiptDocumentReference",
        "OriginatorDocumentReference", "ContractDocumentReference",
        "AdditionalDocumentReference", "ProjectReference", "Signature",
        "AccountingSupplierParty", "AccountingCustomerParty",
        "PayeeParty", "BuyerCustomerParty", "SellerSupplierParty",
        "TaxRepresentativeParty", "Delivery", "DeliveryTerms",
        "PaymentMeans", "PaymentTerms", "PrepaidPayment",
        "AllowanceCharge", "TaxExchangeRate", "PricingExchangeRate",
        "PaymentExchangeRate", "PaymentAlternativeExchangeRate",
        "TaxTotal", "WithholdingTaxTotal", "LegalMonetaryTotal", "InvoiceLine",
    ];

    /// <summary>عناصر لا تقوم الفاتورة بدونها.</summary>
    private static readonly (XNamespace Ns, string Name)[] Mandatory =
    [
        (Cbc, "ID"), (Cbc, "UUID"), (Cbc, "IssueDate"), (Cbc, "IssueTime"),
        (Cbc, "InvoiceTypeCode"), (Cbc, "DocumentCurrencyCode"), (Cbc, "TaxCurrencyCode"),
        (Cac, "Signature"), (Cac, "AccountingSupplierParty"),
        (Cac, "TaxTotal"), (Cac, "LegalMonetaryTotal"),
    ];

    /// <summary>يقرأ من البايتات مباشرةً — فمخرَج المولّد يحمل BOM ولا يقبله محلّل النصّ.</summary>
    public static IReadOnlyList<string> Validate(byte[] xml)
    {
        using var stream = new MemoryStream(xml);
        return Validate(XDocument.Load(stream));
    }

    public static IReadOnlyList<string> Validate(XDocument doc)
    {
        var violations = new List<string>();
        var root = doc.Root;

        if (root is null || root.Name != Ubl + "Invoice")
        {
            violations.Add($"الجذر ليس Invoice في فضاء UBL — وجد: {root?.Name.LocalName ?? "لا شيء"}");
            return violations;
        }

        CheckChildOrder(root, violations);
        CheckMandatory(root, violations);
        CheckInvoiceTypeCode(root, violations);
        CheckDocumentReferences(root, violations);
        CheckSignature(root, violations);
        CheckTaxTotals(root, violations);
        CheckLines(root, violations);
        CheckBillingReference(root, violations);
        CheckIssueReason(root, violations);
        CheckSupplierParty(root, violations);
        CheckCustomerParty(root, violations);
        CheckAllowances(root, violations);

        return violations;
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب) <b>طرفُ المشتري — للفاتورة القياسية وحدها.</b>
    ///
    /// <para><b>لماذا لم يكن مفحوصاً:</b> المُصدِّق كان يفحص طرفَ البائع فحصاً كاملاً
    /// (سجلٌّ تجاريّ، اسمٌ مسجَّل، عنوانٌ وطنيّ بستّة حقول) <b>ولا يفحص طرفَ المشتري إطلاقاً</b>.
    /// وهذا اللاتماثلُ هو الذي أخفى العطل: <c>AddCustomer</c> يكتب اسماً ورقماً ضريبياً
    /// وحدهما — بلا عنوان ولا سجلٍّ تجاريّ — و<c>Customer.Address</c> نصٌّ حرٌّ واحد لا يبلغ
    /// الباني أصلاً.</para>
    ///
    /// <para><b>ولماذا القياسية وحدها:</b> المبسّطة (B2C) تُرفع تقريراً بعد البيع ولا تحمل
    /// مشترياً معرَّفاً — فالكاشيرُ لا يسأل الزبونَ عن عنوانه. أمّا القياسية فتذهب إلى
    /// <b>المقاصّة قبل التسليم</b>، وطرفُ المشتري فيها معرَّف. والتمييزُ من سمة <c>name</c>
    /// على <c>InvoiceTypeCode</c>: تبدأ بـ<c>01</c> للقياسية و<c>02</c> للمبسّطة.</para>
    /// </summary>
    private static void CheckCustomerParty(XElement root, List<string> violations)
    {
        var typeName = root.Element(Cbc + "InvoiceTypeCode")?.Attribute("name")?.Value;
        if (typeName is null || !typeName.StartsWith("01", StringComparison.Ordinal))
            return;   // مبسّطة أو غير محدَّدة — لا يُحكَم على مشتريها

        var party = root.Element(Cac + "AccountingCustomerParty")?.Element(Cac + "Party");
        if (party is null)
        {
            violations.Add("طرف المشتري غائب في فاتورة قياسية (cac:AccountingCustomerParty/cac:Party)");
            return;
        }

        if (string.IsNullOrWhiteSpace(
                party.Element(Cac + "PartyTaxScheme")?.Element(Cbc + "CompanyID")?.Value))
            violations.Add("الرقم الضريبي للمشتري غائب في فاتورة قياسية (PartyTaxScheme/CompanyID)");

        if (string.IsNullOrWhiteSpace(
                party.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "RegistrationName")?.Value))
            violations.Add("الاسم المسجَّل للمشتري غائب (PartyLegalEntity/RegistrationName)");

        var address = party.Element(Cac + "PostalAddress");
        if (address is null)
        {
            violations.Add("العنوان الوطني للمشتري غائب في فاتورة قياسية (cac:PostalAddress)");
            return;
        }

        foreach (var required in new[]
                 { "StreetName", "BuildingNumber", "CitySubdivisionName", "CityName",
                   "PostalZone", "CountrySubentity" })
        {
            if (string.IsNullOrWhiteSpace(address.Element(Cbc + required)?.Value))
                violations.Add($"حقل «{required}» غائب من عنوان المشتري");
        }
    }

    // ── الترتيب ─────────────────────────────────────────────────────────────

    private static void CheckChildOrder(XElement root, List<string> violations)
    {
        var rank = ChildOrder
            .Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i);

        var lastRank = -1;
        var lastName = "";
        foreach (var child in root.Elements())
        {
            if (!rank.TryGetValue(child.Name.LocalName, out var current))
                continue;   // عنصر غير مُرمَّز — لا يُحكَم عليه

            if (current < lastRank)
            {
                violations.Add(
                    $"ترتيب مخالف للمتتالية: «{child.Name.LocalName}» بعد «{lastName}» — " +
                    "والمخطّط يضعه قبله.");
            }

            lastRank = Math.Max(lastRank, current);
            lastName = child.Name.LocalName;
        }
    }

    private static void CheckMandatory(XElement root, List<string> violations)
    {
        foreach (var (ns, name) in Mandatory)
        {
            if (root.Element(ns + name) is null)
                violations.Add($"عنصر إلزاميّ غائب: {name}");
        }

        if (!root.Elements(Cac + "InvoiceLine").Any())
            violations.Add("لا سطر واحد في الفاتورة (InvoiceLine)");
    }

    private static void CheckInvoiceTypeCode(XElement root, List<string> violations)
    {
        var typeCode = root.Element(Cbc + "InvoiceTypeCode");
        if (typeCode is null)
            return;

        var value = typeCode.Value;
        if (value is not ("388" or "381" or "383"))
            violations.Add($"رمز نوع المستند غير معروف: {value} (المتوقّع 388 أو 381 أو 383)");

        var name = typeCode.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name))
            violations.Add("سمة name على InvoiceTypeCode غائبة — بها يُميَّز القياسيّ من المبسّط");
        else if (name.Length != 7)
            violations.Add($"سمة name يجب أن تكون سبع خانات: {name}");
    }

    // ── المراجع الإضافية ────────────────────────────────────────────────────

    private static void CheckDocumentReferences(XElement root, List<string> violations)
    {
        var refs = root.Elements(Cac + "AdditionalDocumentReference")
            .ToDictionary(r => r.Element(Cbc + "ID")?.Value ?? "", r => r);

        foreach (var required in new[] { "ICV", "PIH", "QR" })
        {
            if (!refs.ContainsKey(required))
                violations.Add($"مرجع إضافيّ إلزاميّ غائب: {required}");
        }

        // العدّاد قيمةٌ نصّية في cbc:UUID — لا مرفق ثنائيّ.
        if (refs.TryGetValue("ICV", out var icv))
        {
            if (icv.Element(Cbc + "UUID") is null)
                violations.Add("ICV يجب أن يُكتب في cbc:UUID مباشرةً");
            if (icv.Element(Cac + "Attachment") is not null)
                violations.Add("ICV مكتوب مرفقاً — والمرفق لـPIH وQR وحدهما");
        }

        // PIH وQR مرفقان ثنائيان.
        foreach (var asAttachment in new[] { "PIH", "QR" })
        {
            if (!refs.TryGetValue(asAttachment, out var element))
                continue;
            var binary = element.Element(Cac + "Attachment")?.Element(Cbc + "EmbeddedDocumentBinaryObject");
            if (binary is null)
                violations.Add($"{asAttachment} يجب أن يُكتب مرفقاً في EmbeddedDocumentBinaryObject");
            else if (string.IsNullOrWhiteSpace(binary.Value))
                violations.Add($"{asAttachment} فارغ");
        }
    }

    private static void CheckSignature(XElement root, List<string> violations)
    {
        var signature = root.Element(Cac + "Signature");
        if (signature is null)
            return;   // سُجّل في CheckMandatory

        var id = signature.Element(Cbc + "ID")?.Value;
        var method = signature.Element(Cbc + "SignatureMethod")?.Value;

        if (id != "urn:oasis:names:specification:ubl:signature:Invoice")
            violations.Add($"معرّف cac:Signature غير المتوقّع: {id ?? "غائب"}");
        if (method != "urn:oasis:names:specification:ubl:dsig:enveloped:xades")
            violations.Add($"أسلوب التوقيع غير المتوقّع: {method ?? "غائب"}");
    }

    // ── الضريبة ─────────────────────────────────────────────────────────────

    /// <summary>
    /// (CEEF-230 · ط1-٢) <b>حسابُ الخصم — لا شكلُه.</b>
    ///
    /// <para>ثلاثُ معادلاتٍ تفرضها الهيئة على المستند، وكلُّها <b>تُقاس بالأرقام لا بوجود
    /// العنصر</b>: عنصرٌ موجودٌ بمبلغٍ خاطئ يمرّ على كلّ فحص وجود، <b>ويسقط عند
    /// المُصدِّق</b>.</para>
    ///
    /// <list type="number">
    /// <item><c>LineExtensionAmount</c> == مجموع <c>LineExtensionAmount</c> للأسطر.</item>
    /// <item><c>TaxExclusiveAmount</c> == <c>LineExtensionAmount</c> − <c>AllowanceTotalAmount</c>.</item>
    /// <item><c>AllowanceTotalAmount</c> == مجموع خصومات المستند المعلَنة.</item>
    /// </list>
    ///
    /// <para><b>ورابعةٌ أهمُّها:</b> مجموعُ أوعية الضريبة == <c>TaxExclusiveAmount</c>.
    /// فخصمٌ يُطرح من الإجمالي ولا يُوزَّع على الأوعية <b>يمرّ على المعادلات الثلاث ويسقط
    /// هنا</b> — وهو بعينه العطلُ الذي يجعل الضريبةَ محسوبةً على مبلغٍ لم يُدفع.</para>
    /// </summary>
    private static void CheckAllowances(XElement root, List<string> violations)
    {
        var monetary = root.Element(Cac + "LegalMonetaryTotal");
        if (monetary is null)
            return;   // ‏سُجّل في CheckMandatory

        var lineExtension = Amount(monetary, "LineExtensionAmount");
        var taxExclusive = Amount(monetary, "TaxExclusiveAmount");
        var allowanceTotal = Amount(monetary, "AllowanceTotalAmount");
        var chargeTotal = Amount(monetary, "ChargeTotalAmount");

        var linesSum = root.Elements(Cac + "InvoiceLine")
            .Sum(l => Amount(l, "LineExtensionAmount") ?? 0m);

        if (lineExtension is not null && Math.Abs(lineExtension.Value - linesSum) > 0.01m)
            violations.Add(
                $"LineExtensionAmount {lineExtension.Value:N2} يخالف مجموعَ الأسطر {linesSum:N2}");

        if (lineExtension is not null && taxExclusive is not null)
        {
            var expected = lineExtension.Value - (allowanceTotal ?? 0m) + (chargeTotal ?? 0m);
            if (Math.Abs(expected - taxExclusive.Value) > 0.01m)
                violations.Add(
                    $"TaxExclusiveAmount {taxExclusive.Value:N2} يخالف "
                    + $"LineExtensionAmount − AllowanceTotalAmount = {expected:N2}");
        }

        var declared = root.Elements(Cac + "AllowanceCharge")
            .Where(a => a.Element(Cbc + "ChargeIndicator")?.Value == "false")
            .Sum(a => Amount(a, "Amount") ?? 0m);

        if (Math.Abs(declared - (allowanceTotal ?? 0m)) > 0.01m)
            violations.Add(
                $"AllowanceTotalAmount {(allowanceTotal ?? 0m):N2} يخالف مجموعَ "
                + $"خصومات المستند المعلَنة {declared:N2}");

        foreach (var allowance in root.Elements(Cac + "AllowanceCharge"))
        {
            if (allowance.Element(Cbc + "ChargeIndicator") is null)
                violations.Add("AllowanceCharge بلا ChargeIndicator");
            if (allowance.Element(Cbc + "Amount") is null)
                violations.Add("AllowanceCharge بلا Amount");

            // ‏وفئتُه الضريبية لازمة: خصمٌ لا يُعلن وعاءَه لا يُعرف من أيّ ضريبةٍ يُخصم.
            if (allowance.Element(Cac + "TaxCategory") is null)
                violations.Add("AllowanceCharge على مستوى المستند بلا TaxCategory");
        }

        if (taxExclusive is null) return;

        var taxableSum = root.Elements(Cac + "TaxTotal")
            .Elements(Cac + "TaxSubtotal")
            .Sum(sub => Amount(sub, "TaxableAmount") ?? 0m);

        if (Math.Abs(taxableSum - taxExclusive.Value) > 0.01m)
            violations.Add(
                $"مجموعُ أوعية الضريبة {taxableSum:N2} يخالف TaxExclusiveAmount "
                + $"{taxExclusive.Value:N2} — خصمٌ لم يُوزَّع على الأوعية");
    }

    private static decimal? Amount(XElement parent, string name) =>
        decimal.TryParse(parent.Element(Cbc + name)?.Value,
            NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static void CheckTaxTotals(XElement root, List<string> violations)
    {
        var taxTotal = root.Elements(Cac + "TaxTotal").FirstOrDefault();
        if (taxTotal is null)
            return;

        if (taxTotal.Element(Cbc + "TaxAmount") is null)
            violations.Add("TaxTotal بلا TaxAmount");

        var subtotals = taxTotal.Elements(Cac + "TaxSubtotal").ToList();
        if (subtotals.Count == 0)
            violations.Add("لا وعاء ضريبيّ واحد (TaxSubtotal) — إلزاميّ لدى الهيئة");

        foreach (var sub in subtotals)
        {
            if (sub.Element(Cbc + "TaxableAmount") is null) violations.Add("TaxSubtotal بلا TaxableAmount");
            if (sub.Element(Cbc + "TaxAmount") is null) violations.Add("TaxSubtotal بلا TaxAmount");

            var category = sub.Element(Cac + "TaxCategory");
            if (category is null)
            {
                violations.Add("TaxSubtotal بلا TaxCategory");
                continue;
            }

            var code = category.Element(Cbc + "ID")?.Value;
            if (code is not ("S" or "Z" or "E" or "O"))
                violations.Add($"رمز الفئة الضريبية غير معروف: {code ?? "غائب"}");
            if (category.Element(Cbc + "Percent") is null)
                violations.Add("TaxCategory بلا Percent");
            if (category.Element(Cac + "TaxScheme")?.Element(Cbc + "ID")?.Value != "VAT")
                violations.Add("TaxScheme يجب أن يكون VAT");

            CheckExemptionReason(category, code, violations);
        }
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب الشقّ الثاني) <b>BR-KSA-CL-06</b>: فئةٌ <c>Z</c> أو <c>E</c> أو
    /// <c>O</c> بلا رمز سبب إعفاء (BT-121) من قائمة السعودية ⟵ ترفضها الهيئة.
    ///
    /// <para>ويُفحص <b>الرمزُ نفسُه</b> لا وجودُ العنصر: رمزٌ مخترَع أو رمزُ فئةٍ أخرى
    /// (<c>VATEX-SA-32</c> على وعاءٍ معفى مثلاً) يمرّ على فحص الوجود ويسقط عند الهيئة.</para>
    ///
    /// <para>وترتيبُ الأبناء في <c>cac:TaxCategory</c> مُلزِم أيضاً:
    /// <c>ID → Percent → TaxExemptionReasonCode → TaxExemptionReason → TaxScheme</c>.</para>
    /// </summary>
    private static void CheckExemptionReason(XElement category, string? code, List<string> violations)
    {
        if (code is not ("Z" or "E" or "O"))
            return;

        var reasonCode = category.Element(Cbc + "TaxExemptionReasonCode")?.Value;
        var reasonText = category.Element(Cbc + "TaxExemptionReason")?.Value;

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            violations.Add($"فئة «{code}» بلا TaxExemptionReasonCode — BR-KSA-CL-06");
            return;
        }

        if (string.IsNullOrWhiteSpace(reasonText))
            violations.Add($"فئة «{code}» بلا TaxExemptionReason");

        var known = Brqosh.Domain.Tax.VatExemptionReason.Find(reasonCode);
        if (known is null)
            violations.Add($"رمز سبب إعفاء خارج قائمة الهيئة: {reasonCode}");
        else if (Brqosh.Domain.Tax.VatCategoryExtensions.UblCode(known.Category) != code)
            violations.Add($"رمز «{reasonCode}» يخصّ فئة «{Brqosh.Domain.Tax.VatCategoryExtensions.UblCode(known.Category)}» لا فئة «{code}»");

        string[] sequence = ["ID", "Percent", "TaxExemptionReasonCode", "TaxExemptionReason", "TaxScheme"];
        var present = category.Elements()
            .Select(e => e.Name.LocalName)
            .Where(n => sequence.Contains(n, StringComparer.Ordinal))
            .ToList();
        var sorted = present.OrderBy(n => Array.IndexOf(sequence, n)).ToList();
        if (!present.SequenceEqual(sorted, StringComparer.Ordinal))
            violations.Add("ترتيب أبناء TaxCategory مخالف للتسلسل المُلزِم: " + string.Join(" -> ", present));
    }

    private static void CheckLines(XElement root, List<string> violations)
    {
        foreach (var line in root.Elements(Cac + "InvoiceLine"))
        {
            var id = line.Element(Cbc + "ID")?.Value ?? "?";
            if (line.Element(Cbc + "InvoicedQuantity") is null)
                violations.Add($"سطر {id} بلا InvoicedQuantity");
            if (line.Element(Cbc + "LineExtensionAmount") is null)
                violations.Add($"سطر {id} بلا LineExtensionAmount");
            if (line.Element(Cac + "Item") is null)
                violations.Add($"سطر {id} بلا Item");
        }
    }

    /// <summary>
    /// (CEEF-229 · Z-5) إشعار دائن/مدين يجب أن يحمل سبب إصداره — إلزاميّ نظاميّ.
    /// و<c>cac:PaymentMeans</c> يفرض <c>cbc:PaymentMeansCode</c> أوّلَ أبنائه.
    /// </summary>
    private static void CheckIssueReason(XElement root, List<string> violations)
    {
        var typeCode = root.Element(Cbc + "InvoiceTypeCode")?.Value;
        if (typeCode is not ("381" or "383"))
            return;

        var means = root.Element(Cac + "PaymentMeans");
        if (means is null)
        {
            violations.Add("إشعار دائن/مدين بلا سبب إصدار (cac:PaymentMeans) — إلزاميّ");
            return;
        }

        if (means.Elements().FirstOrDefault()?.Name != Cbc + "PaymentMeansCode")
            violations.Add("cac:PaymentMeans يجب أن يبدأ بـcbc:PaymentMeansCode");
        if (string.IsNullOrWhiteSpace(means.Element(Cbc + "InstructionNote")?.Value))
            violations.Add("سبب الإصدار فارغ (cbc:InstructionNote)");
    }

    /// <summary>
    /// (CEEF-229 · Z-10) طرف البائع كاملاً: سجل تجاريّ واسمٌ مسجَّل وعنوانٌ وطنيّ ورقمٌ ضريبيّ.
    /// ويُفحص <b>ترتيب</b> أبناء <c>cac:Party</c> كذلك — فمتتاليته مُلزِمة كمتتالية الفاتورة.
    /// </summary>
    private static void CheckSupplierParty(XElement root, List<string> violations)
    {
        var party = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
        if (party is null)
        {
            violations.Add("طرف البائع غائب (cac:AccountingSupplierParty/cac:Party)");
            return;
        }

        var crn = party.Element(Cac + "PartyIdentification")?.Element(Cbc + "ID");
        if (crn is null)
            violations.Add("رقم السجل التجاري غائب (PartyIdentification schemeID=CRN)");
        else if (crn.Attribute("schemeID")?.Value != "CRN")
            violations.Add("سمة schemeID على رقم السجل التجاري يجب أن تكون CRN");

        if (string.IsNullOrWhiteSpace(
                party.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "RegistrationName")?.Value))
            violations.Add("الاسم المسجَّل غائب (PartyLegalEntity/RegistrationName)");

        var address = party.Element(Cac + "PostalAddress");
        if (address is null)
        {
            violations.Add("العنوان الوطني غائب (cac:PostalAddress)");
        }
        else
        {
            foreach (var required in new[]
                     { "StreetName", "BuildingNumber", "CitySubdivisionName", "CityName",
                       "PostalZone", "CountrySubentity" })
            {
                if (string.IsNullOrWhiteSpace(address.Element(Cbc + required)?.Value))
                    violations.Add($"حقل العنوان الوطني «{required}» غائب أو فارغ");
            }

            if (string.IsNullOrWhiteSpace(
                    address.Element(Cac + "Country")?.Element(Cbc + "IdentificationCode")?.Value))
                violations.Add("رمز الدولة غائب (Country/IdentificationCode)");
        }

        // ترتيب أبناء cac:Party — مُلزِم كمتتالية الفاتورة.
        string[] partyOrder =
        [
            "PartyIdentification", "PartyName", "PostalAddress", "PhysicalLocation",
            "PartyTaxScheme", "PartyLegalEntity", "Contact", "Person",
        ];
        var rank = partyOrder.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
        var last = -1;
        var lastName = "";
        foreach (var child in party.Elements())
        {
            if (!rank.TryGetValue(child.Name.LocalName, out var current)) continue;
            if (current < last)
                violations.Add($"ترتيب مخالف داخل cac:Party: «{child.Name.LocalName}» بعد «{lastName}»");
            last = Math.Max(last, current);
            lastName = child.Name.LocalName;
        }
    }

    private static void CheckBillingReference(XElement root, List<string> violations)
    {
        var typeCode = root.Element(Cbc + "InvoiceTypeCode")?.Value;
        if (typeCode is not ("381" or "383"))
            return;

        var reference = root.Element(Cac + "BillingReference")?
            .Element(Cac + "InvoiceDocumentReference")?
            .Element(Cbc + "ID")?.Value;

        if (string.IsNullOrWhiteSpace(reference))
            violations.Add("إشعار دائن/مدين بلا BillingReference للفاتورة الأصلية — إلزاميّ");
    }
}
