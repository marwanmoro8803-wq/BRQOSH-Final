using Brqosh.Domain.Companies;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public partial class DocumentPrintService
{
    /// <summary>
    /// (المخرجاتُ كلُّها بالقالب الموحَّد) <b>أيُّ جدولٍ في البرنامج يُطبع بالترويسة والتذييل والشعار والخطّ نفسِها</b> — كانت الطباعةُ لأربعٍ وعشرين
    /// شاشةً فقط، وما سواها (الأعمار والمستودعات والعملاء والموردون…) يُصدَّر إلى Excel ولا يُطبع. الأرقامُ تُحاذى عند نهاية العمود، وعمودٌ كلُّه
    /// أرقامٌ يُعامَل رقميّاً؛ وما زاد على <see cref="MaxTableRows"/> صفٍّ يُقصّ <b>بتنبيهٍ ظاهرٍ في الورقة</b> لا بصمت.
    /// </summary>
    public async Task<PrintResult> ExportTableAsync(
        long companyId, string username, string title, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company, username);
        using var _lang = PrintLang.Begin(branding.English);

        var shown = rows.Take(MaxTableRows).ToList();
        PrintColumn[] columns = headers.Select((h, i) => new PrintColumn(
            h, Numeric: shown.Count > 0 && shown.All(r => i < r.Count && (string.IsNullOrEmpty(r[i]) || IsNumberText(r[i]))))).ToArray();

        var summary = new List<(string Label, string Value)> { (PrintLang.T("عدد الصفوف"), PrintText.Money(rows.Count).Replace(".00", "")) };
        if (rows.Count > shown.Count)
            summary.Add((PrintLang.T("تنبيه"), PrintLang.TF("المعروض {0} صفٍّ من {1} — صدِّر إلى Excel للباقي", shown.Count, rows.Count)));

        var pdf = PrintReport.Table(branding, title, null, columns, shown, summary, landscape: columns.Length > 6);
        await LogAsync(companyId, username, "Table", title, ct);
        return new PrintResult(pdf, $"brqosh-table-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
    }

    public const int MaxTableRows = 3000;

    private static bool IsNumberText(string s) =>
        decimal.TryParse(s.Replace(",", "").Replace("(", "-").Replace(")", "").Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _);

    public async Task<PrintResult> ExportTrialBalanceAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string username, CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company, username);
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
        var branding = PrintBranding.FromCompany(company, username);
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
        var branding = PrintBranding.FromCompany(company, username);
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
        var branding = PrintBranding.FromCompany(company, username);
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
        var branding = PrintBranding.FromCompany(company, username);
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
        var branding = PrintBranding.FromCompany(company, username);
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
        var branding = PrintBranding.FromCompany(company, username);
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
        var data = await ProgressBillingRequestAsync(companyId, billingId, ct);

        var pdf = ProgressBillingPdfGenerator.Generate(PrintBranding.FromCompany(company, username), data);
        var fileName = $"brqosh-pb-{SanitizeFileName(data.BillingNumber)}.pdf";
        await LogAsync(companyId, username, "ProgressBilling", data.BillingNumber, ct);
        return new PrintResult(pdf, fileName);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥٦ · ui-ux-i18n-printing#1) <b>ما يُطبع منه المستخلص — ويُقاس قبل أن يُرسَم</b>
    /// (مذهبُ ح-٤٢). <b>وهو فاتورةٌ ضريبيّة</b>: نوعُها ونسبتُها وهويّةُ المشتري فيه.
    /// </summary>
    internal async Task<ProgressBillingPdfGenerator.BillingPrintData> ProgressBillingRequestAsync(
        long companyId, long billingId, CancellationToken ct = default)
    {
        var billing = await db.ProgressBillings.SingleAsync(b => b.CompanyId == companyId && b.Id == billingId, ct);
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == billing.CustomerId, ct);
        var project = await db.ConstructionProjects.SingleAsync(p => p.Id == billing.ProjectId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.SourceDocumentId == billingId &&
            d.DocumentType == ZatcaDocumentType.ProgressBilling, ct);

        // ‏(ح-٥٣) المستخلصُ لعميلٍ مسجَّلٍ يُخلَّص — فرمزُه من القارئ الواحد. ولا بديلَ من
        // ‏الطور الأوّل هنا: لم يكن للمستخلص رمزٌ قبل أن يُولَّد مستندُه، ولا يُخترع الآن.
        var mark = ZatcaHandOverCopy.MarkFor(zatca, () => null);
        return new ProgressBillingPdfGenerator.BillingPrintData(
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
            Kind: BillingKind(zatca?.TransmissionMode, customer.SupportsStandardTaxInvoice),
            VatRate: ProgressBilling.SaudiVatRate,
            PartyVatNumber: customer.VatRegistrationNumber,
            PartyAddress: customer.Address,
            mark.QrTlvBase64,
            zatca?.Icv,
            zatca?.ComplianceStatus.ToString(),
            mark.ClearanceNotice);
    }

    /// <summary>
    /// (ح-٥٦) <b>المستخلصُ فاتورةٌ ضريبيّةٌ أم مبسّطة</b> — <b>ما أُرسل للهيئة يحكم</b>: نوعُ المستند المُولَّد،
    /// لا حالُ العميل اليوم (قد يُضاف رقمُه أو يُحذف بعد الإرسال). <b>وبلا مستندٍ فقاعدةُ المولِّد نفسُها</b>
    /// (<c>TryGenerateForProgressBillingAsync</c>): عميلٌ يدعم الفاتورةَ الضريبيّة يُخلَّص، وغيرُه يُبلَّغ.
    /// </summary>
    internal static PrintDocumentKind BillingKind(ZatcaTransmissionMode? mode, bool supportsStandardTaxInvoice) =>
        mode switch
        {
            ZatcaTransmissionMode.Clearance => PrintDocumentKind.StandardSalesInvoice,
            ZatcaTransmissionMode.Reporting => PrintDocumentKind.SimplifiedSalesInvoice,
            null => supportsStandardTaxInvoice
                ? PrintDocumentKind.StandardSalesInvoice
                : PrintDocumentKind.SimplifiedSalesInvoice,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "نمطُ إرسالٍ غيرُ معروف."),
        };

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
            // (جاهزيّةُ الإطلاق · ح-١٦) ‏وبيعُ الأصل الثابت — مستندٌ ضريبيٌّ يأخذه المشتري.
            ZatcaDocumentType.AssetDisposal => await ExportAssetDisposalAsync(companyId, doc.SourceDocumentId, username, ct),
            // (جاهزيّةُ الإطلاق · ح-١٩) ‏ومستنداتُ الإلغاء الثلاثة — **بأنفسها لا بمصادرها**.
            ZatcaDocumentType.InvoiceCancel or ZatcaDocumentType.BillingCancel or ZatcaDocumentType.DebitNote
                or ZatcaDocumentType.DisposalCancel
                // ‏(sector-modules#6) ‏وفاتورةُ الدفعة المقدّمة — مصدرُها قيدٌ لا مستندٌ يُطبع، فتُطبع بنفسها.
                or ZatcaDocumentType.Prepayment or ZatcaDocumentType.DepositRefund
                => await ExportZatcaCancellationAsync(companyId, zatcaDocumentId, username, ct),
            _ => throw new InvalidOperationException($"طباعة PDF غير مدعومة لنوع المستند: {doc.DocumentType}"),
        };
    }

    /// <summary>
    /// (CEEF-230 · ط6-٢) <b>قسيمةُ راتبٍ واحدة — والمولِّدُ كان مبنيّاً بلا مستدعٍ واحد.</b>
    ///
    /// <para><c>PayslipPdfGenerator</c> بُني في <c>ط2-٦</c> <b>ولا شيءَ في المستودع
    /// يستدعيه</b>. فالقسيمةُ ميزةٌ موصوفةٌ ومختبَرةٌ <b>لا يستطيع المالكُ إخراجَها</b> —
    /// وهو عينُ عطل «الاسم بلا جسد» في طبقة العرض.</para>
    ///
    /// <para><b>ويُختار السطرُ بترتيبه في الدورة</b>: الموظّفُ قد لا يكون له سجلٌّ بعد،
    /// <b>فمفتاحُه الكودُ يخرج «—»</b> ولا يُميّز. والترتيبُ في الدورة ثابتٌ ومُخزَّن.</para>
    /// </summary>
    public async Task<PrintResult> ExportPayslipAsync(
        long companyId, long payrollRunId, int lineIndex, string username,
        CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var branding = PrintBranding.FromCompany(company, username);

        var slips = await new Brqosh.Infrastructure.Payroll.PayslipQuery(db)
            .ExecuteAsync(companyId, payrollRunId, ct);

        if (lineIndex < 0 || lineIndex >= slips.Count)
            throw new Brqosh.Domain.Accounting.AccountingException("سطر القسيمة غير موجود في الدورة.");

        var slip = slips[lineIndex];
        var pdf = PayslipPdfGenerator.Build(branding, slip);
        var fileName = $"brqosh-payslip-{slip.PeriodCode}-{lineIndex + 1}.pdf";

        await LogAsync(companyId, username, "Payslip", $"{slip.PeriodCode}#{lineIndex + 1}", ct);
        return new PrintResult(pdf, fileName);
    }

    private async Task<CompanyGroup> LoadGroupAsync(long groupId, CancellationToken ct) =>
        await db.CompanyGroups.SingleAsync(g => g.Id == groupId, ct);
}
