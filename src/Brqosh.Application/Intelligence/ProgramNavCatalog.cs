namespace Brqosh.Application.Intelligence;

/// <summary>فهرس شاشات التنقل — يغذي المساعد عند غياب دليل مفصّل في <see cref="ProgramKnowledge"/>.</summary>
public static class ProgramNavCatalog
{
    public sealed record NavEntry(string TitleAr, string TitleEn);

    private static readonly Dictionary<string, NavEntry> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = new("لوحة التحكم", "Dashboard"),
        ["chart_of_accounts"] = new("دليل الحسابات", "Chart of Accounts"),
        ["journal_entries"] = new("القيود اليومية", "Journal Entries"),
        ["cost_centers"] = new("مراكز التكلفة", "Cost Centers"),
        ["fiscal_periods"] = new("الفترات المالية", "Fiscal Periods"),
        ["projects"] = new("المشاريع", "Projects"),
        ["contracts"] = new("العقود", "Contracts"),
        ["boq"] = new("جداول الكميات (BOQ)", "Bill of Quantities"),
        ["progress_billings"] = new("المستخلصات", "Progress Billings"),
        ["subcontractors"] = new("مقاولو الباطن", "Subcontractors"),
        ["retentions"] = new("المحتجزات", "Retentions"),
        ["project_profitability"] = new("ربحية المشاريع", "Project Profitability"),
        ["bom"] = new("قوائم المواد (BOM)", "Bill of Materials"),
        ["work_orders"] = new("أوامر الإنتاج", "Work Orders"),
        ["wip"] = new("أعمال تحت التشغيل", "Work in Progress"),
        ["pos_session"] = new("شاشة البيع", "POS Session"),
        ["pos_shifts"] = new("الورديات", "POS Shifts"),
        ["pos_reports"] = new("تقارير نقاط البيع", "POS Reports"),
        ["customers"] = new("العملاء", "Customers"),
        ["customer_statement"] = new("كشف حساب عميل", "Customer Statement"),
        ["sales_quotations"] = new("عروض الأسعار", "Sales Quotations"),
        ["sales_orders"] = new("أوامر البيع", "Sales Orders"),
        ["sales_invoices"] = new("فواتير المبيعات", "Sales Invoices"),
        ["sales_returns"] = new("مردودات المبيعات", "Sales Returns"),
        ["crm_leads"] = new("العملاء المحتملون", "Leads"),
        ["crm_opportunities"] = new("فرص البيع", "Opportunities"),
        ["crm_activities"] = new("أنشطة CRM", "CRM Activities"),
        ["suppliers"] = new("الموردون", "Suppliers"),
        ["supplier_statement"] = new("كشف حساب مورد", "Supplier Statement"),
        ["purchase_requests"] = new("طلبات الشراء", "Purchase Requests"),
        ["purchase_orders"] = new("أوامر الشراء", "Purchase Orders"),
        ["goods_receipts"] = new("أذونات استلام البضاعة", "Goods Receipts"),
        ["purchase_invoices"] = new("فواتير المشتريات", "Purchase Invoices"),
        ["debit_notes"] = new("إشعارات المدين (مشتريات)", "Debit Notes"),
        ["products"] = new("الأصناف والمنتجات", "Products"),
        ["warehouses"] = new("المستودعات", "Warehouses"),
        ["stock_movements"] = new("حركات المخزون", "Stock Movements"),
        ["stock_count"] = new("الجرد", "Stock Count"),
        ["landed_cost"] = new("التكلفة الإضافية", "Landed Cost"),
        ["receipt_vouchers"] = new("سندات القبض", "Receipt Vouchers"),
        ["payment_vouchers"] = new("سندات الصرف", "Payment Vouchers"),
        ["bank_accounts"] = new("الحسابات البنكية", "Bank Accounts"),
        ["bank_transfers"] = new("التحويلات البنكية", "Bank Transfers"),
        ["exchange_rates"] = new("أسعار الصرف", "Exchange Rates"),
        ["cheques"] = new("الشيكات", "Cheques"),
        ["bank_reconciliation"] = new("التسوية البنكية", "Bank Reconciliation"),
        ["zatca_invoices"] = new("سجل الفواتير الإلكترونية", "E-Invoice Log"),
        ["zatca_settings"] = new("إعدادات هيئة الزكاة", "ZATCA Settings"),
        ["trial_balance"] = new("ميزان المراجعة", "Trial Balance"),
        ["income_statement"] = new("قائمة الدخل", "Income Statement"),
        ["balance_sheet"] = new("قائمة المركز المالي", "Balance Sheet"),
        ["cash_flow_statement"] = new("قائمة التدفقات النقدية", "Cash Flow Statement"),
        ["consolidated_trial_balance"] = new("ميزان مراجعة موحّد", "Consolidated Trial Balance"),
        ["consolidated_income_statement"] = new("قائمة دخل موحّدة", "Consolidated Income Statement"),
        ["consolidated_balance_sheet"] = new("مركز مالي موحّد", "Consolidated Balance Sheet"),
        ["intercompany_elimination"] = new("إزالة المعاملات بين الشركات", "Intercompany Elimination"),
        ["vat_return"] = new("إقرار ضريبة القيمة المضافة", "VAT Return"),
        ["general_ledger_report"] = new("دفتر الأستاذ العام", "General Ledger"),
        ["fixed_assets"] = new("سجل الأصول", "Fixed Assets"),
        ["payroll"] = new("دورات الرواتب", "Payroll"),
        ["company_settings"] = new("إعدادات المنشأة", "Company Settings"),
        ["organizations"] = new("المنشآت", "Organizations"),
        ["company_groups"] = new("مجموعات الشركات", "Company Groups"),
        ["branches"] = new("الفروع", "Branches"),
        ["users"] = new("المستخدمون", "Users"),
        ["audit_log"] = new("سجل التدقيق", "Audit Log"),
        ["data_quality"] = new("جودة البيانات", "Data Quality"),
        ["platform_events"] = new("أحداث المنصة", "Platform Events"),
        ["customer_support"] = new("دعم العملاء", "Customer Support"),
        ["pricing_plans"] = new("الباقات والتسعير", "Pricing Plans"),
    };

    public static bool TryGet(string navKey, out NavEntry entry) =>
        Entries.TryGetValue(navKey.Trim(), out entry!);

    public static IReadOnlyCollection<string> Keys => Entries.Keys;

    public static int Count => Entries.Count;
}
