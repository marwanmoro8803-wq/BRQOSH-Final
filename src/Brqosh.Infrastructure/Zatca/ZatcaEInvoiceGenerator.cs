using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
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
            invoice.InvoiceNumber, invoice.InvoiceDate, company, customer.NameAr, customer.VatRegistrationNumber,
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal(), invoice.Lines, mode, isCreditNote: false);
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
            note.NoteNumber, note.NoteDate, company, customer.NameAr, customer.VatRegistrationNumber,
            note.Subtotal, note.VatAmount, note.GrandTotal(), note.Lines, mode, isCreditNote: true);
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
            billing.BillingNumber, billing.BillingDate, company, customer.NameAr, customer.VatRegistrationNumber,
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
                d.DocumentType == ZatcaDocumentType.CreditNote &&
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
            billing.CompanyId, ZatcaDocumentType.CreditNote, progressBillingId,
            cnNumber, billing.BillingDate, company, customer.NameAr, customer.VatRegistrationNumber,
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
            dnNumber, note.NoteDate, company, customer.NameAr, customer.VatRegistrationNumber,
            note.Subtotal, note.VatAmount, note.GrandTotal(), note.Lines, mode,
            isCreditNote: false, isDebitNote: true);
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
                d.DocumentType == ZatcaDocumentType.CreditNote &&
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
            invoice.CompanyId, ZatcaDocumentType.CreditNote, salesInvoiceId,
            cnNumber, invoice.InvoiceDate, company, customer.NameAr, customer.VatRegistrationNumber,
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal(), invoice.Lines, mode, isCreditNote: true);
    }

    private async Task<ZatcaEInvoiceDocument> GenerateAsync(
        long companyId, ZatcaDocumentType docType, long sourceId,
        string number, DateOnly date, Company company,
        string customerName, string customerVat,
        decimal subtotal, decimal vat, decimal total,
        IReadOnlyList<SalesInvoiceLine> lines, ZatcaTransmissionMode mode, bool isCreditNote, bool isDebitNote = false)
    {
        var icv = (await db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == companyId)
            .MaxAsync(d => (int?)d.Icv) ?? 0) + 1;
        var previousHash = await db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == companyId)
            .OrderByDescending(d => d.Icv)
            .Select(d => d.InvoiceHash)
            .FirstOrDefaultAsync() ?? ZatcaHashHelper.ZeroHash();

        var uuid = Guid.NewGuid();
        var ts = date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)), DateTimeKind.Utc);
        var (billingRefNumber, billingRefUuid) = await ResolveBillingReferenceAsync(
            companyId, docType, sourceId, isCreditNote, isDebitNote);

        var qrPhase1 = ZatcaQrGenerator.GenerateTlvBase64Phase1(
            company.NameAr, company.VatRegistrationNumber, ts, total, vat);

        var ublData = new ZatcaUblInvoiceBuilder.InvoiceData(
            number, uuid, date, company.NameAr, company.VatRegistrationNumber,
            customerName, string.IsNullOrWhiteSpace(customerVat) ? null : customerVat,
            subtotal, vat, total, lines, icv.ToString(), previousHash, qrPhase1, isCreditNote, isDebitNote,
            IsSimplified: string.IsNullOrWhiteSpace(customerVat),
            BillingReferenceInvoiceNumber: billingRefNumber,
            BillingReferenceUuid: billingRefUuid);

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
                signResult.InvoiceHashBase64, signResult.SignatureBase64);
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
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BRQOSH", "zatca", companyId.ToString(), invoiceNumber);
        Directory.CreateDirectory(dir);
        var file = signed ? $"{invoiceNumber}.signed.xml" : $"{invoiceNumber}.xml";
        var path = Path.Combine(dir, file);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private async Task<(string? Number, Guid? Uuid)> ResolveBillingReferenceAsync(
        long companyId, ZatcaDocumentType docType, long sourceId, bool isCreditNote, bool isDebitNote)
    {
        if (!isCreditNote && !isDebitNote)
            return (null, null);

        if (docType == ZatcaDocumentType.CreditNote && !isDebitNote)
        {
            var note = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == sourceId);
            if (note?.OriginalInvoiceId is long invoiceId)
            {
                var invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceId);
                var originalZatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId &&
                    d.DocumentType == ZatcaDocumentType.SalesInvoice &&
                    d.SourceDocumentId == invoiceId);
                if (invoice?.InvoiceNumber is not null)
                    return (invoice.InvoiceNumber, originalZatca?.InvoiceUuid);
            }
        }

        if (docType == ZatcaDocumentType.CreditNote && sourceId > 0)
        {
            var salesInvoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == sourceId);
            if (salesInvoice?.InvoiceNumber is not null)
            {
                var originalZatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId &&
                    d.DocumentType == ZatcaDocumentType.SalesInvoice &&
                    d.SourceDocumentId == sourceId);
                return (salesInvoice.InvoiceNumber, originalZatca?.InvoiceUuid);
            }
        }

        if (docType == ZatcaDocumentType.DebitNote)
        {
            var originalCn = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
                d.CompanyId == companyId &&
                d.DocumentType == ZatcaDocumentType.CreditNote &&
                d.SourceDocumentId == sourceId);
            if (originalCn is not null)
                return (originalCn.InvoiceNumber, originalCn.InvoiceUuid);
        }

        return (null, null);
    }

    private async Task<ZatcaSettings?> GetSettingsAsync(long companyId) =>
        await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId);
}
