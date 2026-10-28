namespace Brqosh.Desktop.Ui;

public sealed record EmptyStateSpec(
    string TitleAr,
    string SubtitleAr,
    string Monogram,
    string? TitleEn = null,
    string? SubtitleEn = null)
{
    public string Title => LocaleManager.T(TitleAr, TitleEn ?? TitleAr);
    public string Subtitle => LocaleManager.T(SubtitleAr, SubtitleEn ?? SubtitleAr);
}

/// <summary>نصوص الحالة الفارغة — CEEF-039 + G-27 bilingual.</summary>
public static class EmptyStateCatalog
{
    private static readonly Dictionary<string, EmptyStateSpec> ByGridName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CustomersGrid"] = new("لا يوجد عملاء", "أضف عميلاً جديداً من شريط الأدوات أو عدّل البحث.", "CL", "No customers", "Add a customer from the toolbar or adjust search."),
        ["SuppliersGrid"] = new("لا يوجد موردون", "أضف مورداً جديداً أو عدّل معايير البحث.", "SP", "No suppliers", "Add a supplier or adjust filters."),
        ["ProductsGrid"] = new("لا توجد أصناف", "أضف صنفاً/منتجاً جديداً للبدء.", "IT", "No products", "Add an item to get started."),
        ["Grid"] = new("لا توجد سجلات", "أضف سجلاً جديداً أو عدّل الفلاتر.", "—", "No records", "Add a record or adjust filters."),
        ["LeadsGrid"] = new("لا يوجد عملاء محتملون", "أضف عميلاً محتملاً من شريط الأدوات.", "LD", "No leads", "Add a lead from the toolbar."),
        ["EntriesGrid"] = new("لا توجد قيود", "أنشئ قيداً يومياً جديداً.", "QY", "No journal entries", "Create a new journal entry."),
        ["InvoicesGrid"] = new("لا توجد فواتير", "أنشئ فاتورة جديدة من شريط الأدوات.", "FV", "No invoices", "Create a new invoice."),
        ["VouchersGrid"] = new("لا توجد سندات", "أنشئ سنداً جديداً.", "SQ", "No vouchers", "Create a new voucher."),
        ["NotesGrid"] = new("لا توجد إشعارات", "أنشئ إشعاراً جديداً.", "NT", "No notes", "Create a new note."),
        ["PeriodsGrid"] = new("لا توجد فترات", "أنشئ فترة مالية جديدة.", "FP", "No periods", "Create a fiscal period."),
        ["ReportGrid"] = new("لا توجد بيانات للتقرير", "اضبط الفلاتر ثم اضغط «عرض».", "RP", "No report data", "Set filters and click View."),
        ["GroupsGrid"] = new("لا توجد مجموعات شركات", "أنشئ مجموعة جديدة للتقارير الموحّدة.", "GRP", "No company groups", "Create a group for consolidation."),
        ["MapsGrid"] = new("لا توجد خرائط حسابات", "اربط حسابات الشركات في المجموعة المحددة.", "MAP", "No account maps", "Map intercompany accounts."),
        ["CartGrid"] = new("سلة فارغة", "اختر منتجات من القائمة لإضافتها.", "POS", "Empty cart", "Select products to add."),
        ["ProductGrid"] = new("لا توجد منتجات", "عدّل البحث أو أضف أصنافاً في المخزون.", "IT", "No products", "Adjust search or add inventory items."),
    };

    private static readonly Dictionary<string, EmptyStateSpec> ByPageKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["customers"] = new("لا يوجد عملاء", "أضف عميلاً جديداً من شريط الأدوات.", "CL", "No customers", "Add a customer from the toolbar."),
        ["crm_leads"] = new("لا يوجد عملاء محتملون", "أضف عميلاً محتملاً.", "LD", "No leads", "Add a lead."),
        ["crm_opportunities"] = new("لا توجد فرص", "أنشئ فرصة بيع جديدة.", "OP", "No opportunities", "Create a sales opportunity."),
        ["crm_activities"] = new("لا توجد أنشطة", "سجّل مكالمة أو اجتماعاً.", "AC", "No activities", "Log a call or meeting."),
        ["suppliers"] = new("لا يوجد موردون", "أضف مورداً جديداً.", "SP", "No suppliers", "Add a supplier."),
        ["products"] = new("لا توجد أصناف", "أضف صنفاً جديداً.", "IT", "No products", "Add a product."),
        ["chart_of_accounts"] = new("لا توجد حسابات", "أضف حساباً في دليل الحسابات.", "DH", "No accounts", "Add an account."),
        ["journal_entries"] = new("لا توجد قيود", "أنشئ قيداً يومياً.", "QY", "No entries", "Create a journal entry."),
        ["sales_invoices"] = new("لا توجد فواتير مبيعات", "أنشئ فاتورة جديدة.", "FV", "No sales invoices", "Create an invoice."),
        ["sales_quotations"] = new("لا توجد عروض أسعار", "أنشئ عرض سعر جديداً.", "QT", "No quotations", "Create a quotation."),
        ["sales_orders"] = new("لا توجد أوامر بيع", "أنشئ أمر بيع جديداً.", "SO", "No sales orders", "Create a sales order."),
        ["purchase_invoices"] = new("لا توجد فواتير مشتريات", "أنشئ فاتورة مشتريات.", "FP", "No purchase invoices", "Create a purchase invoice."),
        ["warehouses"] = new("لا توجد مستودعات", "أضف مستودعاً.", "WH", "No warehouses", "Add a warehouse."),
        ["product_categories"] = new("لا توجد فئات أصناف", "أضف فئة.", "CT", "No product categories", "Add a category."),
        ["users"] = new("لا يوجد مستخدمون", "أضف مستخدماً جديداً.", "US", "No users", "Add a user."),
        ["branches"] = new("لا توجد فروع", "أضف فرعاً جديداً.", "BR", "No branches", "Add a branch."),
        ["audit_log"] = new("لا توجد أحداث", "ستظهر عمليات التدقيق عند حدوثها.", "AU", "No events", "Audit events appear when posted."),
        ["exchange_rates"] = new("لا توجد أسعار صرف", "زامِن الأسعار أو أضف سعراً يدوياً.", "FX", "No exchange rates", "Sync or add a rate."),
        ["bank_accounts"] = new("لا توجد حسابات بنكية", "أضف حساباً بنكياً.", "BN", "No bank accounts", "Add a bank account."),
        ["contracts"] = new("لا توجد عقود", "أضف عقداً جديداً.", "CN", "No contracts", "Add a contract."),
        ["projects"] = new("لا توجد مشاريع", "أضف مشروعاً جديداً.", "PR", "No projects", "Add a project."),
        ["bom"] = new("لا توجد قوائم مواد", "أنشئ BOM جديداً.", "BM", "No BOMs", "Create a bill of materials."),
        ["work_orders"] = new("لا توجد أوامر إنتاج", "أنشئ أمر إنتاج.", "WO", "No work orders", "Create a work order."),
        ["fixed_assets"] = new("لا توجد أصول", "سجّل أصلاً ثابتاً.", "FA", "No fixed assets", "Register a fixed asset."),
        ["payroll"] = new("لا توجد دورات رواتب", "أنشئ دورة رواتب.", "PY", "No payroll runs", "Create a payroll run."),
        ["cheques"] = new("لا توجد شيكات", "سجّل شيكاً جديداً.", "CH", "No cheques", "Register a cheque."),
        ["zatca_invoices"] = new("لا توجد فواتير إلكترونية", "ستظهر بعد إرسال الفواتير.", "ZE", "No e-invoices", "Shown after ZATCA submission."),
        ["retentions"] = new("لا توجد محتجزات", "ستُنشأ من المستخلصات.", "RT", "No retentions", "Created from progress billings."),
        ["progress_billings"] = new("لا توجد مستخلصات", "أنشئ مستخلصاً جديداً.", "MK", "No progress billings", "Create a progress billing."),
        ["subcontractors"] = new("لا توجد اتفاقيات باطن", "أضف مقاول باطن.", "SB", "No subcontracts", "Add a subcontract."),
        ["purchase_orders"] = new("لا توجد أوامر شراء", "أنشئ أمر شراء.", "PO", "No purchase orders", "Create a PO."),
        ["goods_receipts"] = new("لا توجد أذونات استلام", "أنشئ إذن استلام.", "GR", "No goods receipts", "Create a GRN."),
        ["debit_notes"] = new("لا توجد إشعارات مدين", "أنشئ إشعار مدين.", "DN", "No debit notes", "Create a debit note."),
        ["credit_notes"] = new("لا توجد إشعارات دائن", "أنشئ إشعار دائن.", "CR", "No credit notes", "Create a credit note."),
        ["payment_vouchers"] = new("لا توجد سندات صرف", "أنشئ سند صرف.", "SX", "No payment vouchers", "Create a payment voucher."),
        ["cost_centers"] = new("لا توجد مراكز تكلفة", "أضف مركز تكلفة.", "CT", "No cost centers", "Add a cost center."),
        ["pos_shifts"] = new("لا توجد ورديات", "افتح وردية من شاشة البيع.", "SH", "No POS shifts", "Open a shift from POS."),
        ["pos_reports"] = new("لا توجد بيانات POS", "اختر فترة وعرض التقرير.", "RP", "No POS data", "Select a period and view."),
        ["landed_cost"] = new("لا توجد تكاليف إضافية", "أضف تكلفة landed cost.", "LND", "No landed costs", "Add landed cost."),
        ["wip"] = new("لا توجد أعمال تحت التشغيل", "ستظهر من أوامر الإنتاج.", "WIP", "No WIP", "Shown from work orders."),
        ["project_profitability"] = new("لا توجد بيانات ربحية", "اختر مشروعاً واضغط عرض.", "RB", "No profitability data", "Select a project and view."),
        ["stock_count"] = new("لا توجد بنود جرد", "اختر مستودعاً وابدأ الجرد.", "JD", "No count lines", "Select warehouse and count."),
        ["bank_reconciliation"] = new("لا توجد حركات للتسوية", "اختر حساباً بنكياً.", "TS", "No reconciliation items", "Select a bank account."),
        ["company_groups"] = new("لا توجد مجموعات", "أنشئ مجموعة شركات.", "GRP", "No groups", "Create a company group."),
        ["fiscal_periods"] = new("لا توجد فترات", "أنشئ فترة مالية.", "FP", "No fiscal periods", "Create a fiscal period."),
        ["boq"] = new("لا توجد بنود BOQ", "اختر مشروعاً لعرض جدول الكميات.", "BQ", "No BOQ lines", "Select a project for BOQ."),
        ["organizations"] = new("لا توجد منشآت", "أنشئ منشأة من المعالج.", "OR", "No organizations", "Provision an organization."),
    };

    public static EmptyStateSpec Resolve(string? gridName, string? pageKey = null)
    {
        // ‏اسمُ الشبكة العامّ «Grid» يشترك فيه عشراتُ الصفحات فنصُّه «لا توجد سجلات» لا يغلب نصَّ الصفحة الخاصّ بها (أوامر بيع · أنشطة CRM…).
        var generic = string.Equals(gridName, "Grid", StringComparison.OrdinalIgnoreCase);
        if (generic && !string.IsNullOrEmpty(pageKey) && ByPageKey.TryGetValue(pageKey, out var specific))
            return specific;

        if (!string.IsNullOrEmpty(gridName) && ByGridName.TryGetValue(gridName, out var byGrid))
            return byGrid;

        if (!string.IsNullOrEmpty(pageKey) && ByPageKey.TryGetValue(pageKey, out var byPage))
            return byPage;

        if (!string.IsNullOrEmpty(gridName) && ByPageKey.TryGetValue(NormalizeGridName(gridName), out byPage))
            return byPage;

        return new EmptyStateSpec(
            "لا توجد سجلات", "عدّل الفلاتر أو أضف سجلاً جديداً.", "—",
            "No records", "Adjust filters or add a record.");
    }

    internal static string NormalizeGridName(string gridName)
    {
        var name = gridName;
        if (name.EndsWith("Grid", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        return name switch
        {
            "Customers" => "customers",
            "Suppliers" => "suppliers",
            "Invoices" => "sales_invoices",
            "Entries" => "journal_entries",
            "Vouchers" => "receipt_vouchers",
            "Notes" => "credit_notes",
            "Periods" => "fiscal_periods",
            "Groups" => "company_groups",
            "Maps" => "company_groups",
            "Report" => "trial_balance",
            "Leads" => "crm_leads",
            _ => name.ToLowerInvariant(),
        };
    }
}
