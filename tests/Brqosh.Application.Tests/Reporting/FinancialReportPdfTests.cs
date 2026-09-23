using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;

namespace Brqosh.Application.Tests.Reporting;

public class FinancialReportPdfTests
{
    [Fact]
    public void IncomeStatementPdf_GeneratesBytes()
    {
        var result = new IncomeStatementResult(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            [new IncomeStatementRow("4101", "مبيعات", Brqosh.Domain.Accounting.AccountType.Revenue, 50000m)],
            [new IncomeStatementRow("5101", "رواتب", Brqosh.Domain.Accounting.AccountType.Expense, 20000m)]);

        var pdf = FinancialReportPdfGenerator.IncomeStatement("شركة تجريبية", result);
        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]); // PDF magic %
    }

    [Fact]
    public void BalanceSheetPdf_GeneratesBytes()
    {
        var result = new BalanceSheetResult(
            new DateOnly(2026, 6, 30),
            [new BalanceSheetRow("1101", "نقدية", Brqosh.Domain.Accounting.AccountType.Asset, 100000m)],
            [new BalanceSheetRow("2101", "موردون", Brqosh.Domain.Accounting.AccountType.Liability, 40000m)],
            [new BalanceSheetRow("3101", "رأس المال", Brqosh.Domain.Accounting.AccountType.Equity, 60000m)]);

        var pdf = FinancialReportPdfGenerator.BalanceSheet("شركة تجريبية", result);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void GeneralLedgerPdf_GeneratesBytes()
    {
        var rows = new List<(DateOnly, string, string, decimal, decimal, string)>
        {
            (new DateOnly(2026, 3, 1), "1101", "نقدية", 1000m, 0m, "قبض"),
        };
        var pdf = FinancialReportPdfGenerator.GeneralLedger("شركة", new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31), rows);
        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
    }

    [Fact]
    public void VatReturnPdf_GeneratesBytes()
    {
        var result = new VatReturnResult(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
            10000m, 1500m, 2000m, 300m, 500m, 75m, 8000m, 1200m, 200m, 30m, 30m,
            SalesBaseNet: 11_500m, PurchasesBaseNet: 7_800m);
        var pdf = FinancialReportPdfGenerator.VatReturn("شركة", result);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void StatementAndVoucherPdf_GenerateBytes()
    {
        var stmt = StatementPdfGenerator.CustomerStatement("شركة", "عميل", 500m,
            [new CustomerStatementLine(new DateOnly(2026, 3, 1), "INV-1", "فاتورة", 500m, 0m, 500m)]);
        Assert.NotEmpty(stmt);

        var voucher = VoucherPdfGenerator.ReceiptVoucher("شركة", "310123456700003", "RV-001",
            new DateOnly(2026, 3, 1), "عميل", 1000m, "نقداً", "", "دفعة");
        Assert.NotEmpty(voucher);
    }

    [Fact]
    public void SalesInvoicePdf_EmbedsQrImage_WhenTlvProvided()
    {
        var tlv = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-zatca-tlv"));
        var pdf = SalesInvoicePdfGenerator.Generate(
            PrintBranding.From("شركة", "310123456700003"),
            "INV-100",
            new DateOnly(2026, 3, 1),
            [new SalesInvoicePdfGenerator.Line("صنف", 1m, 100m, 100m)],
            100m, 15m, 115m,
            new SalesInvoicePdfGenerator.InvoicePrintOptions(
                PrintDocumentKind.SimplifiedSalesInvoice,
                PartyName: "عميل",
                QrTlvBase64: tlv,
                Icv: 42));

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Contains("/Image", System.Text.Encoding.ASCII.GetString(pdf));
    }

    [Fact]
    public void JournalEntryPdf_GeneratesBytes()
    {
        var pdf = JournalEntryPdfGenerator.Generate(
            PrintBranding.From("شركة", "310123456700003"),
            "JE-1",
            new DateOnly(2026, 3, 1),
            "مرحّل",
            "قيد اختبار",
            "INV-1",
            [new JournalEntryPdfGenerator.LineRow("1101", "نقدية", 100m, 0m),
             new JournalEntryPdfGenerator.LineRow("4101", "إيراد", 0m, 100m)],
            100m, 100m);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void CashFlowPdf_GeneratesBytes()
    {
        var result = new CashFlowStatementResult(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31),
            new CashFlowSection("التشغيل", [new CashFlowLine("صافي الدخل", 5000m)]),
            new CashFlowSection("الاستثمار", []),
            new CashFlowSection("التمويل", []),
            5000m, 10000m, 15000m);
        var pdf = FinancialReportPdfGenerator.CashFlowStatement("شركة", result);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void ConsolidatedAndProgressBillingPdf_GenerateBytes()
    {
        var tbRows = new List<ConsolidatedTrialBalanceRow>
        {
            new("1101", "نقدية", Brqosh.Domain.Accounting.AccountType.Asset, 1000m, 0m, 2),
        };
        var tbPdf = ConsolidatedReportPdfGenerator.TrialBalance(
            "مجموعة", "SAR", new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), tbRows);
        Assert.NotEmpty(tbPdf);

        var isResult = new ConsolidatedIncomeStatementResult(
            1, "SAR", new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
            [new ConsolidatedIncomeStatementRow("4101", "مبيعات", Brqosh.Domain.Accounting.AccountType.Revenue, 5000m, 2)],
            [new ConsolidatedIncomeStatementRow("5101", "رواتب", Brqosh.Domain.Accounting.AccountType.Expense, 2000m, 2)]);
        var isPdf = ConsolidatedReportPdfGenerator.IncomeStatement("مجموعة", isResult);
        Assert.NotEmpty(isPdf);

        var pbPdf = ProgressBillingPdfGenerator.Generate(
            PrintBranding.From("شركة", "310123456700003"),
            new ProgressBillingPdfGenerator.BillingPrintData(
                "PB-001", new DateOnly(2026, 3, 1), "عميل", "مشروع", "مارس 2026",
                100000m, 10m, 10000m, 90000m, 13500m, 103500m));
        Assert.NotEmpty(pbPdf);
    }

    [Fact]
    public void SalesInvoicePdf_EmbedsLogo_WhenBytesProvided()
    {
        var logo = ZatcaQrImageHelper.TryCreatePng(
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("logo-test")));
        Assert.NotNull(logo);

        var branding = new PrintBranding(
            "شركة تجريبية",
            "310123456700003",
            CompanyNameEn: "Demo Co",
            AddressAr: "الرياض",
            LogoImageBytes: logo,
            PrintFontFamily: "Tahoma");

        var pdf = SalesInvoicePdfGenerator.Generate(
            branding,
            "INV-LOGO",
            new DateOnly(2026, 3, 1),
            [new SalesInvoicePdfGenerator.Line("صنف", 1m, 100m, 100m)],
            100m, 15m, 115m);

        Assert.NotEmpty(pdf);
        Assert.Contains("/Image", System.Text.Encoding.ASCII.GetString(pdf));
    }

    [Fact]
    public void CompanyBrandingStorage_NormalizesPrintFont()
    {
        Assert.Equal("Tahoma", CompanyBrandingStorage.NormalizePrintFont(null));
        Assert.Equal("Traditional Arabic", CompanyBrandingStorage.NormalizePrintFont("Traditional Arabic"));
        Assert.Equal("Tahoma", CompanyBrandingStorage.NormalizePrintFont("UnknownFont"));
    }

    [Fact]
    public void ZatcaQrImageHelper_CreatesPng()
    {
        var tlv = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("qr-payload"));
        var png = ZatcaQrImageHelper.TryCreatePng(tlv);
        Assert.NotNull(png);
        Assert.Equal(0x89, png![0]);
    }
}
