using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public sealed record PrintResult(byte[] PdfBytes, string SuggestedFileName);

/// <summary>Central PDF export — loads entities, generates PDF, logs audit.</summary>
public partial class DocumentPrintService(BrqoshDbContext db, AuditService audit, Treasury.ExchangeRateService rateService)
{
    public async Task<PrintResult> ExportSalesInvoiceAsync(
        long companyId, long invoiceId, string username, CancellationToken ct = default)
    {
        var (company, invoice, customerName, zatca) = await LoadSalesInvoiceAsync(companyId, invoiceId, ct);
        PrintPostedGuard.EnsureSalesInvoicePosted(invoice);
        var kind = ResolveSalesKind(invoice, zatca);
        var lines = invoice.Lines.Select(l =>
            new SalesInvoicePdfGenerator.Line(l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList();
        var branding = PrintBranding.FromCompany(company);
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            kind,
            PartyName: customerName,
            QrTlvBase64: zatca?.QrTlvBase64,
            Icv: zatca?.Icv,
            ComplianceStatus: zatca?.ComplianceStatus.ToString());

        var pdf = SalesInvoicePdfGenerator.Generate(
            branding,
            invoice.InvoiceNumber ?? invoice.Id.ToString(),
            invoice.InvoiceDate,
            lines,
            invoice.Subtotal,
            invoice.VatAmount,
            invoice.GrandTotal(),
            options);

        var fileName = $"brqosh-inv-{SanitizeFileName(invoice.InvoiceNumber ?? invoice.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "SalesInvoice", invoice.InvoiceNumber ?? invoice.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportPurchaseInvoiceAsync(
        long companyId, long invoiceId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var invoice = await db.PurchaseInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == invoiceId, ct);
        PrintPostedGuard.EnsurePurchaseInvoicePosted(invoice);
        var supplier = await db.Suppliers.SingleAsync(s => s.Id == invoice.SupplierId, ct);
        var lines = invoice.Lines.Select(l =>
            new SalesInvoicePdfGenerator.Line(l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList();
        var branding = PrintBranding.FromCompany(company);
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            PrintDocumentKind.PurchaseInvoice,
            PartyName: supplier.NameAr);

        var pdf = SalesInvoicePdfGenerator.Generate(
            branding,
            invoice.InvoiceNumber ?? invoice.Id.ToString(),
            invoice.InvoiceDate,
            lines,
            invoice.Subtotal,
            invoice.VatAmount,
            invoice.GrandTotal(),
            options);

        var fileName = $"brqosh-pi-{SanitizeFileName(invoice.InvoiceNumber ?? invoice.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "PurchaseInvoice", invoice.InvoiceNumber ?? invoice.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportCreditNoteAsync(
        long companyId, long noteId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var note = await db.CreditNotes.SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, ct);
        PrintPostedGuard.EnsureCreditNotePosted(note);
        var customer = await db.Customers.SingleAsync(c => c.Id == note.CustomerId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == noteId &&
            d.DocumentType == ZatcaDocumentType.CreditNote, ct);

        var lines = note.Lines.Select(l =>
            new SalesInvoicePdfGenerator.Line(l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList();
        var branding = PrintBranding.FromCompany(company);
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            PrintDocumentKind.CreditNote,
            PartyName: customer.NameAr,
            QrTlvBase64: zatca?.QrTlvBase64,
            Icv: zatca?.Icv,
            ComplianceStatus: zatca?.ComplianceStatus.ToString());

        var pdf = SalesInvoicePdfGenerator.Generate(
            branding,
            note.NoteNumber ?? note.Id.ToString(),
            note.NoteDate,
            lines,
            note.Subtotal,
            note.VatAmount,
            note.GrandTotal(),
            options);

        var fileName = $"brqosh-cn-{SanitizeFileName(note.NoteNumber ?? note.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "CreditNote", note.NoteNumber ?? note.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportDebitNoteAsync(
        long companyId, long noteId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var note = await db.DebitNotes.SingleAsync(n => n.CompanyId == companyId && n.Id == noteId, ct);
        PrintPostedGuard.EnsureDebitNotePosted(note);
        var supplier = await db.Suppliers.SingleAsync(s => s.Id == note.SupplierId, ct);
        var lines = note.Lines.Select(l =>
            new SalesInvoicePdfGenerator.Line(l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList();
        var branding = PrintBranding.FromCompany(company);
        var options = new SalesInvoicePdfGenerator.InvoicePrintOptions(
            PrintDocumentKind.PurchaseDebitNote,
            PartyName: supplier.NameAr);

        var pdf = SalesInvoicePdfGenerator.Generate(
            branding,
            note.NoteNumber ?? note.Id.ToString(),
            note.NoteDate,
            lines,
            note.Subtotal,
            note.VatAmount,
            note.GrandTotal(),
            options);

        var fileName = $"brqosh-dn-{SanitizeFileName(note.NoteNumber ?? note.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "DebitNote", note.NoteNumber ?? note.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportReceiptVoucherAsync(
        long companyId, long voucherId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var voucher = await db.ReceiptVouchers.SingleAsync(v => v.CompanyId == companyId && v.Id == voucherId, ct);
        PrintPostedGuard.EnsureReceiptVoucherPosted(voucher);
        var customer = await db.Customers.SingleAsync(c => c.Id == voucher.CustomerId, ct);
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
        var supplier = await db.Suppliers.SingleAsync(s => s.Id == voucher.SupplierId, ct);
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
        var customer = await db.Customers.SingleAsync(c => c.Id == invoice.CustomerId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == invoiceId &&
            d.DocumentType == ZatcaDocumentType.SalesInvoice, ct);
        return (company, invoice, customer.NameAr, zatca);
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
