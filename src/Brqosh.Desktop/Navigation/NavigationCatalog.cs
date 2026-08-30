using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Navigation;

public record NavPage(string Key, string TitleAr);
public record NavGroup(string TitleAr, ModuleKey Module, IReadOnlyList<NavPage> Pages);

/// <summary>
/// قوائم التنقل حسب الوحدة — تُعرض فقط قوائم الوحدات المفعّلة لنظام العمل المختار.
/// أيقونات التنقل: اختصارات Monogram عبر <see cref="NavIconFactory"/> (بدون emoji).
/// </summary>
public static class NavigationCatalog
{
    public static IReadOnlyList<NavGroup> Groups { get; } =
    [
        new("المركز التنفيذي", ModuleKey.GeneralLedger,
        [
            new("dashboard", "لوحة التحكم"),
        ]),

        new("المحاسبة العامة", ModuleKey.GeneralLedger,
        [
            new("chart_of_accounts", "دليل الحسابات"),
            new("journal_entries", "القيود اليومية"),
            new("cost_centers", "مراكز التكلفة"),
            new("fiscal_periods", "الفترات المالية"),
        ]),

        new("المشاريع والمقاولات", ModuleKey.ProjectsConstruction,
        [
            new("projects", "المشاريع"),
            new("contracts", "العقود"),
            new("boq", "جداول الكميات (BOQ)"),
            new("progress_billings", "المستخلصات"),
            new("subcontractors", "مقاولو الباطن"),
            new("retentions", "المحتجزات"),
            new("project_profitability", "ربحية المشاريع"),
        ]),

        new("الإنتاج والتصنيع", ModuleKey.Manufacturing,
        [
            new("bom", "قوائم المواد (BOM)"),
            new("work_orders", "أوامر الإنتاج"),
            new("wip", "أعمال تحت التشغيل"),
        ]),

        new("نقاط البيع", ModuleKey.PointOfSale,
        [
            new("pos_session", "شاشة البيع"),
            new("pos_shifts", "الورديات"),
            new("pos_reports", "تقارير نقاط البيع"),
        ]),

        new("المبيعات والعملاء", ModuleKey.Sales,
        [
            new("customers", "العملاء"),
            new("customer_statement", "كشف حساب عميل"),
            new("sales_quotations", "عروض الأسعار"),
            new("sales_orders", "أوامر البيع"),
            new("sales_invoices", "فواتير المبيعات"),
            new("sales_returns", "مردودات المبيعات"),
        ]),

        new("إدارة علاقات العملاء", ModuleKey.Crm,
        [
            new("crm_leads", "العملاء المحتملون"),
            new("crm_opportunities", "فرص البيع"),
            new("crm_activities", "أنشطة CRM"),
        ]),

        new("المشتريات والموردون", ModuleKey.Purchasing,
        [
            new("suppliers", "الموردون"),
            new("supplier_statement", "كشف حساب مورد"),
            new("purchase_requests", "طلبات الشراء"),
            new("purchase_orders", "أوامر الشراء"),
            new("goods_receipts", "أذونات استلام البضاعة"),
            new("purchase_invoices", "فواتير المشتريات"),
            new("debit_notes", "إشعارات المدين (مشتريات)"),
        ]),

        new("المخزون والمستودعات", ModuleKey.Inventory,
        [
            new("products", "الأصناف والمنتجات"),
            new("warehouses", "المستودعات"),
            new("stock_movements", "حركات المخزون"),
            new("stock_count", "الجرد"),
            new("landed_cost", "التكلفة الإضافية"),
        ]),

        new("الخزينة والبنوك", ModuleKey.Treasury,
        [
            new("receipt_vouchers", "سندات القبض"),
            new("payment_vouchers", "سندات الصرف"),
            new("bank_accounts", "الحسابات البنكية"),
            new("bank_transfers", "التحويلات البنكية"),
            new("exchange_rates", "أسعار الصرف"),
            new("cheques", "الشيكات"),
            new("bank_reconciliation", "التسوية البنكية"),
        ]),

        new("الفوترة الإلكترونية", ModuleKey.Zatca,
        [
            new("zatca_invoices", "سجل الفواتير الإلكترونية"),
            new("zatca_settings", "إعدادات هيئة الزكاة"),
        ]),

        new("التقارير المالية", ModuleKey.Reports,
        [
            new("trial_balance", "ميزان المراجعة"),
            new("income_statement", "قائمة الدخل"),
            new("balance_sheet", "قائمة المركز المالي"),
            new("cash_flow_statement", "قائمة التدفقات النقدية"),
            new("consolidated_trial_balance", "ميزان مراجعة موحّد"),
            new("consolidated_income_statement", "قائمة دخل موحّدة"),
            new("consolidated_balance_sheet", "مركز مالي موحّد"),
            new("intercompany_elimination", "إزالة المعاملات بين الشركات"),
            new("vat_return", "إقرار ضريبة القيمة المضافة"),
            new("general_ledger_report", "دفتر الأستاذ العام"),
        ]),

        new("الأصول الثابتة", ModuleKey.FixedAssets,
        [
            new("fixed_assets", "سجل الأصول"),
        ]),

        new("الرواتب", ModuleKey.Payroll,
        [
            new("payroll", "دورات الرواتب"),
        ]),

        new("الأمان والتدقيق", ModuleKey.GeneralLedger,
        [
            new("company_settings", "إعدادات المنشأة"),
            new("organizations", "المنشآت"),
            new("company_groups", "مجموعات الشركات"),
            new("branches", "الفروع"),
            new("users", "المستخدمون"),
            new("audit_log", "سجل التدقيق"),
            new("backup", "النسخ الاحتياطي"),
            new("data_quality", "جودة البيانات"),
            new("platform_events", "أحداث المنصة"),
            new("customer_support", "دعم العملاء"),
            new("pricing_plans", "الباقات والتسعير"),
        ]),
    ];

    public static IReadOnlyList<NavGroup> ForProfile(BusinessProfile profile)
    {
        var active = ModuleCatalog.ResolveModules(profile);
        return Groups.Where(g => active.Contains(g.Module)).ToList();
    }

    /// <summary>عناوين إنجليزية للتنقل — G-27 i18n foundation.</summary>
    public static string PageTitleEn(string key) =>
        PageTitlesEn.TryGetValue(key, out var en) ? en : key;

    public static string GroupTitleEn(string titleAr) =>
        GroupTitlesEn.TryGetValue(titleAr, out var en) ? en : titleAr;

    private static readonly Dictionary<string, string> PageTitlesEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "Dashboard",
        ["chart_of_accounts"] = "Chart of Accounts",
        ["journal_entries"] = "Journal Entries",
        ["cost_centers"] = "Cost Centers",
        ["fiscal_periods"] = "Fiscal Periods",
        ["projects"] = "Projects",
        ["contracts"] = "Contracts",
        ["boq"] = "Bill of Quantities",
        ["progress_billings"] = "Progress Billings",
        ["subcontractors"] = "Subcontractors",
        ["retentions"] = "Retentions",
        ["project_profitability"] = "Project Profitability",
        ["bom"] = "Bill of Materials",
        ["work_orders"] = "Work Orders",
        ["wip"] = "Work in Progress",
        ["pos_session"] = "POS Session",
        ["pos_shifts"] = "POS Shifts",
        ["pos_reports"] = "POS Reports",
        ["customers"] = "Customers",
        ["customer_statement"] = "Customer Statement",
        ["sales_quotations"] = "Sales Quotations",
        ["sales_orders"] = "Sales Orders",
        ["sales_invoices"] = "Sales Invoices",
        ["sales_returns"] = "Sales Returns",
        ["crm_leads"] = "Leads",
        ["crm_opportunities"] = "Opportunities",
        ["crm_activities"] = "CRM Activities",
        ["suppliers"] = "Suppliers",
        ["supplier_statement"] = "Supplier Statement",
        ["purchase_requests"] = "Purchase Requests",
        ["purchase_orders"] = "Purchase Orders",
        ["goods_receipts"] = "Goods Receipts",
        ["purchase_invoices"] = "Purchase Invoices",
        ["debit_notes"] = "Debit Notes",
        ["products"] = "Products",
        ["warehouses"] = "Warehouses",
        ["stock_movements"] = "Stock Movements",
        ["stock_count"] = "Stock Count",
        ["landed_cost"] = "Landed Cost",
        ["receipt_vouchers"] = "Receipt Vouchers",
        ["payment_vouchers"] = "Payment Vouchers",
        ["bank_accounts"] = "Bank Accounts",
        ["bank_transfers"] = "Bank Transfers",
        ["exchange_rates"] = "Exchange Rates",
        ["cheques"] = "Cheques",
        ["bank_reconciliation"] = "Bank Reconciliation",
        ["zatca_invoices"] = "E-Invoice Log",
        ["zatca_settings"] = "ZATCA Settings",
        ["trial_balance"] = "Trial Balance",
        ["income_statement"] = "Income Statement",
        ["balance_sheet"] = "Balance Sheet",
        ["cash_flow_statement"] = "Cash Flow Statement",
        ["consolidated_trial_balance"] = "Consolidated Trial Balance",
        ["consolidated_income_statement"] = "Consolidated Income Statement",
        ["consolidated_balance_sheet"] = "Consolidated Balance Sheet",
        ["intercompany_elimination"] = "Intercompany Elimination",
        ["vat_return"] = "VAT Return",
        ["general_ledger_report"] = "General Ledger",
        ["fixed_assets"] = "Fixed Assets",
        ["payroll"] = "Payroll",
        ["company_settings"] = "Company Settings",
        ["backup"] = "Backup & Restore",
        ["organizations"] = "Organizations",
        ["company_groups"] = "Company Groups",
        ["branches"] = "Branches",
        ["users"] = "Users",
        ["audit_log"] = "Audit Log",
        ["data_quality"] = "Data Quality",
        ["platform_events"] = "Platform Events",
        ["customer_support"] = "Customer Support",
        ["pricing_plans"] = "Pricing Plans",
    };

    private static readonly Dictionary<string, string> GroupTitlesEn = new()
    {
        ["المركز التنفيذي"] = "Executive",
        ["المحاسبة العامة"] = "General Ledger",
        ["المشاريع والمقاولات"] = "Projects & Construction",
        ["الإنتاج والتصنيع"] = "Manufacturing",
        ["نقاط البيع"] = "Point of Sale",
        ["المبيعات والعملاء"] = "Sales & Customers",
        ["إدارة علاقات العملاء"] = "CRM",
        ["المشتريات والموردون"] = "Purchasing",
        ["المخزون والمستودعات"] = "Inventory",
        ["الخزينة والبنوك"] = "Treasury",
        ["الفوترة الإلكترونية"] = "E-Invoicing",
        ["التقارير المالية"] = "Financial Reports",
        ["الأصول الثابتة"] = "Fixed Assets",
        ["الرواتب"] = "Payroll",
        ["الأمان والتدقيق"] = "Security & Audit",
    };
}
