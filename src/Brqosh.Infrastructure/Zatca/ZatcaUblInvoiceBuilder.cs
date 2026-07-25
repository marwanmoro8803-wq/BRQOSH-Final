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
        Guid? BillingReferenceUuid = null);

    public static byte[] BuildXml(InvoiceData data)
    {
        var issueTime = "12:00:00";
        var typeCode = data.IsDebitNote ? "383" : data.IsCreditNote ? "381" : "388";
        var typeName = data.IsSimplified ? "0200000" : "0100000";

        var root = new XElement(Ubl + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            El(Cbc, "ProfileID", "reporting:1.0"),
            El(Cbc, "ID", data.InvoiceNumber),
            El(Cbc, "UUID", data.Uuid.ToString()),
            El(Cbc, "IssueDate", data.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            El(Cbc, "IssueTime", issueTime),
            InvoiceTypeCodeEl(typeCode, typeName),
            El(Cbc, "DocumentCurrencyCode", "SAR"),
            El(Cbc, "TaxCurrencyCode", "SAR"));

        AddDocRef(root, "ICV", data.Icv);
        AddDocRef(root, "PIH", data.PreviousInvoiceHash);
        AddDocRef(root, "QR", data.QrTlvBase64);

        if ((data.IsCreditNote || data.IsDebitNote) &&
            !string.IsNullOrWhiteSpace(data.BillingReferenceInvoiceNumber))
            AddBillingReference(root, data.BillingReferenceInvoiceNumber!, data.BillingReferenceUuid);

        AddSupplier(root, data.SellerName, data.SellerVat);
        if (!string.IsNullOrWhiteSpace(data.CustomerName) || !string.IsNullOrWhiteSpace(data.CustomerVat))
            AddCustomer(root, data.CustomerName, data.CustomerVat);

        // cac:TaxSubtotal إلزاميّ لدى الهيئة (BR-CO-18/KSA): وعاء الضريبة + فئتها + نسبتها.
        // كان غائباً تماماً فتُرفض الفاتورة في الفحص رغم صحة التوقيع (ع-Z2، CEEF-202).
        //
        // (CEEF-207) وعاء لكل فئة ضريبية حاضرة في الفاتورة، لا وعاء واحد بنسبة مسمّرة:
        // فاتورة فيها سطر أساسيّ وآخر صفريّ تُنتج وعاءين — والنسبة المعلنة في كل وعاء
        // تطابق مبلغ ضريبته، فينتفي التناقض الذي كان يرفضه المدقّق (صفر مصنّف بنسبة 15).
        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(data.Lines);
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
                    new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")))));
        }

        root.Add(taxTotal);

        var monetary = new XElement(Cac + "LegalMonetaryTotal",
            El(Cbc, "LineExtensionAmount", F(data.Subtotal), ("currencyID", "SAR")),
            El(Cbc, "TaxExclusiveAmount", F(data.Subtotal), ("currencyID", "SAR")),
            El(Cbc, "TaxInclusiveAmount", F(data.GrandTotal), ("currencyID", "SAR")),
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

    private static void AddSupplier(XElement root, string name, string vat)
    {
        var party = new XElement(Cac + "AccountingSupplierParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyName", El(Cbc, "Name", name)),
                PartyTax(vat)));
        root.Add(party);
    }

    private static void AddCustomer(XElement root, string? name, string? vat)
    {
        var partyEl = new XElement(Cac + "Party");
        if (!string.IsNullOrWhiteSpace(name))
            partyEl.Add(new XElement(Cac + "PartyName", El(Cbc, "Name", name)));
        if (!string.IsNullOrWhiteSpace(vat))
            partyEl.Add(PartyTax(vat));
        root.Add(new XElement(Cac + "AccountingCustomerParty", partyEl));
    }

    private static XElement PartyTax(string vat) =>
        new(Cac + "PartyTaxScheme",
            El(Cbc, "CompanyID", vat),
            new XElement(Cac + "TaxScheme", El(Cbc, "ID", "VAT")));
}
