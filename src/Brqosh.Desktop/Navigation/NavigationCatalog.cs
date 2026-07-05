using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Navigation;

public record NavPage(string Key, string TitleAr, string Icon);
public record NavGroup(string TitleAr, ModuleKey Module, IReadOnlyList<NavPage> Pages);

/// <summary>
/// قوائم التنقل حسب الوحدة — تُعرض فقط قوائم الوحدات المفعّلة لنظام العمل المختار.
/// </summary>
public static class NavigationCatalog
{
    public static IReadOnlyList<NavGroup> Groups { get; } =
    [
        new("المركز التنفيذي", ModuleKey.GeneralLedger,
        [
            new("dashboard", "لوحة التحكم", "📊"),
        ]),

        new("المحاسبة العامة", ModuleKey.GeneralLedger,
        [
            new("chart_of_accounts", "دليل الحسابات", "🗂️"),
            new("journal_entries", "القيود اليومية", "📒"),
            new("cost_centers", "مراكز التكلفة", "🎯"),
            new("fiscal_periods", "الفترات المالية", "📅"),
        ]),

        new("المشاريع والمقاولات", ModuleKey.ProjectsConstruction,
        [
            new("projects", "المشاريع", "🏗️"),
            new("contracts", "العقود", "📜"),
            new("boq", "جداول الكميات (BOQ)", "📐"),
            new("progress_billings", "المستخلصات", "🧾"),
            new("subcontractors", "مقاولو الباطن", "👷"),
            new("retentions", "المحتجزات", "🔒"),
        ]),

        new("الإنتاج والتصنيع", ModuleKey.Manufacturing,
        [
            new("bom", "قوائم المواد (BOM)", "🧩"),
            new("work_orders", "أوامر الإنتاج", "⚙️"),
            new("wip", "أعمال تحت التشغيل", "🔄"),
        ]),

        new("نقاط البيع", ModuleKey.PointOfSale,
        [
            new("pos_session", "شاشة البيع", "🛒"),
            new("pos_shifts", "الورديات", "⏱️"),
            new("pos_reports", "تقارير نقاط البيع", "📈"),
        ]),

        new("المبيعات والعملاء", ModuleKey.Sales,
        [
            new("customers", "العملاء", "👥"),
            new("sales_invoices", "فواتير المبيعات", "🧾"),
            new("sales_returns", "مردودات المبيعات", "↩️"),
        ]),

        new("المشتريات والموردون", ModuleKey.Purchasing,
        [
            new("suppliers", "الموردون", "🚚"),
            new("purchase_orders", "أوامر الشراء", "📋"),
            new("purchase_invoices", "فواتير المشتريات", "🧾"),
        ]),

        new("المخزون والمستودعات", ModuleKey.Inventory,
        [
            new("products", "الأصناف والمنتجات", "📦"),
            new("warehouses", "المستودعات", "🏬"),
            new("stock_movements", "حركات المخزون", "🔀"),
            new("stock_count", "الجرد", "✅"),
        ]),

        new("الخزينة والبنوك", ModuleKey.Treasury,
        [
            new("receipt_vouchers", "سندات القبض", "💵"),
            new("payment_vouchers", "سندات الصرف", "💸"),
            new("bank_accounts", "الحسابات البنكية", "🏦"),
            new("cheques", "الشيكات", "📝"),
            new("bank_reconciliation", "التسوية البنكية", "⚖️"),
        ]),

        new("الفوترة الإلكترونية", ModuleKey.Zatca,
        [
            new("zatca_invoices", "سجل الفواتير الإلكترونية", "📡"),
            new("zatca_settings", "إعدادات هيئة الزكاة", "🛡️"),
        ]),

        new("التقارير المالية", ModuleKey.Reports,
        [
            new("trial_balance", "ميزان المراجعة", "📊"),
            new("income_statement", "قائمة الدخل", "📈"),
            new("balance_sheet", "قائمة المركز المالي", "🏛️"),
            new("vat_return", "إقرار ضريبة القيمة المضافة", "🧮"),
            new("general_ledger_report", "دفتر الأستاذ العام", "📚"),
        ]),
    ];

    public static IReadOnlyList<NavGroup> ForProfile(BusinessProfile profile)
    {
        var active = ModuleCatalog.ResolveModules(profile);
        return Groups.Where(g => active.Contains(g.Module)).ToList();
    }
}
