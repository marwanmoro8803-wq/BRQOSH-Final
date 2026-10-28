using System.Windows.Controls;

namespace Brqosh.Desktop.Navigation;

/// <summary>
/// (CEEF-230 · ط6-١) <b>مفتاحُ القائمة ⟵ الشاشة — في مكانٍ واحدٍ يُقاس.</b>
///
/// <para><b>ما كان:</b> الربطُ <c>switch</c> داخل <c>MainWindow.NavigateTo</c>، <b>وفرعُه
/// الافتراضيّ يعرض «هذه الشاشة قيد البناء»</b>. فبندٌ في القائمة بلا حالةٍ في التبديل
/// <b>يُفتح على لوحةٍ فارغة</b> — ولا اختبارَ يراه: <c>MainWindow</c> لا يُبنى في
/// الاختبارات، و<c>PageConstructionTests</c> يُثبت أنّ الصفحاتِ <b>تقوم</b> لا أنّ
/// القائمةَ <b>تصل إليها</b>.</para>
///
/// <para><b>وقاموسٌ لا <c>switch</c>:</b> السؤالُ «هل لهذا البند شاشة؟» يجب أن يُجاب
/// <b>بلا إنشاء</b> — وإنشاءُ عنصر واجهةٍ يشترط خيطاً من نوع <c>STA</c>، فيصير الحارسُ
/// رهينَ بيئةِ التشغيل. <b>والقاموسُ يُجيب بمفتاحٍ ويُنشئ عند الطلب</b>، فيُقاس الربطُ
/// في أيّ خيط.</para>
///
/// <para><b>وثلاثُ قوائمَ كان يجب أن تتّفق ولا شيءَ يُلزمها:</b> <c>NavigationCatalog</c>
/// وهذا الربطُ و<c>NavKeyModuleMap</c> في أداة التدقيق. <b>وقياسُ اتّفاقها هو الآلة
/// التي تُبنى قبل الشاشات</b>.</para>
/// </summary>
public static class PageFactory
{
    private static readonly Dictionary<string, Func<UserControl>> Pages = new(StringComparer.Ordinal)
    {
        ["dashboard"] = () => new Views.Pages.DashboardPage(),
        ["chart_of_accounts"] = () => new Views.Pages.ChartOfAccountsPage(),
        ["journal_entries"] = () => new Views.Pages.JournalEntriesPage(),
        ["trial_balance"] = () => new Views.Pages.TrialBalancePage(),
        ["customers"] = () => new Views.Pages.CustomersPage(),
        ["customer_statement"] = () => new Views.Pages.CustomerStatementPage(),
        ["receivable_aging"] = () => new Views.Pages.ReceivableAgingPage(),
        ["payable_aging"] = () => new Views.Pages.PayableAgingPage(),
        ["branch_profitability"] = () => new Views.Pages.BranchProfitabilityPage(),
        ["gosi_policy"] = () => new Views.Pages.GosiPolicyPage(),
        ["diagnostics"] = () => new Views.Pages.DiagnosticsPage(),
        ["cash_flow_forecast"] = () => new Views.Pages.CashFlowForecastPage(),
        ["insurance_payers"] = () => new Views.Pages.InsurancePayersPage(),
        ["sales_quotations"] = () => new Views.Pages.SalesQuotationsPage(),
        ["sales_orders"] = () => new Views.Pages.SalesOrdersPage(),
        ["sales_invoices"] = () => new Views.Pages.SalesInvoicesPage(),
        ["fiscal_periods"] = () => new Views.Pages.FiscalPeriodsPage(),
        ["sales_returns"] = () => new Views.Pages.CreditNotesPage(),
        ["crm_leads"] = () => new Views.Pages.CrmLeadsPage(),
        ["crm_opportunities"] = () => new Views.Pages.CrmOpportunitiesPage(),
        ["crm_activities"] = () => new Views.Pages.CrmActivitiesPage(),
        ["receipt_vouchers"] = () => new Views.Pages.ReceiptVouchersPage(),
        ["payment_vouchers"] = () => new Views.Pages.PaymentVouchersPage(),
        ["suppliers"] = () => new Views.Pages.SuppliersPage(),
        ["supplier_statement"] = () => new Views.Pages.SupplierStatementPage(),
        ["purchase_requests"] = () => new Views.Pages.PurchaseRequestsPage(),
        ["purchase_orders"] = () => new Views.Pages.PurchaseOrdersPage(),
        ["goods_receipts"] = () => new Views.Pages.GoodsReceiptsPage(),
        ["purchase_invoices"] = () => new Views.Pages.PurchaseInvoicesPage(),
        ["debit_notes"] = () => new Views.Pages.DebitNotesPage(),
        ["products"] = () => new Views.Pages.ProductsPage(),
        ["product_categories"] = () => new Views.Pages.ProductCategoriesPage(),
        ["posting_accounts"] = () => new Views.Pages.PostingAccountsPage(),
        ["warehouses"] = () => new Views.Pages.WarehousesPage(),
        ["stock_movements"] = () => new Views.Pages.StockMovementsPage(),
        ["stock_count"] = () => new Views.Pages.StockCountPage(),
        ["landed_cost"] = () => new Views.Pages.LandedCostPage(),
        ["bank_accounts"] = () => new Views.Pages.BankAccountsPage(),
        ["bank_transfers"] = () => new Views.Pages.BankTransfersPage(),
        ["exchange_rates"] = () => new Views.Pages.ExchangeRatesPage(),
        ["cheques"] = () => new Views.Pages.ChequesPage(),
        ["bank_reconciliation"] = () => new Views.Pages.BankReconciliationPage(),
        ["cost_centers"] = () => new Views.Pages.CostCentersPage(),
        ["fixed_assets"] = () => new Views.Pages.FixedAssetsPage(),
        ["payroll"] = () => new Views.Pages.PayrollPage(),
        ["employees"] = () => new Views.Pages.EmployeesPage(),
        ["properties"] = () => new Views.Pages.PropertiesPage(),
        ["leases"] = () => new Views.Pages.LeasesPage(),
        ["patients"] = () => new Views.Pages.PatientsPage(),
        ["visits"] = () => new Views.Pages.VisitsPage(),
        ["insurance_claims"] = () => new Views.Pages.InsuranceClaimsPage(),
        ["tip_pools"] = () => new Views.Pages.TipPoolsPage(),
        ["maintenance_orders"] = () => new Views.Pages.MaintenanceOrdersPage(),
        ["delivery_trips"] = () => new Views.Pages.DeliveryTripsPage(),
        ["menu_items"] = () => new Views.Pages.MenuItemsPage(),
        ["rooms"] = () => new Views.Pages.RoomsPage(),
        ["reservations"] = () => new Views.Pages.ReservationsPage(),
        ["shipments"] = () => new Views.Pages.ShipmentsPage(),
        ["units_of_measure"] = () => new Views.Pages.UnitsOfMeasurePage(),
        ["stock_batches"] = () => new Views.Pages.StockBatchesPage(),
        ["recurring_plans"] = () => new Views.Pages.RecurringPlansPage(),
        ["cost_center_statement"] = () => new Views.Pages.CostCenterStatementPage(),
        ["budget_variance"] = () => new Views.Pages.BudgetVariancePage(),
        ["payslips"] = () => new Views.Pages.PayslipsPage(),
        ["employee_advances"] = () => new Views.Pages.EmployeeAdvancesPage(),
        ["leave_requests"] = () => new Views.Pages.LeaveRequestsPage(),
        ["wage_protection"] = () => new Views.Pages.WageProtectionPage(),
        ["projects"] = () => new Views.Pages.ProjectsPage(),
        ["contracts"] = () => new Views.Pages.ContractsPage(),
        ["boq"] = () => new Views.Pages.BoqPage(),
        ["progress_billings"] = () => new Views.Pages.ProgressBillingsPage(),
        ["subcontractors"] = () => new Views.Pages.SubcontractorsPage(),
        ["retentions"] = () => new Views.Pages.RetentionsPage(),
        ["project_profitability"] = () => new Views.Pages.ProjectProfitabilityPage(),
        ["bom"] = () => new Views.Pages.BomPage(),
        ["work_orders"] = () => new Views.Pages.WorkOrdersPage(),
        ["wip"] = () => new Views.Pages.WipPage(),
        ["production_cost_sheet"] = () => new Views.Pages.ProductionCostSheetPage(),
        ["pos_session"] = () => new Views.Pages.PosSessionPage(),
        ["pos_shifts"] = () => new Views.Pages.PosShiftsPage(),
        ["pos_reports"] = () => new Views.Pages.PosReportsPage(),
        ["income_statement"] = () => new Views.Pages.IncomeStatementPage(),
        ["balance_sheet"] = () => new Views.Pages.BalanceSheetPage(),
        ["equity_statement"] = () => new Views.Pages.EquityStatementPage(),
        ["cash_flow_statement"] = () => new Views.Pages.CashFlowStatementPage(),
        ["consolidated_trial_balance"] = () => new Views.Pages.ConsolidatedTrialBalancePage(),
        ["consolidated_income_statement"] = () => new Views.Pages.ConsolidatedIncomeStatementPage(),
        ["consolidated_balance_sheet"] = () => new Views.Pages.ConsolidatedBalanceSheetPage(),
        ["intercompany_elimination"] = () => new Views.Pages.IntercompanyEliminationPage(),
        ["vat_return"] = () => new Views.Pages.VatReturnPage(),
        ["general_ledger_report"] = () => new Views.Pages.GeneralLedgerPage(),
        ["zatca_invoices"] = () => new Views.Pages.ZatcaInvoicesPage(),
        ["zatca_settings"] = () => new Views.Pages.ZatcaSettingsPage(),
        ["users"] = () => new Views.Pages.UsersPage(),
        ["audit_log"] = () => new Views.Pages.AuditLogPage(),
        ["soft_deleted_rows"] = () => new Views.Pages.SoftDeletedRowsPage(),
        ["fx_revaluation"] = () => new Views.Pages.FxRevaluationPage(),
        ["monetary_revaluation"] = () => new Views.Pages.MonetaryRevaluationPage(),
        ["bank_statements"] = () => new Views.Pages.BankStatementsPage(),
        ["cash_position"] = () => new Views.Pages.CashPositionPage(),
        ["data_migration"] = () => new Views.Pages.DataMigrationPage(),
        ["data_quality"] = () => new Views.Pages.DataQualityPage(),
        ["platform_events"] = () => new Views.Pages.PlatformEventsPage(),
        ["customer_support"] = () => new Views.Pages.CustomerSupportPage(),
        ["preferences"] = () => new Views.Pages.PreferencesPage(),
        ["pricing_plans"] = () => new Views.Pages.PricingPlansPage(),
        ["backup"] = () => new Views.Pages.BackupPage(),
        ["company_groups"] = () => new Views.Pages.CompanyGroupsPage(),
        ["branches"] = () => new Views.Pages.BranchesPage(),
    };

    /// <summary>
    /// شاشتان تحتاجان سياقَ النافذة، فتُبنيان في <c>MainWindow</c> — <b>وإعلانُهما هنا
    /// يمنع أن يُقرأ غيابُهما من الربط نقصاً</b>.
    /// </summary>
    public static readonly IReadOnlySet<string> WindowScopedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "company_settings", "organizations" };

    /// <summary>كلُّ مفتاحٍ مربوطٍ — ومصدرُه القاموسُ نفسُه، فلا تُكرَّر قائمةٌ رابعة.</summary>
    public static IReadOnlyCollection<string> WiredKeys { get; } =
        [.. Pages.Keys, .. WindowScopedKeys];

    /// <summary>
    /// هل لهذا البند شاشةٌ حقيقية؟ — <b>وهذا هو ما يُقاس، بلا إنشاءٍ ولا خيطِ واجهة</b>.
    /// </summary>
    public static bool HasScreen(string pageKey) =>
        WindowScopedKeys.Contains(pageKey) || Pages.ContainsKey(pageKey);

    /// <summary>يُنشئ شاشةَ المفتاح — و<c>null</c> يعني <b>لا شاشةَ لهذا البند</b>.</summary>
    public static UserControl? Create(string pageKey) =>
        Pages.TryGetValue(pageKey, out var factory) ? factory() : null;
}
