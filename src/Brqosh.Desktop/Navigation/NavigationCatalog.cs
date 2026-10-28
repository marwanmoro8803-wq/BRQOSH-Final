using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Navigation;

/// <param name="Requires">
/// (ح-١١٩) وحدةٌ تلزم الصفحةَ فوق وحدة مجموعتها — فإذنُ استلام البضاعة في «المشتريات» يُدخل إلى مستودع،
/// فلا يظهر لمن يشتري خدماتٍ بلا مخزون.
/// </param>
public record NavPage(string Key, string TitleAr, ModuleKey? Requires = null);
public record NavGroup(string TitleAr, ModuleKey Module, IReadOnlyList<NavPage> Pages);

/// <summary>قسمٌ في «الإعدادات» (قائمة المستخدم): عنوانٌ بلغتين ومفاتيحُ صفحاتٍ من <see cref="NavigationCatalog.Groups"/> — لا نسخةَ ثانيةٌ من الصفحات.</summary>
public record SettingsSection(string TitleAr, string TitleEn, IReadOnlyList<string> Keys);

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
            new("production_cost_sheet", "كشف تكلفة الإنتاج"),
        ]),

        new("نقاط البيع", ModuleKey.PointOfSale,
        [
            new("pos_session", "شاشة البيع"),
            new("pos_shifts", "الورديات"),
            new("pos_reports", "تقارير نقاط البيع"),
        ]),

        new("العقارات والإيجارات", ModuleKey.RealEstate,
        [
            new("properties", "العقارات والوحدات"),
            new("leases", "عقود الإيجار"),
            new("maintenance_orders", "أوامر الصيانة"),
        ]),

        new("الرعاية الصحية", ModuleKey.Healthcare,
        [
            new("patients", "المرضى"),
            new("visits", "الزيارات والفوترة"),
            new("insurance_payers", "شركات التأمين"),
            new("insurance_claims", "مطالبات التأمين"),
        ]),

        new("الضيافة", ModuleKey.Hospitality,
        [
            new("tip_pools", "أوعية الإكراميات"),
            new("menu_items", "قائمة الطعام والوصفات"),
            new("rooms", "الغرف"),
            new("reservations", "الحجوزات وحسابات النزلاء"),
        ]),

        new("اللوجستيات", ModuleKey.Logistics,
        [
            new("delivery_trips", "رحلات التوصيل"),
            new("shipments", "البوالص والتحصيل"),
        ]),

        new("المبيعات والعملاء", ModuleKey.Sales,
        [
            new("customers", "العملاء"),
            new("sales_quotations", "عروض الأسعار"),
            new("sales_orders", "أوامر البيع"),
            new("sales_invoices", "فواتير المبيعات"),
            new("sales_returns", "مردودات المبيعات"),
            new("recurring_plans", "الفوترة الدورية"),
            new("customer_statement", "كشف حساب عميل"),
            new("receivable_aging", "أعمار ديون العملاء"),
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
            new("purchase_requests", "طلبات الشراء"),
            new("purchase_orders", "أوامر الشراء"),
            new("goods_receipts", "أذونات استلام البضاعة", ModuleKey.Inventory),
            new("purchase_invoices", "فواتير المشتريات"),
            new("debit_notes", "إشعارات المدين (مشتريات)"),
            new("supplier_statement", "كشف حساب مورد"),
            new("payable_aging", "أعمار ديون الموردين"),
        ]),

        new("المخزون والمستودعات", ModuleKey.Inventory,
        [
            new("products", "الأصناف والمنتجات"),
            new("product_categories", "فئات الأصناف"),
            new("posting_accounts", "حسابات الترحيل"),
            new("units_of_measure", "وحدات القياس"),
            new("warehouses", "المستودعات"),
            new("stock_movements", "حركات المخزون"),
            new("stock_batches", "الدفعات والصلاحية"),
            new("stock_count", "الجرد"),
            new("landed_cost", "التكلفة الإضافية"),
        ]),

        new("الخزينة والبنوك", ModuleKey.Treasury,
        [
            new("bank_accounts", "الحسابات البنكية"),
            new("receipt_vouchers", "سندات القبض"),
            new("payment_vouchers", "سندات الصرف"),
            new("cheques", "الشيكات"),
            new("bank_transfers", "التحويلات البنكية"),
            new("bank_statements", "كشوف الحسابات البنكية"),
            new("bank_reconciliation", "التسوية البنكية"),
            new("cash_position", "حركة النقد والمركز النقدي"),
            new("cash_flow_forecast", "التدفق النقدي المتوقع"),
            new("exchange_rates", "أسعار الصرف"),
            new("fx_revaluation", "إعادة تقييم العملات"),
            new("monetary_revaluation", "إعادة تقييم الذمم بالعملات"),
        ]),

        new("المحاسبة العامة", ModuleKey.GeneralLedger,
        [
            new("chart_of_accounts", "دليل الحسابات"),
            new("journal_entries", "القيود اليومية"),
            new("fiscal_periods", "الفترات المالية"),
            new("cost_centers", "مراكز التكلفة"),
            new("budget_variance", "الموازنة والانحراف"),
            new("cost_center_statement", "كشف مراكز التكلفة"),
            new("branch_profitability", "ربحية الفروع"),
        ]),

        new("الأصول الثابتة", ModuleKey.FixedAssets,
        [
            new("fixed_assets", "سجل الأصول"),
        ]),

        new("الرواتب", ModuleKey.Payroll,
        [
            new("employees", "سجل الموظفين"),
            new("payroll", "دورات الرواتب"),
            new("payslips", "قسائم الرواتب"),
            new("employee_advances", "سلف الموظفين"),
            new("leave_requests", "الإجازات والغياب"),
            new("wage_protection", "ملف حماية الأجور"),
            new("gosi_policy", "نسب التأمينات"),
        ]),

        new("الفوترة الإلكترونية", ModuleKey.Zatca,
        [
            new("zatca_invoices", "سجل الفواتير الإلكترونية"),
            new("zatca_settings", "إعدادات هيئة الزكاة"),
        ]),

        new("التقارير المالية", ModuleKey.Reports,
        [
            new("trial_balance", "ميزان المراجعة"),
            new("general_ledger_report", "دفتر الأستاذ العام"),
            new("income_statement", "قائمة الدخل"),
            new("balance_sheet", "قائمة المركز المالي"),
            new("equity_statement", "قائمة التغيّرات في حقوق الملكية"),
            new("cash_flow_statement", "قائمة التدفقات النقدية"),
            new("vat_return", "إقرار ضريبة القيمة المضافة"),
            new("consolidated_trial_balance", "ميزان مراجعة موحّد"),
            new("consolidated_income_statement", "قائمة دخل موحّدة"),
            new("consolidated_balance_sheet", "مركز مالي موحّد"),
            new("intercompany_elimination", "إزالة المعاملات بين الشركات"),
        ]),

        new("الرقابة والتدقيق", ModuleKey.GeneralLedger,
        [
            new("company_settings", "إعدادات المنشأة"),
            new("organizations", "المنشآت"),
            new("company_groups", "مجموعات الشركات"),
            new("branches", "الفروع"),
            new("users", "المستخدمون"),
            new("audit_log", "سجل التدقيق"),
            new("soft_deleted_rows", "المحذوفات"),
            new("data_migration", "ترحيل البيانات"),
            new("backup", "النسخ الاحتياطي"),
            new("diagnostics", "حالة النظام"),
            new("data_quality", "جودة البيانات"),
            new("platform_events", "أحداث المنصة"),
            new("pricing_plans", "الباقات والتسعير"),
        ]),

        // (طلبُ المالك 2026-10-26) صفحتا «تفضيلات البرنامج» و«الدعم والشكاوى» تُعرضان في قائمة المستخدم لا في الشريط الجانبيّ؛
        // وتبقيان هنا في الكتالوج ليبقى حجبُ الصلاحيّة والترخيص ونظامِ العمل عليهما كما كان.
        new("البرنامج", ModuleKey.GeneralLedger,
        [
            new("preferences", "تفضيلات البرنامج"),
            new("customer_support", "الدعم والشكاوى"),
        ]),
    ];

    /// <summary>
    /// (طلبُ المالك 2026-10-26) <b>كلُّ ما يخصّ الإعدادات في خانةٍ واحدة</b> تُفتح من قائمة المستخدم أسفل القائمة الجانبيّة — وتُخرَج هذه الصفحاتُ من القائمة الجانبيّة.
    /// الأقسامُ بيانات: صفحةٌ جديدةٌ للإعدادات تُضاف هنا بمفتاحها فتظهر في القائمة؛ وتبقى الصفحاتُ في <see cref="Groups"/> فيبقى حجبُ الترخيص والصلاحيات كما هو.
    /// </summary>
    public static IReadOnlyList<SettingsSection> SettingsSections { get; } =
    [
        new("تفضيلاتي", "My preferences", ["preferences"]),
        new("المنشأة", "Company", ["company_settings", "branches", "organizations", "company_groups"]),
        new("الامتثال والرواتب", "Compliance & payroll", ["zatca_settings", "gosi_policy"]),
        new("المستخدمون والنظام", "Users & system", ["users", "backup", "data_migration", "diagnostics", "pricing_plans"]),
    ];

    /// <summary>«الدعم والشكاوى» بند مستقلٌّ في قائمة المستخدم (متاحٌ لكلّ دور) — ليس إعداداً.</summary>
    public const string SupportKey = "customer_support";

    /// <summary>مفتاحُ كلِّ صفحةٍ تنتقل من القائمة الجانبيّة إلى قائمة المستخدم.</summary>
    public static bool IsInUserMenu(string pageKey) =>
        string.Equals(pageKey, SupportKey, StringComparison.Ordinal)
        || SettingsSections.Any(s => s.Keys.Contains(pageKey, StringComparer.Ordinal));

    /// <param name="licensed">
    /// ‏(ح-١٦٤ · security-licensing-tenancy#2) الوحداتُ التي يمنحها الترخيص — و<c>null</c> لا يحصر.
    /// ‏فالقائمةُ لنظام العمل <b>وللترخيص معاً</b> (<see cref="ModuleCatalog.RestrictToLicense"/>).
    /// </param>
    public static IReadOnlyList<NavGroup> ForProfile(BusinessProfile profile, IReadOnlySet<ModuleKey>? licensed = null)
    {
        var active = ModuleCatalog.RestrictToLicense(ModuleCatalog.ResolveModules(profile), licensed);
        return Groups
            .Where(g => active.Contains(g.Module))
            .Select(g => g.Pages.All(p => p.Requires is null || active.Contains(p.Requires.Value))
                ? g
                : g with { Pages = g.Pages.Where(p => p.Requires is null || active.Contains(p.Requires.Value)).ToList() })
            .ToList();
    }

    /// <summary>
    /// ‏(ح-١٦٤) <b>شاشةٌ يعرضها نظامُ العمل ويحجبها الترخيص</b> — فيرفضها <c>Navigate</c> نفسُه، لا القائمةُ وحدَها:
    /// ‏الشاشةُ تُفتح كذلك من روابط اللوحة والشاشات الأخرى بمفتاحها، فإخفاءُ زرّها وحدَه لا يحجب شيئاً.
    /// ‏وما لا يعرضه نظامُ العمل أصلاً ليس من شأن الترخيص — يبقى كما كان.
    /// </summary>
    public static bool IsWithheldByLicense(string pageKey, BusinessProfile profile, IReadOnlySet<ModuleKey>? licensed)
    {
        if (licensed is null)
            return false;

        static bool Shows(IReadOnlyList<NavGroup> groups, string key) =>
            groups.Any(g => g.Pages.Any(p => p.Key == key));

        return Shows(ForProfile(profile), pageKey) && !Shows(ForProfile(profile, licensed), pageKey);
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
        ["branch_profitability"] = "Branch Profitability",
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
        ["production_cost_sheet"] = "Production Cost Sheet",
        ["pos_session"] = "POS Session",
        ["pos_shifts"] = "POS Shifts",
        ["pos_reports"] = "POS Reports",
        ["customers"] = "Customers",
        ["customer_statement"] = "Customer Statement",
        ["receivable_aging"] = "Receivable Aging",
        ["sales_quotations"] = "Sales Quotations",
        ["sales_orders"] = "Sales Orders",
        ["sales_invoices"] = "Sales Invoices",
        ["sales_returns"] = "Sales Returns",
        ["crm_leads"] = "Leads",
        ["crm_opportunities"] = "Opportunities",
        ["crm_activities"] = "CRM Activities",
        ["suppliers"] = "Suppliers",
        ["supplier_statement"] = "Supplier Statement",
        ["payable_aging"] = "Payable Aging",
        ["purchase_requests"] = "Purchase Requests",
        ["purchase_orders"] = "Purchase Orders",
        ["goods_receipts"] = "Goods Receipts",
        ["purchase_invoices"] = "Purchase Invoices",
        ["debit_notes"] = "Debit Notes",
        ["products"] = "Products",
        ["product_categories"] = "Product categories",
        ["posting_accounts"] = "Posting accounts",
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
        ["balance_sheet"] = "Statement of Financial Position",
        ["equity_statement"] = "Statement of Changes in Equity",
        ["cash_flow_statement"] = "Cash Flow Statement",
        ["consolidated_trial_balance"] = "Consolidated Trial Balance",
        ["consolidated_income_statement"] = "Consolidated Income Statement",
        ["consolidated_balance_sheet"] = "Consolidated Statement of Financial Position",
        ["intercompany_elimination"] = "Intercompany Elimination",
        ["vat_return"] = "VAT Return",
        ["general_ledger_report"] = "General Ledger",
        ["fixed_assets"] = "Fixed Assets",
        ["payroll"] = "Payroll",
        ["properties"] = "Properties & Units",
        ["leases"] = "Lease Contracts",
        ["patients"] = "Patients",
        ["visits"] = "Visits & Billing",
        ["insurance_payers"] = "Insurance Payers",
        ["insurance_claims"] = "Insurance Claims",
        ["tip_pools"] = "Tip Pools",
        ["maintenance_orders"] = "Maintenance Orders",
        ["delivery_trips"] = "Delivery Trips",
        ["menu_items"] = "Menu & Recipes",
        ["rooms"] = "Rooms",
        ["reservations"] = "Reservations & Folios",
        ["shipments"] = "Waybills & COD",
        ["units_of_measure"] = "Units of Measure",
        ["stock_batches"] = "Batches & Expiry",
        ["recurring_plans"] = "Recurring Billing",
        ["cost_center_statement"] = "Cost Center Statement",
        ["budget_variance"] = "Budget & Variance",
        ["employees"] = "Employees",
        ["gosi_policy"] = "GOSI Rates",
        ["payslips"] = "Payslips",
        ["employee_advances"] = "Employee Advances",
        ["leave_requests"] = "Leave & Absence",
        ["wage_protection"] = "Wage Protection File",
        ["company_settings"] = "Company Settings",
        ["backup"] = "Backup & Restore",
        ["diagnostics"] = "System Status",
        ["organizations"] = "Organizations",
        ["company_groups"] = "Company Groups",
        ["branches"] = "Branches",
        ["users"] = "Users",
        ["audit_log"] = "Audit Log",
        ["soft_deleted_rows"] = "Deleted Records",
        ["fx_revaluation"] = "FX Revaluation",
        ["monetary_revaluation"] = "Monetary Revaluation",
        ["bank_statements"] = "Bank Statements",
        ["cash_position"] = "Cash Movement & Position",
        ["cash_flow_forecast"] = "Cash Flow Forecast",
        ["data_migration"] = "Data Migration",
        ["data_quality"] = "Data Quality",
        ["platform_events"] = "Platform Events",
        ["customer_support"] = "Support & Complaints",
        ["preferences"] = "Preferences",
        ["pricing_plans"] = "Pricing Plans",
    };

    private static readonly Dictionary<string, string> GroupTitlesEn = new()
    {
        ["المركز التنفيذي"] = "Executive",
        ["الإعدادات"] = "Settings",
        ["المحاسبة العامة"] = "General Ledger",
        ["المشاريع والمقاولات"] = "Projects & Construction",
        ["الإنتاج والتصنيع"] = "Manufacturing",
        ["نقاط البيع"] = "Point of Sale",
        ["المبيعات والعملاء"] = "Sales & Customers",
        ["إدارة علاقات العملاء"] = "CRM",
        ["المشتريات والموردون"] = "Purchasing",
        ["المخزون والمستودعات"] = "Inventory",
        ["العقارات والإيجارات"] = "Real Estate & Leasing",
        ["الرعاية الصحية"] = "Healthcare",
        ["الضيافة"] = "Hospitality",
        ["اللوجستيات"] = "Logistics",
        ["الخزينة والبنوك"] = "Treasury",
        ["الفوترة الإلكترونية"] = "E-Invoicing",
        ["التقارير المالية"] = "Financial Reports",
        ["الأصول الثابتة"] = "Fixed Assets",
        ["الرواتب"] = "Payroll",
        ["الرقابة والتدقيق"] = "Oversight & Audit",
        ["البرنامج"] = "Program",
    };
}
