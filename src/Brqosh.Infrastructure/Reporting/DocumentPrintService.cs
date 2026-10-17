using Brqosh.Domain.Companies;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public sealed record PrintResult(byte[] PdfBytes, string SuggestedFileName);

/// <summary>Central PDF export — loads entities, generates PDF, logs audit.</summary>
public partial class DocumentPrintService(BrqoshDbContext db, AuditService audit, Treasury.ExchangeRateService rateService)
{
    public async Task<PrintResult> ExportSalesInvoiceAsync(
        long companyId, long invoiceId, string username, CancellationToken ct = default)
    {
        var request = await SalesInvoiceRequestAsync(companyId, invoiceId, ct);
        var pdf = SalesInvoicePdfGenerator.Generate(request);

        var fileName = $"brqosh-inv-{SanitizeFileName(request.DocumentNumber)}.pdf";
        await LogAsync(companyId, username, "SalesInvoice", request.DocumentNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>(ح-٤٢) ما تُطبع منه فاتورةُ المبيعات — يُقاس قبل أن يُرسَم.</summary>
    internal async Task<SalesInvoicePdfGenerator.Request> SalesInvoiceRequestAsync(
        long companyId, long invoiceId, CancellationToken ct = default)
    {
        var (company, invoice, customerName, zatca) = await LoadSalesInvoiceAsync(companyId, invoiceId, ct);
        PrintPostedGuard.EnsureSalesInvoicePosted(invoice);
        var kind = ResolveSalesKind(invoice, zatca);
        // (CEEF-229 · م2-١٨) قراءةٌ تتجاوز مرشّح الحذف الناعم عمداً — المستند تاريخيّ.
        // الطرف كان موجوداً يوم صدور المستند، وحذفُه لاحقاً لا يُلغي المستند. وقراءةُ
        // المرشَّح كانت ترمي «Sequence contains no elements» بالإنجليزية داخل واجهةٍ
        // عربية، فتصير فاتورةٌ ضريبية سعودية **غير قابلة للطباعة أبداً** — وهي وثيقةٌ
        // يُلزم النظام السعوديّ بالاحتفاظ بها وإصدار نسخٍ منها.
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == invoice.CustomerId, ct);
        var amounts = PrintedAmounts.Of(invoice.IsForeignCurrency, invoice.CurrencyCode, invoice.ExchangeRate,
            invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.Subtotal, invoice.VatAmount);
        // ‏(ح-٥٣ · tax-zatca#9) **الرمزُ المطبوعُ يختاره قارئٌ واحد**: المعتمدُ من الهيئة، أو
        // ‏رمزُنا حيث هو النافذ، أو لا رمزَ وتنبيهٌ — وكان رمزُنا أبداً، اعتُمدت أم لم تُعتمد.
        var mark = ZatcaHandOverCopy.MarkFor(zatca,
            () => Phase1QrFallback(company, invoice.InvoiceDate, invoice.PostedAtUtc, amounts.Total, amounts.Vat));
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            kind,
            PartyName: customerName,
            PartyVatNumber: customer.VatRegistrationNumber,
            PartyAddress: customer.Address,
            QrTlvBase64: mark.QrTlvBase64,
            Icv: zatca?.Icv,
            ComplianceStatus: zatca?.ComplianceStatus.ToString(),
            Currency: amounts.Currency,
            ClearanceNotice: mark.ClearanceNotice);

        return new SalesInvoicePdfGenerator.Request(
            PrintBranding.FromCompany(company),
            invoice.InvoiceNumber ?? invoice.Id.ToString(),
            invoice.InvoiceDate,
            LinesOf(invoice.Lines),
            amounts.Subtotal, amounts.Vat, amounts.Total,
            invoice.DocumentDiscountAmount,
            options);
    }

    public async Task<PrintResult> ExportPurchaseInvoiceAsync(
        long companyId, long invoiceId, string username, CancellationToken ct = default)
    {
        var request = await PurchaseInvoiceRequestAsync(companyId, invoiceId, ct);
        var pdf = SalesInvoicePdfGenerator.Generate(request);

        var fileName = $"brqosh-pi-{SanitizeFileName(request.DocumentNumber)}.pdf";
        await LogAsync(companyId, username, "PurchaseInvoice", request.DocumentNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>(ح-٤٢) ما تُطبع منه فاتورةُ المشتريات.</summary>
    internal async Task<SalesInvoicePdfGenerator.Request> PurchaseInvoiceRequestAsync(
        long companyId, long invoiceId, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var invoice = await db.PurchaseInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == invoiceId, ct);
        PrintPostedGuard.EnsurePurchaseInvoicePosted(invoice);
        var supplier = await db.Suppliers.IgnoreQueryFilters().SingleAsync(s => s.Id == invoice.SupplierId, ct);
        var amounts = PrintedAmounts.Of(invoice.IsForeignCurrency, invoice.CurrencyCode, invoice.ExchangeRate,
            invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.Subtotal, invoice.VatAmount);

        return new SalesInvoicePdfGenerator.Request(
            PrintBranding.FromCompany(company),
            invoice.InvoiceNumber ?? invoice.Id.ToString(),
            invoice.InvoiceDate,
            LinesOf(invoice.Lines),
            amounts.Subtotal, amounts.Vat, amounts.Total,
            // ‏(ح-٤٤) فاتورةُ الشراء بلا خصمِ مستندٍ في النطاق — صفرٌ مُسمًّى لا افتراضيّ.
            DocumentDiscount: 0m,
            new SalesInvoicePdfGenerator.InvoicePrintOptions(
                PrintDocumentKind.PurchaseInvoice,
                PartyName: supplier.NameAr,
                Currency: amounts.Currency));
    }

    public async Task<PrintResult> ExportCreditNoteAsync(
        long companyId, long noteId, string username, CancellationToken ct = default)
    {
        var request = await CreditNoteRequestAsync(companyId, noteId, ct);
        var pdf = SalesInvoicePdfGenerator.Generate(request);

        var fileName = $"brqosh-cn-{SanitizeFileName(request.DocumentNumber)}.pdf";
        await LogAsync(companyId, username, "CreditNote", request.DocumentNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>(ح-٤٢) ما يُطبع منه الإشعارُ الدائن.</summary>
    internal async Task<SalesInvoicePdfGenerator.Request> CreditNoteRequestAsync(
        long companyId, long noteId, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var note = await db.CreditNotes.SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, ct);
        PrintPostedGuard.EnsureCreditNotePosted(note);
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == note.CustomerId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == noteId &&
            d.DocumentType == ZatcaDocumentType.CreditNote, ct);

        var amounts = PrintedAmounts.Of(note.IsForeignCurrency, note.CurrencyCode, note.ExchangeRate,
            note.ForeignSubtotal, note.ForeignVatAmount, note.Subtotal, note.VatAmount);
        // ‏(ح-٤٣) **رقمُ الفاتورة التي يُصحّحها** — قراءةٌ تتجاوز المرشّحات: المستندُ تاريخيّ.
        var original = await OriginalSalesInvoiceNumberAsync(note.OriginalInvoiceId, ct);
        // ‏(ح-٥٣) الإشعارُ لفاتورةٍ ضريبيّةٍ يُخلَّص كذلك — فرمزُه من القارئ الواحد.
        var mark = ZatcaHandOverCopy.MarkFor(zatca,
            () => Phase1QrFallback(company, note.NoteDate, note.PostedAtUtc, amounts.Total, amounts.Vat));
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            PrintDocumentKind.CreditNote,
            PartyName: customer.NameAr,
            // ‏(ح-٤٣ · ui-ux-i18n-printing#0) ‏**هويّةُ المشتري كما في الفاتورة** — وكان الإشعارُ
            // ‏بلا رقمه الضريبيّ ولا عنوانه، والفاتورةُ في الملفّ نفسِه تمرّرهما.
            PartyVatNumber: customer.VatRegistrationNumber,
            PartyAddress: customer.Address,
            QrTlvBase64: mark.QrTlvBase64,
            Icv: zatca?.Icv,
            ComplianceStatus: zatca?.ComplianceStatus.ToString(),
            Currency: amounts.Currency,
            ReferenceDocumentNumber: original,
            ReasonAr: note.Reason,
            ClearanceNotice: mark.ClearanceNotice);

        return new SalesInvoicePdfGenerator.Request(
            PrintBranding.FromCompany(company),
            note.NoteNumber ?? note.Id.ToString(),
            note.NoteDate,
            LinesOf(note.Lines),
            amounts.Subtotal, amounts.Vat, amounts.Total,
            note.DocumentDiscountAmount,
            options);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤٢ · tax-zatca#14) <b>مبالغُ المستند المطبوعة — بعملةٍ واحدة.</b>
    ///
    /// <para><b>وكانت كلُّ طباعةٍ تمرّر الإجماليّاتِ بالريال والسطورَ بعملة المستند</b> — خمسُ
    /// طباعاتٍ بالخطأ نفسِه (الفاتورتان والإشعاران ومستندُ الإلغاء). <b>والقاعدةُ هنا مرّةً واحدة</b>: مستندٌ أجنبيٌّ يُطبع بعملته
    /// (سطوراً وإجماليّاتٍ ورمزَ QR، كما في الـXML) ومعه الريالُ إفصاحاً؛ <b>ومستندٌ بالريال
    /// يُطبع بأرقامه كما كان حرفاً</b> — لا يُقرأ منه <c>Foreign*</c> أصلاً، فصفوفٌ قديمةٌ لم
    /// تُعبَّأ لا تُطبع أصفاراً.</para>
    /// </summary>
    internal sealed record PrintedAmounts(
        decimal Subtotal, decimal Vat, SalesInvoicePdfGenerator.CurrencyDisclosure? Currency)
    {
        public decimal Total => Subtotal + Vat;

        public static PrintedAmounts Of(bool isForeign, string currencyCode, decimal exchangeRate,
            decimal foreignSubtotal, decimal foreignVat, decimal sarSubtotal, decimal sarVat) =>
            isForeign
                ? new(foreignSubtotal, foreignVat,
                      new SalesInvoicePdfGenerator.CurrencyDisclosure(currencyCode, exchangeRate, sarSubtotal, sarVat))
                : new(sarSubtotal, sarVat, null);
    }

    /// <summary>(ح-٤٣) رقمُ فاتورة المبيعات الأصليّة — ومعرّفُها إن لم تُرقَّم.</summary>
    private async Task<string> OriginalSalesInvoiceNumberAsync(long invoiceId, CancellationToken ct) =>
        await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.InvoiceNumber)
            .SingleOrDefaultAsync(ct) ?? invoiceId.ToString();

    private static IReadOnlyList<SalesInvoicePdfGenerator.Line> LinesOf(IEnumerable<SalesInvoiceLine> lines) =>
        [.. lines.Select(l =>
            new SalesInvoicePdfGenerator.Line(
                l.Description, l.Quantity, l.UnitPrice, l.LineTotal, l.VatCategory, l.DiscountAmount))];

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>فاتورةُ بيع الأصل الثابت مطبوعةً — يأخذها المشتري.</b>
    ///
    /// <para><b>ومستندٌ ضريبيٌّ لا يُطبع نصفُ مستند</b>: المشتري يطلب فاتورتَه ورقاً أو
    /// ملفّاً ليخصم مدخلاتِه، <b>وزرُّ الطباعة في شاشة المستندات كان سيرمي</b> على النوع
    /// الذي أُضيف للتوّ.</para>
    ///
    /// <para><b>ولا نوعَ طباعةٍ جديد</b>: الفاتورةُ القياسيّةُ والمبسَّطةُ تصفان هذا البيعَ
    /// تماماً كما تصفان أيَّ بيع — <b>والفرقُ في المشتري لا في المبيع</b>. فيُقرأ النوعُ
    /// من وضع الإرسال كما يُقرأ لفاتورة المبيعات.</para>
    /// </summary>
    public async Task<PrintResult> ExportAssetDisposalAsync(
        long companyId, long assetId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var asset = await db.FixedAssets.IgnoreQueryFilters()
            .SingleAsync(a => a.CompanyId == companyId && a.Id == assetId, ct);

        // ‏**ولا تُطبع فاتورةٌ لبيعٍ لم يقع** — حارسُ «المرحَّل وحدَه» بصيغته هنا:
        // ‏خردةٌ بلا مقابلٍ أو منشأةٌ خارج النطاق لا فاتورةَ لها فلا شيءَ يُطبع.
        if (asset.DisposalInvoiceNumber is not string invoiceNumber
            || asset.DisposalProceeds is not decimal proceeds)
            throw new Domain.Accounting.AccountingException(
                $"الأصل {asset.Code} لم يُبَع بفاتورة ضريبة — لا يوجد مستند يُطبع.");

        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == assetId &&
            d.DocumentType == ZatcaDocumentType.AssetDisposal, ct);

        var buyerName = asset.DisposalBuyerCustomerId is long buyerId
            ? (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == buyerId, ct)).NameAr
            : "عميل نقدي";

        var category = asset.DisposalVatCategory ?? VatCategory.Standard;
        var vat = Math.Round(
            proceeds * category.RateFor(VatCalculator.SaudiVatRate),
            2, MidpointRounding.AwayFromZero);

        // ‏**والتاريخُ تاريخُ القيد** — وهو ما كتبه المولّد في المستند الإلكترونيّ، فلا
        // ‏تختلف الورقةُ عمّا وصل الهيئة.
        var invoiceDate = asset.DisposalJournalEntryId is long entryId
            ? await db.JournalEntries.Where(e => e.Id == entryId).Select(e => e.EntryDate).SingleAsync(ct)
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var lines = new List<SalesInvoicePdfGenerator.Line>
        {
            new($"بيع أصل ثابت {asset.Code} — {asset.NameAr}", 1m, proceeds, proceeds, category),
        };

        var branding = PrintBranding.FromCompany(company);
        // ‏(ح-٥٣) وبيعُ الأصل لمشترٍ مسجَّلٍ فاتورةٌ ضريبيّةٌ تُخلَّص — فرمزُه من القارئ الواحد.
        var mark = ZatcaHandOverCopy.MarkFor(zatca,
            () => Phase1QrFallback(company, invoiceDate, null, proceeds + vat, vat));
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            zatca?.TransmissionMode == ZatcaTransmissionMode.Reporting
                ? PrintDocumentKind.SimplifiedSalesInvoice
                : PrintDocumentKind.StandardSalesInvoice,
            PartyName: buyerName,
            QrTlvBase64: mark.QrTlvBase64,
            Icv: zatca?.Icv,
            ComplianceStatus: zatca?.ComplianceStatus.ToString(),
            ClearanceNotice: mark.ClearanceNotice);

        var pdf = SalesInvoicePdfGenerator.Generate(
            branding, invoiceNumber, invoiceDate, lines, proceeds, vat, proceeds + vat, options);

        var fileName = $"brqosh-fas-{SanitizeFileName(invoiceNumber)}.pdf";
        await LogAsync(companyId, username, "AssetDisposal", invoiceNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٩) <b>مستندُ الإلغاء يُطبع — وكان يرمي.</b>
    ///
    /// <para>ثلاثةُ أنواعٍ تُصدرها الهيئةُ عند الإلغاء (<c>InvoiceCancel</c> و
    /// <c>BillingCancel</c> و<c>DebitNote</c>) <b>لم يكن لها فرعٌ في مُوزِّع الطباعة</b>،
    /// فيرمي «طباعة PDF غير مدعومة». <b>والرميةُ كانت أصدقَ من البديل</b>: إسنادُها إلى
    /// مستندها المصدر يطبع <b>الفاتورةَ الملغاة بدل إشعار إلغائها</b> — وهو داءُ ح-١٥
    /// بعينه.</para>
    ///
    /// <para><b>والعلاجُ مستندٌ يصف نفسَه</b>: رقمُه رقمُ مستند ZATCA لا رقمُ المصدر،
    /// وعنوانُه نوعُه هو، <b>وسببُه مكتوبٌ فيه</b> («إلغاء فاتورة مبيعات …») —
    /// <b>وسطورُه سطورُ ما أُلغي</b>، فهي ما عُكس.</para>
    ///
    /// <para><b>ورمزُ QR وعدّادُ ICV من مستند الإلغاء نفسِه</b> لا من المصدر: <b>هما
    /// هويّتُه لدى الهيئة</b>، ونسخُهما من المصدر ينسب المستندَ إلى غيره.</para>
    /// </summary>
    public async Task<PrintResult> ExportZatcaCancellationAsync(
        long companyId, long zatcaDocumentId, string username, CancellationToken ct = default)
    {
        var (request, auditType) = await ZatcaCancellationRequestAsync(companyId, zatcaDocumentId, ct);
        var pdf = SalesInvoicePdfGenerator.Generate(request);

        var fileName = $"brqosh-cancel-{SanitizeFileName(request.DocumentNumber)}.pdf";
        await LogAsync(companyId, username, auditType, request.DocumentNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>
    /// (ح-٤٢) ما يُطبع منه مستندُ الإلغاء — <b>بعملة ما أُلغي</b>: ح-٣٦ جعل الـXML يحمل عملةَ
    /// مصدره، <b>والمطبوعُ بقي يخلط سطوراً بعملة الفاتورة بإجماليّاتٍ بالريال</b> — طباعةٌ
    /// خامسةٌ بالعطل نفسِه لم يُسمِّها التدقيق.
    /// </summary>
    internal async Task<(SalesInvoicePdfGenerator.Request Request, string AuditType)> ZatcaCancellationRequestAsync(
        long companyId, long zatcaDocumentId, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var doc = await db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.CompanyId == companyId && d.Id == zatcaDocumentId, ct);

        var lines = new List<SalesInvoicePdfGenerator.Line>();
        PrintedAmounts amounts;
        decimal documentDiscount;
        DateOnly date;
        string partyName, notes;
        // ‏(ح-٤٣) هويّةُ المشتري والمستندُ الأصليّ — مستندُ الإلغاء إشعارٌ ضريبيٌّ كغيره.
        Customer party;
        string reference;
        var kind = PrintDocumentKind.CreditNote;

        switch (doc.DocumentType)
        {
            case ZatcaDocumentType.InvoiceCancel:
            {
                var invoice = await db.SalesInvoices.SingleAsync(
                    i => i.CompanyId == companyId && i.Id == doc.SourceDocumentId, ct);
                lines.AddRange(LinesOf(invoice.Lines));
                documentDiscount = invoice.DocumentDiscountAmount;
                amounts = PrintedAmounts.Of(invoice.IsForeignCurrency, invoice.CurrencyCode, invoice.ExchangeRate,
                    invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.Subtotal, invoice.VatAmount);
                date = invoice.CancelledAt ?? invoice.InvoiceDate;
                party = await db.Customers.IgnoreQueryFilters()
                    .SingleAsync(c => c.Id == invoice.CustomerId, ct);
                partyName = party.NameAr;
                reference = invoice.InvoiceNumber ?? invoice.Id.ToString();
                notes = $"إلغاء فاتورة مبيعات {reference}";
                break;
            }

            case ZatcaDocumentType.BillingCancel:
            {
                var billing = await db.ProgressBillings.SingleAsync(
                    b => b.CompanyId == companyId && b.Id == doc.SourceDocumentId, ct);
                lines.Add(new SalesInvoicePdfGenerator.Line(
                    billing.PeriodDescription, 1m, billing.TaxableAmount, billing.TaxableAmount));
                // ‏المستخلصُ بالريال بالبناء — لا عملةَ له ولا خصمَ مستند.
                amounts = new PrintedAmounts(billing.TaxableAmount, billing.VatAmount, null);
                documentDiscount = 0m;
                date = billing.CancelledAt ?? billing.BillingDate;
                party = await db.Customers.IgnoreQueryFilters()
                    .SingleAsync(c => c.Id == billing.CustomerId, ct);
                partyName = party.NameAr;
                reference = billing.BillingNumber ?? billing.Id.ToString();
                notes = $"إلغاء مستخلص أعمال {reference}";
                break;
            }

            // ‏(ح-١٦١) ‏**وإلغاءُ بيع الأصل** — سطرُه سطرُ الفاتورة الملغاة، ومشتريها طرفُه (أو «عميل نقدي»).
            case ZatcaDocumentType.DisposalCancel:
            {
                var asset = await db.FixedAssets.IgnoreQueryFilters().SingleAsync(
                    a => a.CompanyId == companyId && a.Id == doc.SourceDocumentId, ct);
                var proceeds = asset.DisposalProceeds ?? 0m;
                var category = asset.DisposalVatCategory ?? Domain.Tax.VatCategory.Standard;
                var vat = Math.Round(
                    proceeds * category.RateFor(Domain.Accounting.VatCalculator.SaudiVatRate), 2, MidpointRounding.AwayFromZero);
                lines.Add(new SalesInvoicePdfGenerator.Line(
                    $"إلغاء بيع أصل ثابت {asset.Code} — {asset.NameAr}", 1m, proceeds, proceeds));
                amounts = new PrintedAmounts(proceeds, vat, null);
                documentDiscount = 0m;
                // ‏تاريخُ الإشعار تاريخُ قيد العكس — به صدر.
                var disposalSource = $"FixedAssetDisposal:{asset.Code}";
                var reversalId = await db.JournalEntries
                    .Where(e => e.CompanyId == companyId && e.SourceDocument == disposalSource && e.ReversalEntryId != null)
                    .OrderByDescending(e => e.Id).Select(e => e.ReversalEntryId).FirstAsync(ct);
                date = await db.JournalEntries.Where(e => e.Id == reversalId).Select(e => e.EntryDate).SingleAsync(ct);
                party = asset.DisposalBuyerCustomerId is long buyerId
                    ? await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == buyerId, ct)
                    : await db.Customers.IgnoreQueryFilters()
                        .Where(c => c.CompanyId == companyId && c.NameAr == "عميل نقدي").FirstAsync(ct);
                partyName = party.NameAr;
                reference = asset.DisposalInvoiceNumber ?? asset.Code;
                notes = $"إلغاء بيع أصل ثابت {reference}";
                break;
            }

            case ZatcaDocumentType.DebitNote:
            {
                var note = await db.CreditNotes.SingleAsync(
                    n => n.CompanyId == companyId && n.Id == doc.SourceDocumentId, ct);
                lines.AddRange(LinesOf(note.Lines));
                documentDiscount = note.DocumentDiscountAmount;
                amounts = PrintedAmounts.Of(note.IsForeignCurrency, note.CurrencyCode, note.ExchangeRate,
                    note.ForeignSubtotal, note.ForeignVatAmount, note.Subtotal, note.VatAmount);
                date = note.CancelledAt ?? note.NoteDate;
                party = await db.Customers.IgnoreQueryFilters()
                    .SingleAsync(c => c.Id == note.CustomerId, ct);
                partyName = party.NameAr;
                reference = note.NoteNumber ?? note.Id.ToString();
                notes = $"إلغاء إشعار دائن {reference}";
                // ‏**إشعارٌ مدينٌ على جانب المبيعات** — لا إشعارُ مشترياتٍ طرفُه مورّد.
                kind = PrintDocumentKind.SalesDebitNote;
                break;
            }

            default:
                throw new Domain.Accounting.AccountingException(
                    $"نوعُ المستند {doc.DocumentType} ليس مستندَ إلغاء.");
        }

        // ‏(ح-٥٣) **ورمزُ الإلغاء المعتمدُ رمزُ الهيئة** — وهويّتُه هو لا مصدرِه (ح-١٩) باقية:
        // ‏المستندُ الممرَّرُ مستندُ الإلغاء نفسُه. ولا بديلَ من الطور الأوّل: المستندُ موجودٌ أبداً.
        var mark = ZatcaHandOverCopy.MarkFor(doc, () => null);
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            kind,
            PartyName: partyName,
            PartyVatNumber: party.VatRegistrationNumber,
            PartyAddress: party.Address,
            QrTlvBase64: mark.QrTlvBase64,
            Icv: doc.Icv,
            ComplianceStatus: doc.ComplianceStatus.ToString(),
            NotesAr: notes,
            Currency: amounts.Currency,
            ReferenceDocumentNumber: reference,
            ClearanceNotice: mark.ClearanceNotice);

        return (new SalesInvoicePdfGenerator.Request(
            PrintBranding.FromCompany(company),
            doc.InvoiceNumber, date, lines, amounts.Subtotal, amounts.Vat, amounts.Total,
            documentDiscount, options),
            doc.DocumentType.ToString());
    }

    public async Task<PrintResult> ExportDebitNoteAsync(
        long companyId, long noteId, string username, CancellationToken ct = default)
    {
        var request = await DebitNoteRequestAsync(companyId, noteId, ct);
        var pdf = SalesInvoicePdfGenerator.Generate(request);

        var fileName = $"brqosh-dn-{SanitizeFileName(request.DocumentNumber)}.pdf";
        await LogAsync(companyId, username, "DebitNote", request.DocumentNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>
    /// (ح-٤٢) ما يُطبع منه الإشعارُ المدين — <b>وقد صار يحمل عملةً بـح-٣٩</b>، فانضمّ إلى
    /// صنف الطباعة المختلطة يومَها وخرج منه هنا.
    /// </summary>
    internal async Task<SalesInvoicePdfGenerator.Request> DebitNoteRequestAsync(
        long companyId, long noteId, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var note = await db.DebitNotes.SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, ct);
        PrintPostedGuard.EnsureDebitNotePosted(note);
        var supplier = await db.Suppliers.IgnoreQueryFilters().SingleAsync(s => s.Id == note.SupplierId, ct);
        var amounts = PrintedAmounts.Of(note.IsForeignCurrency, note.CurrencyCode, note.ExchangeRate,
            note.ForeignSubtotal, note.ForeignVatAmount, note.Subtotal, note.VatAmount);
        // ‏(ح-٤٣) **والإشعارُ المدينُ كذلك يُسمّي فاتورتَه وسببَه** — وهو ما يُرسَل للمورّد.
        var original = await db.PurchaseInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.Id == note.OriginalPurchaseInvoiceId)
            .Select(i => i.InvoiceNumber)
            .SingleOrDefaultAsync(ct) ?? note.OriginalPurchaseInvoiceId.ToString();

        return new SalesInvoicePdfGenerator.Request(
            PrintBranding.FromCompany(company),
            note.NoteNumber ?? note.Id.ToString(),
            note.NoteDate,
            LinesOf(note.Lines),
            amounts.Subtotal, amounts.Vat, amounts.Total,
            // ‏(ح-٤٤) الإشعارُ المدينُ بلا خصمِ مستندٍ في النطاق.
            DocumentDiscount: 0m,
            new SalesInvoicePdfGenerator.InvoicePrintOptions(
                PrintDocumentKind.PurchaseDebitNote,
                PartyName: supplier.NameAr,
                PartyVatNumber: supplier.VatRegistrationNumber,
                PartyAddress: supplier.Address,
                Currency: amounts.Currency,
                ReferenceDocumentNumber: original,
                ReasonAr: note.Reason));
    }

    public async Task<PrintResult> ExportReceiptVoucherAsync(
        long companyId, long voucherId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var voucher = await db.ReceiptVouchers.SingleAsync(v => v.CompanyId == companyId && v.Id == voucherId, ct);
        PrintPostedGuard.EnsureReceiptVoucherPosted(voucher);
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == voucher.CustomerId, ct);
        var pdf = VoucherPdfGenerator.ReceiptVoucher(
            branding,
            voucher.VoucherNumber ?? voucher.Id.ToString(),
            voucher.VoucherDate,
            customer.NameAr,
            voucher.Amount,
            MethodAr(voucher.Method),
            voucher.Reference ?? "",
            voucher.Description ?? "");

        var fileName = $"brqosh-rv-{SanitizeFileName(voucher.VoucherNumber ?? voucher.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "ReceiptVoucher", voucher.VoucherNumber ?? voucher.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportPaymentVoucherAsync(
        long companyId, long voucherId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var voucher = await db.PaymentVouchers.SingleAsync(v => v.CompanyId == companyId && v.Id == voucherId, ct);
        PrintPostedGuard.EnsurePaymentVoucherPosted(voucher);
        var supplier = await db.Suppliers.IgnoreQueryFilters().SingleAsync(s => s.Id == voucher.SupplierId, ct);
        var pdf = VoucherPdfGenerator.PaymentVoucher(
            branding,
            voucher.VoucherNumber ?? voucher.Id.ToString(),
            voucher.VoucherDate,
            supplier.NameAr,
            voucher.Amount,
            MethodAr(voucher.Method),
            voucher.Reference ?? "",
            voucher.Description ?? "");

        var fileName = $"brqosh-pv-{SanitizeFileName(voucher.VoucherNumber ?? voucher.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "PaymentVoucher", voucher.VoucherNumber ?? voucher.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportJournalEntryAsync(
        long companyId, long entryId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var entry = await db.JournalEntries.SingleAsync(e => e.CompanyId == companyId && e.Id == entryId, ct);
        PrintPostedGuard.EnsureJournalEntryPosted(entry);
        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.NameAr, ct);

        var lines = entry.Lines.Select(l =>
        {
            var name = accounts.GetValueOrDefault(l.AccountId, "");
            var desc = string.IsNullOrWhiteSpace(l.Description) ? name : l.Description;
            return new JournalEntryPdfGenerator.LineRow(l.AccountCode, desc, l.Debit, l.Credit);
        }).ToList();

        var status = entry.Status switch
        {
            JournalEntryStatus.Posted => "مرحّل",
            JournalEntryStatus.Reversed => "معكوس",
            _ => "مسودة",
        };

        var branding = PrintBranding.FromCompany(company);
        var pdf = JournalEntryPdfGenerator.Generate(
            branding,
            entry.EntryNumber ?? entry.Id.ToString(),
            entry.EntryDate,
            status,
            entry.Description,
            entry.SourceDocument,
            lines,
            entry.TotalDebit,
            entry.TotalCredit);

        var fileName = $"brqosh-je-{SanitizeFileName(entry.EntryNumber ?? entry.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "JournalEntry", entry.EntryNumber ?? entry.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportCashFlowStatementAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var result = await new CashFlowStatementService(db).ExecuteAsync(companyId, fromDate, toDate);
        var pdf = FinancialReportPdfGenerator.CashFlowStatement(branding, result);
        var fileName = $"brqosh-cashflow-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "CashFlowStatement", $"{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportPosDailyReportAsync(
        long companyId, string username, int days = 30, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var rows = await new PosReportQuery(db).DailySummaryAsync(companyId, days, ct);
        var branding = PrintBranding.FromCompany(company);
        var pdf = PosReportPdfGenerator.DailySummary(branding, rows);
        var fileName = $"brqosh-pos-{DateTime.Now:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "PosDailyReport", DateTime.Today.ToString("yyyy-MM-dd"), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task LogReportExportAsync(
        long companyId, string username, string reportType, string periodKey,
        CancellationToken ct = default) =>
        await LogAsync(companyId, username, reportType, periodKey, ct);

    private async Task<(Domain.Companies.Company company, SalesInvoice invoice, string customerName, ZatcaEInvoiceDocument? zatca)>
        LoadSalesInvoiceAsync(long companyId, long invoiceId, CancellationToken ct)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var invoice = await db.SalesInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == invoiceId, ct);
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == invoice.CustomerId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == invoiceId &&
            d.DocumentType == ZatcaDocumentType.SalesInvoice, ct);
        return (company, invoice, customer.NameAr, zatca);
    }

    /// <summary>
    /// (CEEF-226) رمز QR للمرحلة الأولى يُحسب عند الطباعة حين لا يوجد مستند ZATCA.
    ///
    /// كان توليد الرمز مشروطاً بـ<c>ZatcaSettings.IsEnabled</c> كلّه — فمنشأةٌ لم تُفعّل
    /// تكامل المرحلة الثانية تطبع **فواتير ضريبية بلا باركود إطلاقاً**. وقد ثبت ذلك على
    /// قاعدة المستخدم الحقيقية: الفاتورة المرحّلة الوحيدة بلا مستند ZATCA ولا رمز.
    ///
    /// لكن رمز المرحلة الأولى **إلزاميّ منذ 2021-12-04 على كل مسجَّل في ضريبة القيمة
    /// المضافة**، ولا يحتاج شهادة ولا CSID ولا اتصالاً بالهيئة — خمسة حقول تُحسب محلياً
    /// من بيانات الفاتورة نفسها. فربطه بتفعيل المرحلة الثانية خلطٌ بين مرحلتين مستقلّتين.
    ///
    /// <para><b>(CEEF-229 · م2-٢٤) وطابعُه الزمنيّ صار لحظةَ الترحيل المسجَّلة.</b>
    /// كان ظهرَ يوم الإصدار مختلقاً، فصار <c>PostedAtUtc</c> — والمشتري يمسح الرمزَ فيرى
    /// وقتَ شرائه. ويبقى الظهرُ للمستندات التي سبقت وسمَ اللحظة وحدها.</para>
    ///
    /// <para><b>(م2-٢١) وكان قبلها حدّاً مُقَرّاً به:</b>
    /// كان يقول إنّه «نفس اصطلاح <c>ZatcaEInvoiceGenerator</c>» — ولم يعد كذلك: المولّد صار
    /// يعلن لحظةَ توليده الحقيقية المسجَّلة في <c>GeneratedAtUtc</c>. وهذا المسارُ لا يملك
    /// لحظةً يعلنها: <c>SalesInvoice</c> بلا طابعٍ زمنيّ، وهو يُستدعى أصلاً حين **لا مستندَ
    /// ZATCA** — فلا مخزَّنَ يختلف عنه.</para>
    ///
    /// <para>فيبقى ظهرَ يوم الإصدار قيمةً حتميّةً مستقرّة عبر إعادات الطباعة، وهو الأقلّ
    /// سوءاً بلا تغييرِ مخطّط. <b>والإصلاحُ الحقيقيّ ختمُ الفاتورة بلحظة ترحيلها</b> —
    /// بندٌ مفتوحٌ مسجَّل.</para>
    ///
    /// وبلا رقم ضريبيّ لا رمز — لأنّ وسم 2 إلزاميّ ولا يجوز اختلاقه.
    /// </summary>
    internal static string? Phase1QrFallback(
        Company company, DateOnly date, DateTime? postedAtUtc, decimal total, decimal vat)
    {
        if (string.IsNullOrWhiteSpace(company.NameAr) || string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            return null;

        // ‏لحظةُ الترحيل المسجَّلة إن وُجدت — وهي الحقّ. والظهرُ الحتميّ للمستندات التي
        // ‏سبقت وسمَ اللحظة، إذ لا لحظةَ لها وادّعاؤها اختلاق.
        var timestampUtc = postedAtUtc
            ?? date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)), DateTimeKind.Utc);
        return Zatca.ZatcaQrGenerator.GenerateTlvBase64Phase1(
            company.NameAr, company.VatRegistrationNumber, timestampUtc, total, vat);
    }

    private static PrintDocumentKind ResolveSalesKind(SalesInvoice invoice, ZatcaEInvoiceDocument? zatca)
    {
        if (zatca?.TransmissionMode == ZatcaTransmissionMode.Clearance)
            return PrintDocumentKind.StandardSalesInvoice;
        if (invoice.PosShiftId is not null)
            return PrintDocumentKind.SimplifiedSalesInvoice;
        return zatca?.TransmissionMode == ZatcaTransmissionMode.Reporting
            ? PrintDocumentKind.SimplifiedSalesInvoice
            : PrintDocumentKind.StandardSalesInvoice;
    }

    private static string MethodAr(ReceiptMethod method) => method switch
    {
        ReceiptMethod.Cash => "نقداً",
        ReceiptMethod.Bank => "تحويل بنكي",
        _ => method.ToString(),
    };

    private async Task LogAsync(
        long companyId, string username, string entityType, string entityKey, CancellationToken ct) =>
        await audit.LogAsync(
            companyId,
            username,
            "PrintPdf",
            entityType,
            entityKey,
            details: "PDF export",
            ct: ct);

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
