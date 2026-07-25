using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>أدلة M7 — hosts ومسارات UI فاخرة دون تشغيل WPF كامل.</summary>
public class GoldenPathUiMarkupTests
{
    private static string ReadDesktopFile(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

    [Fact]
    public void DashboardPage_declares_luxury_hosts()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\DashboardPage.xaml");
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
        Assert.Contains("x:Name=\"ChartsColumn\"", xaml);
        Assert.Contains("x:Name=\"IntelColumn\"", xaml);
        Assert.Contains("GlassPanel", xaml);
    }

    [Fact]
    public void BankReconciliationPage_has_split_matcher_layout()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BankReconciliationPage.xaml");
        Assert.Contains("x:Name=\"StatementGrid\"", xaml);
        Assert.Contains("x:Name=\"Grid\"", xaml);
        Assert.Contains("MatchSummaryText", xaml);
    }

    [Fact]
    public void SalesInvoiceDialog_has_zatca_side_panel()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SalesInvoiceDialog.xaml");
        Assert.Contains("x:Name=\"ZatcaStatusText\"", xaml);
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("TabularText", xaml);
    }

    [Fact]
    public void PurchaseRequestsPage_has_workflow_pipeline_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PurchaseRequestsPage.xaml");
        Assert.Contains("StatusBadge", xaml);
        Assert.Contains("x:Name=\"SubmitBtn\"", xaml);
        Assert.Contains("x:Name=\"ConvertPoBtn\"", xaml);
        Assert.Contains("خطوة الموافقة", xaml);
    }

    [Fact]
    public void PurchaseOrdersPage_uses_toolbar_card_pattern()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PurchaseOrdersPage.xaml");
        Assert.Contains("ToolbarCard", xaml);
        Assert.Contains("ContentCard", xaml);
    }

    [Fact]
    public void StockMovementsPage_has_page_header()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\StockMovementsPage.xaml");
        Assert.Contains("PageTitleText", xaml);
        Assert.Contains("حركات المخزون", xaml);
    }

    [Fact]
    public void ProjectsPage_has_kpi_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ProjectsPage.xaml");
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
        Assert.Contains("PageTitleText", xaml);
    }

    [Fact]
    public void ProgressBillingsPage_has_billing_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ProgressBillingsPage.xaml");
        Assert.Contains("x:Name=\"BillingSummaryText\"", xaml);
        Assert.Contains("AlertStrip", xaml);
    }

    [Fact]
    public void ProjectProfitabilityPage_has_glass_hero_and_kpi()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ProjectProfitabilityPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
    }

    [Fact]
    public void PayrollPage_has_kpi_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PayrollPage.xaml");
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
    }

    [Fact]
    public void FixedAssetsPage_has_kpi_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\FixedAssetsPage.xaml");
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
    }

    [Fact]
    public void ProductsPage_has_inventory_summary()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ProductsPage.xaml");
        Assert.Contains("x:Name=\"InventorySummaryText\"", xaml);
    }

    [Fact]
    public void SalesInvoicesPage_has_kpi_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SalesInvoicesPage.xaml");
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
        Assert.Contains("PageTitleText", xaml);
    }

    [Fact]
    public void CustomersPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CustomersPage.xaml");
        Assert.Contains("x:Name=\"CustomerSummaryText\"", xaml);
        Assert.Contains("AlertStrip", xaml);
    }

    [Fact]
    public void ReceiptVouchersPage_has_header_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ReceiptVouchersPage.xaml");
        Assert.Contains("x:Name=\"VoucherSummaryText\"", xaml);
        Assert.Contains("سندات القبض", xaml);
    }

    [Fact]
    public void CreditNotesPage_has_header_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CreditNotesPage.xaml");
        Assert.Contains("x:Name=\"NoteSummaryText\"", xaml);
        Assert.Contains("مردودات المبيعات", xaml);
        Assert.Contains("إشعارات دائن", xaml);
    }

    [Fact]
    public void BankTransfersPage_has_header_and_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BankTransfersPage.xaml");
        Assert.Contains("x:Name=\"TransfersGrid\"", xaml);
        Assert.Contains("التحويلات البنكية", xaml);
    }

    [Fact]
    public void CompanySettingsPage_has_sticky_action_bar()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CompanySettingsPage.xaml");
        Assert.Contains("x:Name=\"SaveBtn\"", xaml);
        Assert.Contains("CompanySettingsSave", xaml);
        Assert.Contains("x:Name=\"ActiveModulesText\"", xaml);
    }

    [Fact]
    public void SuppliersPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SuppliersPage.xaml");
        Assert.Contains("x:Name=\"SupplierSummaryText\"", xaml);
        Assert.Contains("الموردون", xaml);
    }

    [Fact]
    public void PurchaseInvoicesPage_has_kpi_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PurchaseInvoicesPage.xaml");
        Assert.Contains("x:Name=\"KpiGrid\"", xaml);
    }

    [Fact]
    public void PaymentVouchersPage_has_header_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PaymentVouchersPage.xaml");
        Assert.Contains("x:Name=\"VoucherSummaryText\"", xaml);
        Assert.Contains("سندات الصرف", xaml);
    }

    [Fact]
    public void TrialBalancePage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\TrialBalancePage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("ميزان المراجعة", xaml);
    }

    [Fact]
    public void JournalEntriesPage_has_page_header()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\JournalEntriesPage.xaml");
        Assert.Contains("x:Name=\"EntrySummaryText\"", xaml);
        Assert.Contains("قيود اليومية", xaml);
    }

    [Fact]
    public void BankAccountsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BankAccountsPage.xaml");
        Assert.Contains("x:Name=\"BankSummaryText\"", xaml);
        Assert.Contains("الحسابات البنكية", xaml);
    }

    [Fact]
    public void ChequesPage_has_header_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ChequesPage.xaml");
        Assert.Contains("x:Name=\"ChequeSummaryText\"", xaml);
        Assert.Contains("الشيكات", xaml);
    }

    [Fact]
    public void ZatcaInvoicesPage_has_glass_hero_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ZatcaInvoicesPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("x:Name=\"ZatcaSummaryText\"", xaml);
    }

    [Fact]
    public void ZatcaSettingsPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ZatcaSettingsPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("ZATCA", xaml);
    }

    [Fact]
    public void BalanceSheetPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BalanceSheetPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("الميزانية العمومية", xaml);
    }

    [Fact]
    public void IncomeStatementPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\IncomeStatementPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("قائمة الدخل", xaml);
    }

    [Fact]
    public void GeneralLedgerPage_has_glass_hero_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\GeneralLedgerPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("x:Name=\"GlSummaryText\"", xaml);
    }

    [Fact]
    public void FiscalPeriodsPage_has_glass_hero_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\FiscalPeriodsPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("x:Name=\"PeriodSummaryText\"", xaml);
    }

    [Fact]
    public void VatReturnPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\VatReturnPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("ضريبة القيمة المضافة", xaml);
    }

    [Fact]
    public void ChartOfAccountsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ChartOfAccountsPage.xaml");
        Assert.Contains("x:Name=\"CoaSummaryText\"", xaml);
        Assert.Contains("دليل الحسابات", xaml);
    }

    [Fact]
    public void AuditLogPage_has_header_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\AuditLogPage.xaml");
        Assert.Contains("x:Name=\"AuditSummaryText\"", xaml);
        Assert.Contains("سجل التدقيق", xaml);
    }

    [Fact]
    public void DebitNotesPage_has_header_and_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\DebitNotesPage.xaml");
        Assert.Contains("x:Name=\"NoteSummaryText\"", xaml);
        Assert.Contains("إشعارات مدين", xaml);
    }

    [Fact]
    public void PurchaseOrdersPage_has_order_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PurchaseOrdersPage.xaml");
        Assert.Contains("x:Name=\"OrderSummaryText\"", xaml);
        Assert.Contains("أوامر الشراء", xaml);
    }

    [Fact]
    public void CustomerStatementPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CustomerStatementPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("كشف حساب عميل", xaml);
    }

    [Fact]
    public void SupplierStatementPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SupplierStatementPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("كشف حساب مورد", xaml);
    }

    [Fact]
    public void GoodsReceiptsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\GoodsReceiptsPage.xaml");
        Assert.Contains("x:Name=\"ReceiptSummaryText\"", xaml);
        Assert.Contains("أذون الاستلام", xaml);
    }

    [Fact]
    public void WarehousesPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\WarehousesPage.xaml");
        Assert.Contains("x:Name=\"WarehouseSummaryText\"", xaml);
    }

    [Fact]
    public void StockCountPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\StockCountPage.xaml");
        Assert.Contains("x:Name=\"CountSummaryText\"", xaml);
        Assert.Contains("جرد المخزون", xaml);
    }

    [Fact]
    public void CostCentersPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CostCentersPage.xaml");
        Assert.Contains("x:Name=\"CostCenterSummaryText\"", xaml);
    }

    [Fact]
    public void RetentionsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\RetentionsPage.xaml");
        Assert.Contains("x:Name=\"RetentionSummaryText\"", xaml);
        Assert.Contains("المحتجزات", xaml);
    }

    [Fact]
    public void UsersPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\UsersPage.xaml");
        Assert.Contains("x:Name=\"UserSummaryText\"", xaml);
        Assert.Contains("المستخدمون", xaml);
    }

    [Fact]
    public void BranchesPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BranchesPage.xaml");
        Assert.Contains("x:Name=\"BranchSummaryText\"", xaml);
        Assert.Contains("الفروع", xaml);
    }

    [Fact]
    public void ContractsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ContractsPage.xaml");
        Assert.Contains("x:Name=\"ContractSummaryText\"", xaml);
        Assert.Contains("عقود المقاولات", xaml);
    }

    [Fact]
    public void BoqPage_has_glass_hero_and_summary()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BoqPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("x:Name=\"BoqSummaryText\"", xaml);
        Assert.Contains("BOQ", xaml);
    }

    [Fact]
    public void SubcontractorsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SubcontractorsPage.xaml");
        Assert.Contains("x:Name=\"SubcontractorSummaryText\"", xaml);
        Assert.Contains("مقاولو الباطن", xaml);
    }

    [Fact]
    public void ConsolidatedTrialBalancePage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ConsolidatedTrialBalancePage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("ميزان مراجعة موحّد", xaml);
    }

    [Fact]
    public void ConsolidatedBalanceSheetPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ConsolidatedBalanceSheetPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("الميزانية الموحّدة", xaml);
    }

    [Fact]
    public void ConsolidatedIncomeStatementPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ConsolidatedIncomeStatementPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("قائمة دخل موحّدة", xaml);
    }

    [Fact]
    public void CompanySettingsPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CompanySettingsPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("إعدادات المنشأة", xaml);
    }

    [Fact]
    public void CompanyGroupsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CompanyGroupsPage.xaml");
        Assert.Contains("x:Name=\"GroupSummaryText\"", xaml);
        Assert.Contains("مجموعات الشركات", xaml);
    }

    [Fact]
    public void ExchangeRatesPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\ExchangeRatesPage.xaml");
        Assert.Contains("x:Name=\"RateSummaryText\"", xaml);
        Assert.Contains("أسعار الصرف", xaml);
    }

    [Fact]
    public void BomPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\BomPage.xaml");
        Assert.Contains("x:Name=\"BomSummaryText\"", xaml);
        Assert.Contains("قوائم المواد", xaml);
    }

    [Fact]
    public void WorkOrdersPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\WorkOrdersPage.xaml");
        Assert.Contains("x:Name=\"WorkOrderSummaryText\"", xaml);
        Assert.Contains("أوامر الإنتاج", xaml);
    }

    [Fact]
    public void LandedCostPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\LandedCostPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("Landed Cost", xaml);
    }

    [Fact]
    public void PosShiftsPage_has_summary_strip()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PosShiftsPage.xaml");
        Assert.Contains("x:Name=\"ShiftSummaryText\"", xaml);
        Assert.Contains("ورديات نقاط البيع", xaml);
    }

    [Fact]
    public void PosSessionPage_has_glass_hero_and_summary()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PosSessionPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("x:Name=\"PosSessionSummaryText\"", xaml);
        Assert.Contains("جلسة نقطة البيع", xaml);
    }

    [Fact]
    public void IntercompanyEliminationPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\IntercompanyEliminationPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("إزالة المعاملات بين الشركات", xaml);
    }

    [Fact]
    public void DataQualityPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\DataQualityPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("جودة البيانات", xaml);
    }

    [Fact]
    public void PlatformEventsPage_has_glass_hero()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\PlatformEventsPage.xaml");
        Assert.Contains("GlassPanel", xaml);
        Assert.Contains("أحداث المنصة", xaml);
    }

    [Fact]
    public void MainWindow_has_db_health_indicator()
    {
        var xaml = ReadDesktopFile(@"MainWindow.xaml");
        Assert.Contains("x:Name=\"StatusDbDot\"", xaml);
        Assert.Contains("x:Name=\"StatusDbText\"", xaml);
    }

    [Fact]
    public void BrqoshTheme_enables_datagrid_rtl_layout()
    {
        var xaml = ReadDesktopFile(@"Themes\BrqoshTheme.xaml");
        Assert.Contains("DataGridRtlHelper.EnableLayout", xaml);
        Assert.Contains("TargetType=\"{x:Type DataGridTextColumn}\"", xaml);
    }

    [Fact]
    public void CrmLeadsPage_has_convert_and_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\CrmLeadsPage.xaml");
        Assert.Contains("x:Name=\"LeadsGrid\"", xaml);
        Assert.Contains("x:Name=\"ConvertBtn\"", xaml);
    }

    [Fact]
    public void SalesQuotationsPage_has_toolbar_and_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SalesQuotationsPage.xaml");
        Assert.Contains("ToolbarCard", xaml);
        Assert.Contains("x:Name=\"AddBtn\"", xaml);
        Assert.Contains("عروض الأسعار", xaml);
        Assert.Contains("x:Name=\"Grid\"", xaml);
    }

    [Fact]
    public void SalesOrdersPage_has_invoice_and_new_buttons()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\SalesOrdersPage.xaml");
        Assert.Contains("x:Name=\"InvoiceBtn\"", xaml);
        Assert.Contains("x:Name=\"NewBtn\"", xaml);
        Assert.Contains("أوامر البيع", xaml);
    }

    [Fact]
    public void OrganizationsPage_has_provision_and_switch_grid()
    {
        var xaml = ReadDesktopFile(@"Views\Pages\OrganizationsPage.xaml");
        Assert.Contains("x:Name=\"ProvisionBtn\"", xaml);
        Assert.Contains("المنشآت", xaml);
        Assert.Contains("MouseDoubleClick=\"OnSwitch\"", xaml);
    }

    [Fact]
    public void OrganizationProvisionerWindow_has_industry_combo()
    {
        var xaml = ReadDesktopFile(@"Views\OrganizationProvisionerWindow.xaml");
        Assert.Contains("x:Name=\"ProfileCombo\"", xaml);
        Assert.Contains("x:Name=\"IndustryPreviewText\"", xaml);
    }
}
