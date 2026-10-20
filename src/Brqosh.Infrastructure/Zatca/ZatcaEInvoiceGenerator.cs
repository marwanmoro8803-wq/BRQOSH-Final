using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>توليد مستند ZATCA من فاتورة مبيعات أو إشعار دائن مرحّل.</summary>
public class ZatcaEInvoiceGenerator(BrqoshDbContext db)
{
    public async Task<ZatcaEInvoiceDocument?> TryGenerateForSalesInvoiceAsync(long salesInvoiceId)
    {
        var invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == salesInvoiceId);
        if (invoice is null || invoice.Status != SalesInvoiceStatus.Posted || invoice.InvoiceNumber is null)
            return null;

        var settings = await GetSettingsAsync(invoice.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == salesInvoiceId))
            throw new AccountingException("مستند ZATCA موجود مسبقاً لهذه الفاتورة.");

        var company = await db.Companies.SingleAsync(c => c.Id == invoice.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            throw new AccountingException("الرقم الضريبي للمنشأة مطلوب لتفعيل ZATCA.");

        var customer = await db.Customers.SingleAsync(c => c.Id == invoice.CustomerId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        return await GenerateAsync(
            invoice.CompanyId, ZatcaDocumentType.SalesInvoice, salesInvoiceId,
            invoice.InvoiceNumber, invoice.InvoiceDate, company, customer,
            // (CEEF-230 · ط1-٤) ‏**المستندُ يخرج بعملته**: الوعاءُ والضريبةُ والإجماليُّ
            // ‏بالعملة الأجنبية، والريالُ يُعلَن في عنصرٍ مستقلٍّ للضريبة وحدَها.
            // ‏وبسعرٍ واحدٍ يعود الجميعُ إلى ما كان حرفاً — فبصمةُ فاتورةٍ قائمة لا تتغيّر.
            invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.ForeignGrandTotal,
            invoice.Lines, mode,
            isCreditNote: false, documentDiscount: invoice.DocumentDiscountAmount,
            currencyCode: invoice.CurrencyCode, exchangeRate: invoice.ExchangeRate,
            vatAmountInSar: invoice.VatAmount,
            prepayments: await PrepaymentsSettledByAsync(invoice));
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) <b>فواتيرُ الدفعات المقدّمة (386) التي تُسوّيها فاتورةُ المغادرة</b> — عرابينُ
    /// حجزها التي صدرت لها فاتورةٌ إلكترونيّة، برقمها وتاريخها ووقتها كما صدرت، ووعائها وضريبتها من قيدها.
    /// </summary>
    private async Task<IReadOnlyList<ZatcaUblInvoiceBuilder.PrepaymentReference>?> PrepaymentsSettledByAsync(SalesInvoice invoice)
    {
        if (invoice.SettlesReservationId is not long reservationId)
            return null;

        var source = $"{Brqosh.Infrastructure.Hospitality.ReservationDepositService.TakenPrefix}{reservationId}";
        var deposits = await db.JournalEntries
            .Where(e => e.CompanyId == invoice.CompanyId && e.SourceDocument == source)
            // ‏سؤالُ وجودٍ لا مبلغ: عربونٌ عُكس قيدُه لا يُسوّى، وقيدُ العكس يرث مصدرَه فلا يُعدّ عربوناً (WhereStanding).
            .WhereStanding(db.JournalEntries)
            .Select(e => e.Id)
            .ToListAsync();
        if (deposits.Count == 0)
            return null;

        var docs = await db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == invoice.CompanyId && d.DocumentType == ZatcaDocumentType.Prepayment
                        && deposits.Contains(d.SourceDocumentId))
            .OrderBy(d => d.Icv)
            .ToListAsync();

        var refs = new List<ZatcaUblInvoiceBuilder.PrepaymentReference>();
        foreach (var doc in docs)
        {
            var (date, net, vat) = await DepositAmountsAsync(doc.SourceDocumentId);
            refs.Add(new(doc.InvoiceNumber, date, TimeOnly.FromDateTime(doc.GeneratedAtUtc + RiyadhOffset), net, vat));
        }

        return refs.Count == 0 ? null : refs;
    }

    /// <summary>(sector-modules#6) تاريخُ قيد العربون وصافيه (دائنُ 2105) وضريبتُه (دائنُ 2106).</summary>
    private async Task<(DateOnly Date, decimal Net, decimal Vat)> DepositAmountsAsync(long depositEntryId)
    {
        var entry = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == depositEntryId);
        var net = entry.Lines.Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers).Sum(l => l.Credit);
        var vat = entry.Lines.Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatOutput).Sum(l => l.Credit);
        return (entry.EntryDate, net, vat);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) <b>فاتورةُ الدفعة المقدّمة (386) عن عربون حجز</b> — يومَ قبضه، فهو نقطةُ
    /// استحقاق الضريبة. رقمُها <c>DEP-</c> ورقمُ قيدها (فريدٌ ومتسلسل)، ومشتريها نزيلُ الحجز، وسطرُها صافي العربون بالنسبة الأساسيّة.
    /// </summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateForPrepaymentAsync(long depositEntryId)
    {
        var entry = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == depositEntryId);
        var prefix = Brqosh.Infrastructure.Hospitality.ReservationDepositService.TakenPrefix;
        if (entry is null || entry.Status != JournalEntryStatus.Posted || entry.EntryNumber is null
            || entry.SourceDocument is null || !entry.SourceDocument.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var settings = await GetSettingsAsync(entry.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == entry.CompanyId && d.DocumentType == ZatcaDocumentType.Prepayment && d.SourceDocumentId == depositEntryId))
            throw new AccountingException("مستند ZATCA موجود مسبقاً لهذا العربون.");

        var reservationId = long.Parse(entry.SourceDocument[prefix.Length..], System.Globalization.CultureInfo.InvariantCulture);
        var reservation = await db.Reservations.SingleAsync(r => r.Id == reservationId);
        var company = await db.Companies.SingleAsync(c => c.Id == entry.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            throw new AccountingException("الرقم الضريبي للمنشأة مطلوب لتفعيل ZATCA.");
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == reservation.GuestCustomerId);
        var mode = customer.SupportsStandardTaxInvoice ? ZatcaTransmissionMode.Clearance : ZatcaTransmissionMode.Reporting;

        var (date, net, vat) = await DepositAmountsAsync(depositEntryId);
        var lines = new List<SalesInvoiceLine>
        {
            new($"دفعة مقدّمة — حجز {reservation.ReservationNumber}", 1m, net, null),
        };

        return await GenerateAsync(
            entry.CompanyId, ZatcaDocumentType.Prepayment, depositEntryId,
            $"DEP-{entry.EntryNumber}", date, company, customer,
            net, vat, net + vat, lines, mode, isCreditNote: false, isPrepayment: true);
    }

    public async Task<ZatcaEInvoiceDocument?> TryGenerateForCreditNoteAsync(long creditNoteId)
    {
        var note = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == creditNoteId);
        if (note is null || note.Status != CreditNoteStatus.Posted || note.NoteNumber is null)
            return null;

        var settings = await GetSettingsAsync(note.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.DocumentType == ZatcaDocumentType.CreditNote && d.SourceDocumentId == creditNoteId))
            throw new AccountingException("مستند ZATCA موجود مسبقاً لهذا الإشعار.");

        var company = await db.Companies.SingleAsync(c => c.Id == note.CompanyId);
        var customer = await db.Customers.SingleAsync(c => c.Id == note.CustomerId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        return await GenerateAsync(
            note.CompanyId, ZatcaDocumentType.CreditNote, creditNoteId,
            note.NoteNumber, note.NoteDate, company, customer,
            // (جاهزيّةُ الإطلاق · ح-٣٥) ‏**والإشعارُ يخرج بعملتِه كفاتورتِه.**
            //
            // ‏كان يُمرّر الريالَ ويُسقِط **العملةَ والسعرَ وضريبةَ الريال
            // ‏وخصمَ المستند** — **أربعةً ممّا تُمرّرُه الفاتورةُ أعلاه**.
            // ‏**فتخرج سطورٌ لا تجمع إلى إجماليّها** متى كان ثَمّ خصمٌ،
            // ‏**ويُعلَن مرتجعٌ بالريال عن توريدٍ أُعلِن بالدولار**.
            note.ForeignSubtotal, note.ForeignVatAmount, note.ForeignGrandTotal,
            note.Lines, mode, isCreditNote: true,
            documentDiscount: note.DocumentDiscountAmount,
            currencyCode: note.CurrencyCode, exchangeRate: note.ExchangeRate,
            vatAmountInSar: note.VatAmount,
            issueReason: note.Reason);
    }

    public async Task<ZatcaEInvoiceDocument?> TryGenerateForProgressBillingAsync(long progressBillingId)
    {
        var billing = await db.ProgressBillings.SingleOrDefaultAsync(b => b.Id == progressBillingId);
        if (billing is null || billing.Status != ProgressBillingStatus.Posted || billing.BillingNumber is null)
            return null;

        var settings = await GetSettingsAsync(billing.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.DocumentType == ZatcaDocumentType.ProgressBilling && d.SourceDocumentId == progressBillingId))
            throw new AccountingException("مستند ZATCA موجود مسبقاً لهذا المستخلص.");

        var company = await db.Companies.SingleAsync(c => c.Id == billing.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            throw new AccountingException("الرقم الضريبي للمنشأة مطلوب لتفعيل ZATCA.");

        var customer = await db.Customers.SingleAsync(c => c.Id == billing.CustomerId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        var lines = new List<SalesInvoiceLine>
        {
            new(billing.PeriodDescription, 1m, billing.TaxableAmount, null),
        };

        return await GenerateAsync(
            billing.CompanyId, ZatcaDocumentType.ProgressBilling, progressBillingId,
            billing.BillingNumber, billing.BillingDate, company, customer,
            billing.TaxableAmount, billing.VatAmount, billing.TotalAmount, lines, mode, isCreditNote: false);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>فاتورةُ ضريبةٍ عن بيع أصلٍ ثابت.</b>
    ///
    /// <para><b>وبيعُ أصلٍ توريدٌ خاضعٌ نظاماً</b>، وكان الاستبعادُ يُقيّد ضريبةَ المخرجات
    /// <b>بلا مستندٍ خلفَها</b>. والقياسُ على <see cref="TryGenerateForProgressBillingAsync"/>:
    /// مستندٌ ليس فاتورةَ مبيعاتٍ، بسطرٍ واحدٍ مُركَّبٍ من بيانات المصدر.</para>
    ///
    /// <para><b>ويقرأ ما سُجِّل على الأصل لا ما يُمرَّر إليه</b> — فيُنتج المستندَ نفسَه
    /// سواءٌ استُدعي داخل معاملة الترحيل أو بعدَها.</para>
    ///
    /// <para><b>والصامتُ هنا صوابٌ</b>: <c>null</c> حين لا بيعَ مسجَّلاً أو لا فوترةَ
    /// إلكترونيّةً مفعَّلة — كإخوته. <b>والرميةُ للمكرَّر وحدَه</b>، فمستندان لأصلٍ واحدٍ
    /// يستهلكان عدّادَين وتنكسر سلسلةُ البصمات.</para>
    /// </summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateForAssetDisposalAsync(long fixedAssetId)
    {
        var asset = await db.FixedAssets.IgnoreQueryFilters()
            .SingleOrDefaultAsync(a => a.Id == fixedAssetId);

        // ‏**ولا فاتورةَ لأصلٍ لم يُسجَّل بيعُه**: خردةٌ بلا مقابل، أو منشأةٌ خارج النطاق.
        if (asset?.DisposalInvoiceNumber is null || asset.DisposalProceeds is not decimal proceeds)
            return null;

        var settings = await GetSettingsAsync(asset.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == asset.CompanyId &&
                d.DocumentType == ZatcaDocumentType.AssetDisposal &&
                d.SourceDocumentId == fixedAssetId))
            throw new AccountingException("مستند ZATCA موجود مسبقاً لبيع هذا الأصل.");

        var company = await db.Companies.SingleAsync(c => c.Id == asset.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            throw new AccountingException("الرقم الضريبي للمنشأة مطلوب لتفعيل ZATCA.");

        // ‏**والمشتري قد يكون مجهولاً** — فيُفوتَر مبسَّطةً بـ«عميل نقدي»، كما تفعل نقطةُ
        // ‏البيع. وهو مسارٌ نظاميٌّ للبيع النقديّ لمستهلكٍ لا تحايلٌ على هوية المشتري.
        var customer = asset.DisposalBuyerCustomerId is long buyerId
            ? await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == buyerId)
            : await WalkInCustomerAsync(asset.CompanyId);

        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        var category = asset.DisposalVatCategory ?? VatCategory.Standard;
        var reason = VatExemptionReason.Find(asset.DisposalVatExemptionCode);
        var lines = new List<SalesInvoiceLine>
        {
            new($"بيع أصل ثابت {asset.Code} — {asset.NameAr}", 1m, proceeds, null,
                category, asset.DisposalVatExemptionCode, reason?.ArabicText),
        };

        // ‏**والضريبةُ تُحسب هنا كما تُحسب في القيد** — من الفئة والمقابل، لا من عمودٍ
        // ‏مخزَّنٍ يُوازي القيدَ فيفترق عنه يوماً.
        var vat = Math.Round(
            proceeds * category.RateFor(VatCalculator.SaudiVatRate), 2, MidpointRounding.AwayFromZero);

        return await GenerateAsync(
            asset.CompanyId, ZatcaDocumentType.AssetDisposal, fixedAssetId,
            asset.DisposalInvoiceNumber, await DisposalDateOfAsync(asset), company, customer,
            proceeds, vat, proceeds + vat, lines, mode, isCreditNote: false);
    }

    /// <summary>
    /// تاريخُ الفاتورة هو تاريخُ قيد الاستبعاد — <b>لا تاريخُ اليوم</b>: استبعادٌ يُرحَّل
    /// بتاريخٍ سابقٍ تخرج فاتورتُه بتاريخه، <b>وإلّا وقع المستندُ في فترةٍ ضريبيّةٍ غير
    /// فترة قيده</b> فاختلف الإقرارُ عن الدفتر.
    /// </summary>
    private async Task<DateOnly> DisposalDateOfAsync(Brqosh.Domain.Assets.FixedAsset asset) =>
        asset.DisposalJournalEntryId is long entryId
            ? await db.JournalEntries.Where(e => e.Id == entryId).Select(e => e.EntryDate).SingleAsync()
            : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// «عميل نقدي» — <b>والقاعدةُ والاسمُ من <c>PosSaleService</c> حرفاً</b>، فلا يُنشأ
    /// عميلان بالمعنى نفسِه في منشأةٍ واحدة.
    /// </summary>
    private async Task<Customer> WalkInCustomerAsync(long companyId)
    {
        var walkIn = await db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.NameAr == "عميل نقدي");
        if (walkIn is null)
        {
            walkIn = Customer.Create(companyId, "عميل نقدي", "");
            db.Customers.Add(walkIn);
            await db.SaveChangesAsync();
        }

        return walkIn;
    }

    /// <summary>إشعار دائن ZATCA عند إلغاء مستخلص أعمال مرحّل.</summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateCancellationForProgressBillingAsync(long progressBillingId)
    {
        var billing = await db.ProgressBillings.SingleOrDefaultAsync(b => b.Id == progressBillingId);
        if (billing is null || billing.BillingNumber is null)
            return null;

        if (billing.Status is not (ProgressBillingStatus.Posted or ProgressBillingStatus.Cancelled))
            return null;

        var settings = await GetSettingsAsync(billing.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == billing.CompanyId &&
                d.DocumentType == ZatcaDocumentType.BillingCancel &&
                d.SourceDocumentId == progressBillingId))
            return null;

        var company = await db.Companies.SingleAsync(c => c.Id == billing.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            return null;

        var customer = await db.Customers.SingleAsync(c => c.Id == billing.CustomerId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        var lines = new List<SalesInvoiceLine>
        {
            new(billing.PeriodDescription, 1m, billing.TaxableAmount, null),
        };

        var cnNumber = $"CN-CANCEL-PB-{billing.BillingNumber}";
        return await GenerateAsync(
            billing.CompanyId, ZatcaDocumentType.BillingCancel, progressBillingId,
            cnNumber, billing.CancelledAt ?? billing.BillingDate, company, customer,
            // (جاهزيّةُ الإطلاق · ح-٣٦) ‏**وسببُ الإلغاء يُكتب كما يُكتب
            // ‏للفاتورة والإشعار** — وكان المستخلصُ وحدَه بلا سببٍ.
            // ‏**والسببُ حقلٌ يقرؤه المراجع** لا تزيينٌ: إلغاءٌ بلا سببٍ
            // ‏توريدٌ رُفِع من الإقرار بلا تفسير.
            billing.TaxableAmount, billing.VatAmount, billing.TotalAmount, lines, mode,
            isCreditNote: true,
            issueReason: $"إلغاء مستخلص {billing.BillingNumber}");
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) <b>إشعارٌ دائنٌ بردّ عربون حجزٍ أُلغي</b> — يُشير إلى فاتورة دفعته (386)
    /// بمبلغها نفسِه، فتخرج ضريبتُها من فترة الردّ كما خرجت من الأستاذ. وعربونٌ بلا فاتورةِ دفعةٍ لا إشعارَ له.
    /// </summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateRefundForPrepaymentAsync(long depositEntryId, DateOnly refundDate)
    {
        var prepayment = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.DocumentType == ZatcaDocumentType.Prepayment && d.SourceDocumentId == depositEntryId);
        if (prepayment is null)
            return null;

        var settings = await GetSettingsAsync(prepayment.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;
        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == prepayment.CompanyId && d.DocumentType == ZatcaDocumentType.DepositRefund
                && d.SourceDocumentId == depositEntryId))
            throw new AccountingException("إشعار ردّ هذا العربون صادر مسبقاً.");

        var entry = await db.JournalEntries.SingleAsync(e => e.Id == depositEntryId);
        var prefix = Brqosh.Infrastructure.Hospitality.ReservationDepositService.TakenPrefix;
        var reservationId = long.Parse(entry.SourceDocument![prefix.Length..], System.Globalization.CultureInfo.InvariantCulture);
        var reservation = await db.Reservations.SingleAsync(r => r.Id == reservationId);
        var company = await db.Companies.SingleAsync(c => c.Id == prepayment.CompanyId);
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == reservation.GuestCustomerId);
        var mode = customer.SupportsStandardTaxInvoice ? ZatcaTransmissionMode.Clearance : ZatcaTransmissionMode.Reporting;

        var (_, net, vat) = await DepositAmountsAsync(depositEntryId);
        var lines = new List<SalesInvoiceLine>
        {
            new($"ردّ دفعة مقدّمة — حجز {reservation.ReservationNumber}", 1m, net, null),
        };

        return await GenerateAsync(
            prepayment.CompanyId, ZatcaDocumentType.DepositRefund, depositEntryId,
            $"CN-{prepayment.InvoiceNumber}", refundDate, company, customer,
            net, vat, net + vat, lines, mode, isCreditNote: true,
            issueReason: "إلغاء الحجز وردّ العربون");
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٦١) <b>إشعارٌ دائنٌ عند عكس استبعاد أصلٍ بيع بفاتورة</b> — بمبالغ الفاتورة نفسِها ومشتريها، ومرجعُه هي.
    /// ‏و<c>null</c> إن لم تكن للبيع فاتورةٌ إلكترونيّة أو كانت الفوترةُ معطَّلة — والمستدعي يقرّر.
    /// </summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateCancellationForAssetDisposalAsync(
        long fixedAssetId, DateOnly reversalDate, string reason)
    {
        var asset = await db.FixedAssets.IgnoreQueryFilters().SingleOrDefaultAsync(a => a.Id == fixedAssetId);
        if (asset?.DisposalInvoiceNumber is null || asset.DisposalProceeds is not decimal proceeds)
            return null;

        var settings = await GetSettingsAsync(asset.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (!await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == asset.CompanyId && d.DocumentType == ZatcaDocumentType.AssetDisposal
                && d.SourceDocumentId == fixedAssetId))
            return null;
        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == asset.CompanyId && d.DocumentType == ZatcaDocumentType.DisposalCancel
                && d.SourceDocumentId == fixedAssetId))
            throw new AccountingException($"إشعارُ إلغاء بيع الأصل {asset.Code} صادرٌ مسبقاً.");

        var company = await db.Companies.SingleAsync(c => c.Id == asset.CompanyId);
        var customer = asset.DisposalBuyerCustomerId is long buyerId
            ? await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == buyerId)
            : await WalkInCustomerAsync(asset.CompanyId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        var category = asset.DisposalVatCategory ?? VatCategory.Standard;
        var exemption = VatExemptionReason.Find(asset.DisposalVatExemptionCode);
        var lines = new List<SalesInvoiceLine>
        {
            new($"إلغاء بيع أصل ثابت {asset.Code} — {asset.NameAr}", 1m, proceeds, null,
                category, asset.DisposalVatExemptionCode, exemption?.ArabicText),
        };
        var vat = Math.Round(
            proceeds * category.RateFor(VatCalculator.SaudiVatRate), 2, MidpointRounding.AwayFromZero);

        return await GenerateAsync(
            asset.CompanyId, ZatcaDocumentType.DisposalCancel, fixedAssetId,
            $"CN-CANCEL-{asset.DisposalInvoiceNumber}", reversalDate, company, customer,
            proceeds, vat, proceeds + vat, lines, mode, isCreditNote: true, issueReason: reason);
    }

    /// <summary>إشعار دائن ZATCA عند إلغاء إشعار دائن مرحّل — دون قيد محاسبي منفصل.</summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateCancellationForCreditNoteAsync(long creditNoteId)
    {
        var note = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == creditNoteId);
        if (note is null || note.NoteNumber is null)
            return null;

        if (note.Status is not (CreditNoteStatus.Posted or CreditNoteStatus.Cancelled))
            return null;

        var settings = await GetSettingsAsync(note.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == note.CompanyId &&
                d.DocumentType == ZatcaDocumentType.DebitNote &&
                d.SourceDocumentId == creditNoteId))
            return null;

        var company = await db.Companies.SingleAsync(c => c.Id == note.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            return null;

        var customer = await db.Customers.SingleAsync(c => c.Id == note.CustomerId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        var dnNumber = $"DN-CANCEL-{note.NoteNumber}";
        return await GenerateAsync(
            note.CompanyId, ZatcaDocumentType.DebitNote, creditNoteId,
            dnNumber, note.CancelledAt ?? note.NoteDate, company, customer,
            // (جاهزيّةُ الإطلاق · ح-٣٦) ‏**ومستندُ الإلغاء يحمل ما يحملُه
            // ‏مصدرُه** — عملةً وسعراً وخصمَ مستندٍ وضريبةَ ريال.
            //
            // ‏وإلّا **خرجت سطورٌ لا تجمع إلى إجماليّها** متى كان ثَمّ خصمٌ
            // ‏(ويمسكُه `UblStructureValidator` في المستودع نفسِه)، **وأُعلِن إلغاءٌ
            // ‏بالريال عن توريدٍ أُعلِن بالدولار**.
            note.ForeignSubtotal, note.ForeignVatAmount, note.ForeignGrandTotal, note.Lines, mode,
            isCreditNote: false, isDebitNote: true,
            documentDiscount: note.DocumentDiscountAmount,
            currencyCode: note.CurrencyCode, exchangeRate: note.ExchangeRate,
            vatAmountInSar: note.VatAmount,
            issueReason: $"إلغاء إشعار دائن {note.NoteNumber}");
    }

    /// <summary>إشعار دائن ZATCA عند إلغاء فاتورة — دون قيد محاسبي منفصل (العكس يتم بعكس القيد).</summary>
    public async Task<ZatcaEInvoiceDocument?> TryGenerateCancellationForSalesInvoiceAsync(long salesInvoiceId)
    {
        var invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == salesInvoiceId);
        if (invoice is null || invoice.InvoiceNumber is null)
            return null;

        // يُستدعى بعد MarkCancelled() — الحالة تكون ملغاة وليس مرحّلة.
        if (invoice.Status is not (SalesInvoiceStatus.Posted or SalesInvoiceStatus.Cancelled))
            return null;

        var settings = await GetSettingsAsync(invoice.CompanyId);
        if (settings is null || !settings.IsEnabled) return null;

        if (await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == invoice.CompanyId &&
                d.DocumentType == ZatcaDocumentType.InvoiceCancel &&
                d.SourceDocumentId == salesInvoiceId))
            return null;

        var company = await db.Companies.SingleAsync(c => c.Id == invoice.CompanyId);
        if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
            return null;

        var customer = await db.Customers.SingleAsync(c => c.Id == invoice.CustomerId);
        var mode = customer.SupportsStandardTaxInvoice
            ? ZatcaTransmissionMode.Clearance
            : ZatcaTransmissionMode.Reporting;

        var cnNumber = $"CN-CANCEL-{invoice.InvoiceNumber}";
        return await GenerateAsync(
            invoice.CompanyId, ZatcaDocumentType.InvoiceCancel, salesInvoiceId,
            cnNumber, invoice.CancelledAt ?? invoice.InvoiceDate, company, customer,
            // (جاهزيّةُ الإطلاق · ح-٣٦) ‏**ومستندُ الإلغاء يحمل ما يحملُه
            // ‏مصدرُه** — عملةً وسعراً وخصمَ مستندٍ وضريبةَ ريال.
            //
            // ‏وإلّا **خرجت سطورٌ لا تجمع إلى إجماليّها** متى كان ثَمّ خصمٌ
            // ‏(ويمسكُه `UblStructureValidator` في المستودع نفسِه)، **وأُعلِن إلغاءٌ
            // ‏بالريال عن توريدٍ أُعلِن بالدولار**.
            invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.ForeignGrandTotal,
            invoice.Lines, mode, isCreditNote: true,
            documentDiscount: invoice.DocumentDiscountAmount,
            currencyCode: invoice.CurrencyCode, exchangeRate: invoice.ExchangeRate,
            vatAmountInSar: invoice.VatAmount,
            issueReason: $"إلغاء فاتورة مبيعات {invoice.InvoiceNumber}");
    }

    /// <summary>الرياض ‏+03:00‎ ثابتةً — لا توقيتَ صيفيّ في السعودية.</summary>
    private static readonly TimeSpan RiyadhOffset = TimeSpan.FromHours(3);

    /// <summary>
    /// (CEEF-230 · ط1-١) ‏يحلّ رمزَ UN/ECE لكلّ سطر.
    ///
    /// <para><b>ويُحلّ من الصنف حين لا يذكر السطرُ وحدةً</b> — وهي الحالةُ السائدة في كلّ
    /// قاعدةٍ قائمة. فالتصحيحُ يشمل ما مضى، لا ما يُدخَل من اليوم وحده.</para>
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveUnitCodesAsync(
        long companyId, IReadOnlyList<SalesInvoiceLine> lines)
    {
        var converter = new Brqosh.Infrastructure.Inventory.UnitConversionService(db);
        var productIds = lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).ToList();
        var units = await db.Products.IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Unit);

        var codes = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var productUnit = line.ProductId is long pid && units.TryGetValue(pid, out var u) ? u : null;
            codes.Add(await converter.UnCodeForAsync(companyId, line.UnitCode, productUnit));
        }

        return codes;
    }

    private async Task<ZatcaEInvoiceDocument> GenerateAsync(
        long companyId, ZatcaDocumentType docType, long sourceId,
        string number, DateOnly date, Company company,
        Customer customer,
        decimal subtotal, decimal vat, decimal total,
        IReadOnlyList<SalesInvoiceLine> lines, ZatcaTransmissionMode mode, bool isCreditNote,
        bool isDebitNote = false, string? issueReason = null, decimal documentDiscount = 0m,
        string currencyCode = "SAR", decimal exchangeRate = 1m, decimal? vatAmountInSar = null,
        bool isPrepayment = false, IReadOnlyList<ZatcaUblInvoiceBuilder.PrepaymentReference>? prepayments = null)
    {
        // (CEEF-229 · م2-١٣أ) **هويةُ البائع تُفحص قبل استهلاك أيّ عدّاد.**
        //
        // كان الفحصُ الوحيد هنا هو الرقم الضريبيّ. و`Company.RegulatoryGaps()` يعرف تماماً ما
        // ينقص وبرسائل عربية جاهزة — لكنّه لم يكن يُقرأ إلا في شاشة الإعدادات **عرضاً**، وفي
        // خدمة الإرسال **وفي وضع الإنتاج وحده**. و`DatabaseInitializer` لا يملأ تلك الحقول
        // أصلاً، فالمنشأةُ الجديدة ناقصةٌ بالتعريف.
        //
        // فمنشأةٌ تُفعّل الفوترة قبل إكمال بياناتها كانت تُصدر مستندات ينقصها طرفُ البائع
        // كاملاً (PartyIdentification و PostalAddress و PartyLegalEntity) — ترفضها الهيئة
        // عند التحقّق البنيويّ قبل أن تقرأ أرقامها.
        //
        // **ولا يُصلحه ضبطُ الإعدادات لاحقاً:** كلُّ مستندٍ خاطئ استهلك عدّاداً تسلسلياً (ICV)
        // وصارت بصمتُه PreviousHash لما بعده — فالسلسلة تتلوّث بأثرٍ رجعيّ ويُطلب إعادةُ
        // إصدار ما بعدها. والرفضُ عند الباب أرحمُ.
        //
        // والموضعُ مقصود: **قبل** AcquireIcvLockAsync وقبل قراءة العدّاد، فلا يُستهلك رقمٌ
        // لمستندٍ لن يُقبل.
        var gaps = company.RegulatoryGaps();
        if (gaps.Count > 0)
            throw new AccountingException(
                "لا يمكن إصدار فاتورة إلكترونية قبل إكمال بيانات المنشأة:\n- " + string.Join("\n- ", gaps)
                + "\n\nأكمِلها من: الإعدادات ← بيانات المنشأة.");

        // (CEEF-229 · م2-١٣ب) **بلا شهادةٍ لا يُصدَر مستند — لا يُصدَر غيرَ موقَّع بصمت.**
        //
        // كان `TrySignWithResult` يُعيد null بلا شكوى حين لا يجد ملفَّ شهادةٍ أو مفتاح، فيسلك
        // المولّد فرعاً بديلاً صامتاً: مستندٌ بلا توقيع، ورمزُ QR من الطور الأوّل بلا ختمٍ
        // تشفيريّ، و`InvoiceHash` محسوبٌ بـ **خوارزمية أخرى** — `Sha256Base64` على البايتات
        // الخام بدل `ComputeInvoiceHash` الذي يُقنّن المستند بعد نزع UBLExtensions والتوقيع
        // ومرجع QR. ولا يمكن أن يتّفق الرقمان.
        //
        // وتلك البصمة تصير PreviousInvoiceHash لما بعده، **فتنكسر السلسلة عند ذلك المفصل**
        // انكساراً لا تستطيع الهيئة مطابقته. ثمّ ترفع خدمةُ الإرسال
        // `SignedXmlFilePath ?? XmlFilePath` — أي ترفع الملفَّ غيرَ الموقَّع.
        //
        // ولا يوجد في النظام مفهومُ «طورٍ أوّل» أصلاً: لا إعدادَ ولا حقل. فالانحدارُ الصامت
        // ليس وضعاً مشروعاً، ولا يملك المالكُ ما يخبره أيَّهما هو فيه.
        var zatcaSettings = await GetSettingsAsync(companyId);
        if (!ZatcaXmlSigner.CanSign(zatcaSettings))
            throw new AccountingException(
                "لا يمكن إصدار فاتورة إلكترونية بلا شهادة توقيع. أكمِل تسجيل الشهادة (CSID) "
                + "من إعدادات الفوترة الإلكترونية — فالمستند غير الموقَّع ترفضه الهيئة، "
                + "ويكسر بصمتُه سلسلةَ الفواتير التالية.");

        // (CEEF-229 · م1-١ب) **ومشترٍ ناقصُ الهوية لا تُصدَر له فاتورةٌ قياسية.**
        //
        // حقولُ هوية المشتري أُضيفت في هذه الدفعة، والعملاءُ القائمون أعمدتُهم فارغة — فلا
        // يُطالبون بها إلا إن فُتحت شاشتُهم. فبلا هذا الحارس تبقى فواتيرُهم القياسية تخرج
        // ناقصةَ الطرف المشتري كما كانت، ويكون الإصلاحُ نصفَ إصلاح.
        //
        // والسببُ نفسه الذي يوجب حارسَ المنشأة الناقصة أعلاه: المستندُ الخاطئ يستهلك عدّاداً
        // تسلسلياً وتصير بصمتُه أساسَ ما بعده، فلا يُصلحه ملءُ البيانات لاحقاً.
        //
        // والمبسّطة لا تُمَسّ: `StandardInvoiceGaps` تُعيد فارغاً لمن لا رقمَ ضريبيّ له.
        var buyerGaps = customer.StandardInvoiceGaps();
        if (buyerGaps.Count > 0)
            throw new AccountingException(
                $"لا يمكن إصدار فاتورة قياسية للعميل «{customer.NameAr}» قبل إكمال بياناته:\n- "
                + string.Join("\n- ", buyerGaps)
                + "\n\nأكمِلها من: العملاء ← تعديل العميل.");

        // (CEEF-229 · م1-١ب/الشقّ الثاني) **وسطرٌ غيرُ خاضعٍ للنسبة الأساسية يقول سببَه.**
        //
        // `BR-KSA-CL-06`: فئةٌ `Z` أو `E` أو `O` بلا رمز سبب إعفاء (BT-121) من قائمة السعودية
        // ترفضها الهيئة. وشاشةُ المبيعات تعرض الفئات الأربع منذ اليوم الأول، ولم يكن في
        // المستودع كلِّه ذكرٌ لـ`TaxExemption` — فمن باع سلعةً مصدَّرةً أو خدمةً معفاة أنتج
        // مستنداً مرفوضاً، وقد استهلك عدّاداً تسلسلياً وبنى بصمةَ ما بعده عليه.
        //
        // ويُفحص هنا آخِراً: العطلُ المستنديّ الأخصّ بعد المنشأة والشهادة والمشتري.
        var exemptionGaps = VatExemptionGaps.For(lines);
        if (exemptionGaps.Count > 0)
            throw new AccountingException(
                $"لا يمكن إصدار المستند «{number}» — أسبابُ الإعفاء ناقصة:\n- "
                + string.Join("\n- ", exemptionGaps)
                + "\n\nأكمِلها من سطور المستند قبل الترحيل.");


        // (CEEF-229 · Z-4) قفل استشاريّ قبل قراءة العدّاد — النمط نفسه في JournalEntryNumberGenerator.
        //
        // كان `MaxAsync + 1` بلا قفل وبلا فهرس فريد على `(CompanyId, Icv)`. فصندوقان في فرعين
        // يرحّلان في اللحظة نفسها والعدّاد عند 417: كلاهما يقرأ 417 ويكتب 418 **بنفس PIH**،
        // وتُقبل الكتابتان. السلسلة تتفرّع، ولا يُصلَح ذلك بأثر رجعيّ: الهيئة ترفض الثانية،
        // وإصلاحها يقتضي إعادة إصدار كل ما بعدها.
        //
        // سلسلة سجلّ التدقيق أخذت قفلاً **وفهرساً فريداً**؛ سلسلة ZATCA لم تأخذ أياً منهما.
        await AcquireIcvLockAsync(companyId, CancellationToken.None);

        var icv = (await db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == companyId)
            .MaxAsync(d => (int?)d.Icv) ?? 0) + 1;
        var previousHash = await db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == companyId)
            .OrderByDescending(d => d.Icv)
            .Select(d => d.InvoiceHash)
            .FirstOrDefaultAsync() ?? ZatcaHashHelper.GenesisPreviousInvoiceHash();

        var uuid = Guid.NewGuid();
        // (CEEF-229 · م2-٢١) **لحظةُ الإصدار حقيقيةٌ، والمستندُ ورمزُه يقولانها معاً.**
        //
        // كان `IssueTime` ثابتاً `"12:00:00"` بلا إزاحة — تقرؤه الهيئةُ توقيتاً محلياً —
        // بينما يُبنى وسمُ QR الثالث من ظهرٍ **بتوقيت UTC** فيقول `12:00:00Z`، أي الثالثةَ
        // عصراً بالرياض. **فالمستندُ الواحد كان يعلن لحظتين تفصلهما ثلاث ساعات.**
        //
        // ولم تكن اللحظةُ الحقيقية مفقودة: `GeneratedAtUtc` يسجّلها على المستند منذ البداية،
        // والمولّدُ وحده لا يقولها. فتُؤخذ مرّةً واحدة ويُشتقّ منها الطرفان — فيستحيل
        // اختلافُهما بناءً، لا بحارسٍ يتذكّره أحد.
        //
        // والرياض `+03:00` بلا توقيتٍ صيفيّ، فالإزاحة ثابتةٌ ولا تحتاج قاعدةَ مناطق.
        var generatedAtUtc = DateTime.UtcNow;
        var issueTime = TimeOnly.FromDateTime(generatedAtUtc + RiyadhOffset);
        var ts = DateTime.SpecifyKind(date.ToDateTime(issueTime) - RiyadhOffset, DateTimeKind.Utc);
        var (billingRefNumber, billingRefUuid) = await ResolveBillingReferenceAsync(
            companyId, docType, sourceId, isCreditNote, isDebitNote);

        var qrPhase1 = ZatcaQrGenerator.GenerateTlvBase64Phase1(
            company.NameAr, company.VatRegistrationNumber, ts, total, vat);

        var ublData = new ZatcaUblInvoiceBuilder.InvoiceData(
            number, uuid, date, issueTime, company.NameAr, company.VatRegistrationNumber,
            customer.NameAr, string.IsNullOrWhiteSpace(customer.VatRegistrationNumber)
                ? null : customer.VatRegistrationNumber,
            subtotal, vat, total, lines, icv.ToString(), previousHash, qrPhase1, isCreditNote, isDebitNote,
            IsSimplified: !customer.SupportsStandardTaxInvoice,
            BillingReferenceInvoiceNumber: billingRefNumber,
            BillingReferenceUuid: billingRefUuid,
            IssueReason: issueReason,
            Seller: new ZatcaUblInvoiceBuilder.SellerIdentity(
                company.RegistrationName, company.CommercialRegistrationNumber, company.NationalAddress),

            // (CEEF-229 · م1-١ب) هويةُ المشتري للقياسية وحدها — والمبسّطة بلا مشترٍ معرَّف.
            Buyer: customer.SupportsStandardTaxInvoice
                ? new ZatcaUblInvoiceBuilder.BuyerIdentity(customer.RegistrationName, customer.NationalAddress)
                : null,

            // (CEEF-230 · ط1-٢) ‏خصمُ المستند — يخرج `AllowanceCharge` لكلّ وعاءٍ ضريبيّ.
            DocumentDiscount: documentDiscount,

            // (CEEF-230 · ط1-١) ‏رمزُ الوحدة لكلّ سطر — من وحدة السطر إن ذُكرت، وإلا من
            // ‏وحدة الصنف النصّية عبر جدول المرادفات. **وما لا يُعرَف يخرج PCE مُعلَناً.**
            LineUnitCodes: await ResolveUnitCodesAsync(companyId, lines),

            // (CEEF-230 · ط1-٤) ‏عملةُ المستند وسعرُها، والضريبةُ بالريال للإفصاح BT-111.
            CurrencyCode: currencyCode,
            ExchangeRate: exchangeRate,
            VatAmountInSar: vatAmountInSar,

            // ‏(sector-modules#6) ‏فاتورةُ الدفعة المقدّمة (386)، وتسويةُ ما سبقها في فاتورة المغادرة.
            IsPrepayment: isPrepayment,
            Prepayments: prepayments);

        var xmlBytes = ZatcaUblInvoiceBuilder.BuildXml(ublData);
        var settings = await GetSettingsAsync(companyId);
        var signResult = ZatcaXmlSigner.TrySignWithResult(xmlBytes, settings);

        string qr;
        byte[]? signedBytes;
        string invoiceHash;

        if (signResult is not null)
        {
            invoiceHash = signResult.InvoiceHashBase64;
            qr = ZatcaQrGenerator.GenerateTlvBase64Phase2(
                company.NameAr, company.VatRegistrationNumber, ts, total, vat,
                signResult.InvoiceHashBase64, signResult.SignatureBase64,
                signResult.PublicKeyDerBase64, signResult.CertificateSignatureBase64);
            signedBytes = ZatcaXmlSigner.ReplaceQrInSignedXml(signResult.SignedXml, qr);
        }
        else
        {
            invoiceHash = ZatcaHashHelper.Sha256Base64(xmlBytes);
            qr = qrPhase1;
            signedBytes = null;
        }

        var signedPath = signedBytes is not null ? SaveXml(companyId, number, signedBytes, signed: true) : null;
        var xmlPath = SaveXml(companyId, number, xmlBytes, signed: false);

        // ‏(CEEF-233 · ظ-١) **واللحظةُ المقروءةُ أعلاه تُمرَّر — فلا يقرؤها الكيانُ ثانيةً.**
        // ‏وبها يستحيل أن يُعلن المستندُ لحظةً ويُسجّل أخرى، **بناءً لا بحارس**.
        var doc = ZatcaEInvoiceDocument.Create(
            companyId, docType, sourceId, number, uuid, icv, previousHash, invoiceHash,
            mode, qr, xmlPath, signedPath, generatedAtUtc);

        db.ZatcaEInvoiceDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    private static string SaveXml(long companyId, string invoiceNumber, byte[] bytes, bool signed)
    {
        // جذر البيانات الموحّد — يحترم BRQOSH_DATA_ROOT (CEEF-202/203).
        var dir = Path.Combine(
            Persistence.BrqoshDataRoot.Path(), "zatca", companyId.ToString(), invoiceNumber);
        Directory.CreateDirectory(dir);
        var file = signed ? $"{invoiceNumber}.signed.xml" : $"{invoiceNumber}.xml";
        var path = Path.Combine(dir, file);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// منفذ اختبار لحلّ المرجع الأصليّ. مكشوفٌ عمداً: حلُّ المرجع كان يخمّن الجدول من معرّفٍ
    /// خام، وهو عطلٌ **صامت** لا يُنتج استثناءً ولا رقماً ظاهراً — فلا يُختبَر إلا مباشرةً.
    /// </summary>
    internal Task<(string? Number, Guid? Uuid)> ResolveBillingReferenceForTestsAsync(
        long companyId, ZatcaDocumentType docType, long sourceId) =>
        ResolveBillingReferenceAsync(companyId, docType, sourceId, isCreditNote: true, isDebitNote: false);

    private async Task<(string? Number, Guid? Uuid)> ResolveBillingReferenceAsync(
        long companyId, ZatcaDocumentType docType, long sourceId, bool isCreditNote, bool isDebitNote)
    {
        if (!isCreditNote && !isDebitNote)
            return (null, null);

        // (CEEF-229 · Z-3) الإرسال حسب **نوع المستند** لا بالتخمين، وبتصفية الشركة دائماً.
        //
        // كان الفرع الأول يبحث في `db.CreditNotes` بمعرّفٍ قد يكون معرّف **فاتورة** (مسار إلغاء
        // الفاتورة كان يُسجَّل بالنوع CreditNote نفسه). والتسلسلان مستقلّان ويبدآن من واحد،
        // فالتصادم شبه مؤكّد: إشعارٌ بقيمة فاتورةٍ يُنسب إلى فاتورةٍ أخرى لعميل آخر — ويُقبل
        // لدى الهيئة شكلاً ويُفسد الدفاتر جوهراً. ولا تصفية `CompanyId` في أيّ من الاستعلامين.
        switch (docType)
        {
            // إشعار دائن حقيقيّ — مصدره إشعار، ومرجعه فاتورته الأصلية.
            case ZatcaDocumentType.CreditNote when !isDebitNote:
            {
                var note = await db.CreditNotes.SingleOrDefaultAsync(
                    n => n.Id == sourceId && n.CompanyId == companyId);
                if (note is null) break;
                return await SalesInvoiceReferenceAsync(companyId, note.OriginalInvoiceId);
            }

            // إشعار ناتج عن إلغاء فاتورة — مصدره الفاتورة نفسها.
            case ZatcaDocumentType.InvoiceCancel:
                return await SalesInvoiceReferenceAsync(companyId, sourceId);

            // إشعار مدين — مرجعه الإشعار الدائن الذي يُلغيه.
            case ZatcaDocumentType.DebitNote:
            {
                var originalCn = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId &&
                    d.DocumentType == ZatcaDocumentType.CreditNote &&
                    d.SourceDocumentId == sourceId);
                if (originalCn is not null)
                    return (originalCn.InvoiceNumber, originalCn.InvoiceUuid);
                break;
            }

            // (جاهزيّةُ الإطلاق · ح-٣٦) ‏**وإلغاءُ المستخلص مرجعُه مستخلصُه.**
            //
            // ‏كان `BillingCancel` **يسقط من هذا المبدل إلى `(null, null)`**،
            // ‏فيخرج مستندُ إلغاءٍ **بلا `BillingReference`** — **إشعارٌ دائنٌ لا
            // ‏يقول ماذا يُلغي**. والنمطُ نمطُ `DebitNote` أعلاه حرفاً:
            // ‏يُبحث عن مستند المصدر المُرسَل سلفاً ويُشار إليه.
            // ‏(ح-١٦١) ‏وعكسُ بيع الأصل مرجعُه فاتورةُ بيعه — بالنمط نفسِه.
            case ZatcaDocumentType.DisposalCancel:
            {
                var originalSale = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId &&
                    d.DocumentType == ZatcaDocumentType.AssetDisposal &&
                    d.SourceDocumentId == sourceId);
                if (originalSale is not null)
                    return (originalSale.InvoiceNumber, originalSale.InvoiceUuid);
                break;
            }

            case ZatcaDocumentType.DepositRefund:
            {
                // ‏(sector-modules#6) ‏ردُّ العربون يُشير إلى فاتورة دفعته (386) — المصدرُ نفسُه: قيدُ العربون.
                var prepayment = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId &&
                    d.DocumentType == ZatcaDocumentType.Prepayment &&
                    d.SourceDocumentId == sourceId);
                if (prepayment is not null)
                    return (prepayment.InvoiceNumber, prepayment.InvoiceUuid);
                break;
            }

            case ZatcaDocumentType.BillingCancel:
            {
                var originalPb = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId &&
                    d.DocumentType == ZatcaDocumentType.ProgressBilling &&
                    d.SourceDocumentId == sourceId);
                if (originalPb is not null)
                    return (originalPb.InvoiceNumber, originalPb.InvoiceUuid);
                break;
            }
        }

        return (null, null);
    }

    /// <summary>
    /// قفل معاملاتيّ على عدّاد ZATCA للشركة. المفتاح الثاني <c>7301</c> ثابتٌ مميّز حتى لا
    /// يتصادم مع قفل ترقيم القيود (الذي يستعمل السنة) ولا مع قفل سجلّ التدقيق (991).
    /// </summary>
    /// <summary>
    /// (م-٥ · ن-٢٥) <b>الشرطُ والقفلُ فُصلا — وكانا خلف فحصٍ واحد.</b>
    ///
    /// <para>كانت الدالّةُ تبدأ بـ<c>if (!db.Database.IsNpgsql()) return;</c>،
    /// <b>فيتخطّى غيرُ PostgreSQL القفلَ <u>وشرطَ المعاملة معاً</u></b>. والقفلُ خاصٌّ
    /// بـPostgreSQL بحقّ — <c>pg_advisory_xact_lock</c> لا نظيرَ له. <b>أمّا شرطُ
    /// المعاملة فليس خاصّاً بمزوّد</b>: قراءةُ العدّاد ثمّ الكتابةُ فوقه عمليّةٌ مركّبة،
    /// وبلا معاملةٍ لا تكون ذرّيّةً على أيّ محرّك.</para>
    ///
    /// <para><b>وأثرُ الدمج أنّ الحارسَ كان غائباً حيث تُكتب الاختبارات.</b> فمسارٌ يُكتب
    /// ويُختبَر على SQLite بلا معاملةٍ يمرّ أخضرَ، <b>ثمّ يرمي عند أوّل تشغيلٍ على
    /// PostgreSQL</b> — فيُكتشف الخطأُ في يد المُشغِّل بدل أن يُكتشف في الحزمة. وهذا
    /// الحارسُ بعينه سبق أن أمسك عطلاً حقيقيّاً (انظر أدناه)، <b>فتعطيلُه على مزوّد
    /// الاختبارات يُبطل أنفعَ ما فيه</b>.</para>
    /// </summary>
    private async Task AcquireIcvLockAsync(long companyId, CancellationToken ct)
    {
        // (CEEF-229 · م1-٦ / Z-4) **قفلٌ بلا معاملةٍ قفلٌ ميّت — ويُرفض صراحةً.**
        //
        // `pg_advisory_xact_lock` مربوطٌ بالمعاملة: خارجَها تُنهي العبارةُ نفسُها المعاملةَ
        // الضمنية فيُحرَّر القفلُ فوراً، فيقرأ المتزاحمان العدّادَ نفسَه — **ويبقى ظاهرُ
        // الحماية قائماً**. وهو النمطُ الذي أُغلق في ترقيم المستندات (م1-١٤).
        //
        // وكان حيّاً في إلغاء الفاتورة وميّتاً في إلغاء الإشعار الدائن والمستخلص: معاملتُهما
        // تُغلق **قبل** استدعاء المولّد بستّة أسطر. ولم يكشفه شيء لأنّ الفهرس الفريد يلتقط
        // التزاحمَ برميةٍ صاخبة، فيبدو النظامُ محميّاً وهو يرفض عملاً مشروعاً.
        if (db.Database.CurrentTransaction is null)
            throw new AccountingException(
                "توليدُ مستند ZATCA يجري خارج معاملة — وقفلُ العدّاد يُحرَّر فوراً فلا يحمي "
                + "تسلسل ICV. يُنقل التوليدُ إلى داخل معاملة الإلغاء/الترحيل.");

        // ‏**وهنا وحدَه يقف غيرُ PostgreSQL**: القفلُ الاستشاريُّ صيغةٌ لا نظيرَ لها في
        // ‏SQLite، وطلبُها يرمي. والشرطُ أعلاه سرى على الجميع، وهو المقصود.
        if (!db.Database.IsNpgsql())
            return;

        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, 7301 },
            ct);
    }

    /// <summary>مرجع فاتورة مبيعات برقمها ومعرّفها لدى الهيئة — بتصفية الشركة.</summary>
    private async Task<(string? Number, Guid? Uuid)> SalesInvoiceReferenceAsync(long companyId, long invoiceId)
    {
        var invoice = await db.SalesInvoices.SingleOrDefaultAsync(
            i => i.Id == invoiceId && i.CompanyId == companyId);
        if (invoice?.InvoiceNumber is null)
            return (null, null);

        var originalZatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.DocumentType == ZatcaDocumentType.SalesInvoice &&
            d.SourceDocumentId == invoiceId);

        return (invoice.InvoiceNumber, originalZatca?.InvoiceUuid);
    }

    private async Task<ZatcaSettings?> GetSettingsAsync(long companyId) =>
        await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId);
}
