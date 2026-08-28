using Brqosh.Domain.Companies;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public partial class DocumentPrintService
{
    public async Task<PrintResult> ExportTrialBalanceAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        var pdf = TrialBalancePdfGenerator.Generate(branding, fromDate, toDate, rows);
        var fileName = $"brqosh-trial-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "TrialBalance", $"{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportIncomeStatementAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var result = await new IncomeStatementQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        var pdf = FinancialReportPdfGenerator.IncomeStatement(branding, result);
        var fileName = $"brqosh-income-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "IncomeStatement", $"{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportBalanceSheetAsync(
        long companyId, DateOnly asOfDate, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var result = await new BalanceSheetQuery(db).ExecuteAsync(companyId, asOfDate);
        var pdf = FinancialReportPdfGenerator.BalanceSheet(branding, result);
        var fileName = $"brqosh-balance-{asOfDate:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "BalanceSheet", asOfDate.ToString("yyyy-MM-dd"), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportGeneralLedgerAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string username,
        string? accountCodeFilter = null, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var accounts = await new GeneralLedgerQuery(db).ExecuteAsync(companyId, fromDate, toDate, accountCodeFilter);
        var rows = accounts.SelectMany(a => a.Lines.Select(l =>
            (l.EntryDate, a.AccountCode, a.AccountNameAr, l.Debit, l.Credit, l.Description))).ToList();
        var pdf = FinancialReportPdfGenerator.GeneralLedger(branding, fromDate, toDate, rows);
        var fileName = $"brqosh-gl-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "GeneralLedger", $"{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportVatReturnAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var result = await new VatReturnQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        var pdf = FinancialReportPdfGenerator.VatReturn(branding, result);
        var fileName = $"brqosh-vat-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(companyId, username, "VatReturn", $"{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportCustomerStatementAsync(
        long companyId, long customerId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        // (CEEF-229 · م2-١٨) قراءةٌ تتجاوز مرشّح الحذف الناعم عمداً — المستند تاريخيّ.
        // الطرف كان موجوداً يوم صدور المستند، وحذفُه لاحقاً لا يُلغي المستند. وقراءةُ
        // المرشَّح كانت ترمي «Sequence contains no elements» بالإنجليزية داخل واجهةٍ
        // عربية، فتصير فاتورةٌ ضريبية سعودية **غير قابلة للطباعة أبداً** — وهي وثيقةٌ
        // يُلزم النظام بالاحتفاظ بها وإصدار نسخٍ منها.
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customerId, ct);
        var lines = await new CustomerBalanceQuery(db).StatementAsync(companyId, customerId);
        var balance = await new CustomerBalanceQuery(db).ForCustomerAsync(companyId, customerId);
        var pdf = StatementPdfGenerator.CustomerStatement(branding, customer.NameAr, balance.Balance, lines);
        var fileName = $"brqosh-cust-{SanitizeFileName(customer.NameAr)}.pdf";
        await LogAsync(companyId, username, "CustomerStatement", customerId.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportSupplierStatementAsync(
        long companyId, long supplierId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company);
        var supplier = await db.Suppliers.IgnoreQueryFilters().SingleAsync(s => s.Id == supplierId, ct);
        var lines = await new SupplierBalanceQuery(db).StatementAsync(companyId, supplierId);
        var balance = await new SupplierBalanceQuery(db).ForSupplierAsync(companyId, supplierId);
        var pdf = StatementPdfGenerator.SupplierStatement(branding, supplier.NameAr, balance.Balance, lines);
        var fileName = $"brqosh-supplier-{SanitizeFileName(supplier.NameAr)}.pdf";
        await LogAsync(companyId, username, "SupplierStatement", supplierId.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportConsolidatedTrialBalanceAsync(
        long auditCompanyId, long groupId, DateOnly fromDate, DateOnly toDate, string username,
        CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        var rows = await new ConsolidatedTrialBalanceQuery(db, rateService).ExecuteAsync(groupId, fromDate, toDate, ct: ct);
        var pdf = ConsolidatedReportPdfGenerator.TrialBalance(
            group.NameAr, group.ReportingCurrencyCode, fromDate, toDate, rows);
        var fileName = $"brqosh-cons-tb-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(auditCompanyId, username, "ConsolidatedTrialBalance",
            $"group:{groupId}:{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportConsolidatedIncomeStatementAsync(
        long auditCompanyId, long groupId, DateOnly fromDate, DateOnly toDate, string username,
        CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        var result = await new ConsolidatedIncomeStatementQuery(db, rateService)
            .ExecuteAsync(groupId, fromDate, toDate, ct: ct);
        var pdf = ConsolidatedReportPdfGenerator.IncomeStatement(group.NameAr, result);
        var fileName = $"brqosh-cons-is-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        await LogAsync(auditCompanyId, username, "ConsolidatedIncomeStatement",
            $"group:{groupId}:{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportConsolidatedBalanceSheetAsync(
        long auditCompanyId, long groupId, DateOnly asOfDate, string username, CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        var result = await new ConsolidatedBalanceSheetQuery(db, rateService).ExecuteAsync(groupId, asOfDate, ct: ct);
        var pdf = ConsolidatedReportPdfGenerator.BalanceSheet(group.NameAr, result);
        var fileName = $"brqosh-cons-bs-{asOfDate:yyyyMMdd}.pdf";
        await LogAsync(auditCompanyId, username, "ConsolidatedBalanceSheet",
            $"group:{groupId}:{asOfDate:yyyy-MM-dd}", ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportProgressBillingAsync(
        long companyId, long billingId, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var billing = await db.ProgressBillings.SingleAsync(b => b.CompanyId == companyId && b.Id == billingId, ct);
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == billing.CustomerId, ct);
        var project = await db.ConstructionProjects.SingleAsync(p => p.Id == billing.ProjectId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == billingId &&
            d.DocumentType == ZatcaDocumentType.ProgressBilling, ct);

        var branding = PrintBranding.FromCompany(company);
        var data = new ProgressBillingPdfGenerator.BillingPrintData(
            billing.BillingNumber ?? billing.Id.ToString(),
            billing.BillingDate,
            customer.NameAr,
            project.NameAr,
            billing.PeriodDescription,
            billing.CurrentWorkAmount,
            billing.RetentionPercent,
            billing.RetentionAmount,
            billing.NetBillableAmount,
            billing.VatAmount,
            billing.GrandTotal,
            zatca?.QrTlvBase64,
            zatca?.Icv,
            zatca?.ComplianceStatus.ToString());

        var pdf = ProgressBillingPdfGenerator.Generate(branding, data);
        var fileName = $"brqosh-pb-{SanitizeFileName(billing.BillingNumber ?? billing.Id.ToString())}.pdf";
        await LogAsync(companyId, username, "ProgressBilling", billing.BillingNumber ?? billing.Id.ToString(), ct);
        return new PrintResult(pdf, fileName);
    }

    public async Task<PrintResult> ExportZatcaDocumentAsync(
        long companyId, long zatcaDocumentId, string username, CancellationToken ct = default)
    {
        var doc = await db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.CompanyId == companyId && d.Id == zatcaDocumentId, ct);

        return doc.DocumentType switch
        {
            ZatcaDocumentType.SalesInvoice => await ExportSalesInvoiceAsync(companyId, doc.SourceDocumentId, username, ct),
            ZatcaDocumentType.CreditNote => await ExportCreditNoteAsync(companyId, doc.SourceDocumentId, username, ct),
            ZatcaDocumentType.ProgressBilling => await ExportProgressBillingAsync(companyId, doc.SourceDocumentId, username, ct),
            _ => throw new InvalidOperationException($"طباعة PDF غير مدعومة لنوع المستند: {doc.DocumentType}"),
        };
    }

    private async Task<CompanyGroup> LoadGroupAsync(long groupId, CancellationToken ct) =>
        await db.CompanyGroups.SingleAsync(g => g.Id == groupId, ct);
}
