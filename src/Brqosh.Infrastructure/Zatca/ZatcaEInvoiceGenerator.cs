using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
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
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal(), invoice.Lines, mode,
            isCreditNote: false, documentDiscount: invoice.DocumentDiscountAmount);
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
            note.Subtotal, note.VatAmount, note.GrandTotal(), note.Lines, mode, isCreditNote: true,
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
            billing.TaxableAmount, billing.VatAmount, billing.TotalAmount, lines, mode, isCreditNote: true);
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
            note.Subtotal, note.VatAmount, note.GrandTotal(), note.Lines, mode,
            isCreditNote: false, isDebitNote: true,
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
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal(), invoice.Lines, mode, isCreditNote: true,
            issueReason: $"إلغاء فاتورة مبيعات {invoice.InvoiceNumber}");
    }

    /// <summary>الرياض ‏+03:00‎ ثابتةً — لا توقيتَ صيفيّ في السعودية.</summary>
    private static readonly TimeSpan RiyadhOffset = TimeSpan.FromHours(3);

    private async Task<ZatcaEInvoiceDocument> GenerateAsync(
        long companyId, ZatcaDocumentType docType, long sourceId,
        string number, DateOnly date, Company company,
        Customer customer,
        decimal subtotal, decimal vat, decimal total,
        IReadOnlyList<SalesInvoiceLine> lines, ZatcaTransmissionMode mode, bool isCreditNote,
        bool isDebitNote = false, string? issueReason = null, decimal documentDiscount = 0m)
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
            DocumentDiscount: documentDiscount);

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
                signResult.PublicKeyDerBase64);
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

        var doc = ZatcaEInvoiceDocument.Create(
            companyId, docType, sourceId, number, uuid, icv, previousHash, invoiceHash,
            mode, qr, xmlPath, signedPath);

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
        }

        return (null, null);
    }

    /// <summary>
    /// قفل معاملاتيّ على عدّاد ZATCA للشركة. المفتاح الثاني <c>7301</c> ثابتٌ مميّز حتى لا
    /// يتصادم مع قفل ترقيم القيود (الذي يستعمل السنة) ولا مع قفل سجلّ التدقيق (991).
    /// </summary>
    private async Task AcquireIcvLockAsync(long companyId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
            return;

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
