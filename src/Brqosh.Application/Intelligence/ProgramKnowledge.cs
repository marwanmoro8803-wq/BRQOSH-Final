namespace Brqosh.Application.Intelligence;

/// <summary>
/// قاعدة معرفة البرنامج — حقائق in-repo فقط (لا LLM خارجي).
/// مبادئ: Fable epistemic humility · لا إغلاق كاذب على OTP/ZATCA الحي.
/// </summary>
public static class ProgramKnowledge
{
    public const string SourceId = "brqosh-program-knowledge-v1";

    public static PageGuide? TryGetPageGuide(string? navKey)
    {
        if (string.IsNullOrWhiteSpace(navKey))
            return null;
        var key = navKey.Trim();
        if (Pages.TryGetValue(key, out var detailed))
            return detailed;
        if (ProgramNavCatalog.TryGet(key, out var nav))
        {
            if (ProgramPageGuideTemplates.TryGet(key, out var template))
            {
                return new PageGuide(key, nav.TitleAr, nav.TitleEn, template.Ar, template.En);
            }

            return new PageGuide(
                key,
                nav.TitleAr,
                nav.TitleEn,
                $"أنت على «{nav.TitleAr}». اسأل: «ماذا أفعل هنا؟» · «كيف أرحّل؟» · «صلاحياتي».",
                $"You are on «{nav.TitleEn}». Ask: what to do here · how to post · your permissions.");
        }

        return null;
    }

    public static PageGuide FallbackPage(string? navKey) =>
        new(
            navKey ?? "unknown",
            "شاشة البرنامج",
            "Program screen",
            $"أنت على الشاشة «{navKey}». استخدم القائمة الجانبية للتنقل. اسأل عن: ترحيل · ZATCA · الصلاحيات · التدفق النقدي.",
            $"You are on «{navKey}». Use the side nav. Ask about: posting · ZATCA · permissions · cash flow.");

    public static FinancialAssistantReply PostingGuide() =>
        new(
            "الترحيل في BRQOSH: أنشئ المستند (فاتورة/قيد/سند) كمسودة → تحقق من السطور والضريبة → اضغط ترحيل. بعد الترحيل يُولَّد قيد محاسبي متوازن ولا يُعدَّل المستند (التصحيح بإشعار دائن/مدين). الطباعة متاحة للمستندات المرحّلة فقط.",
            "Posting in BRQOSH: create draft → validate lines/VAT → Post. A balanced journal entry is generated; posted docs are immutable (use credit/debit notes to correct). Print is allowed for posted documents only.",
            SourceId);

    public static FinancialAssistantReply ZatcaGuide() =>
        new(
            "ZATCA in-repo: إعدادات الفوترة · معالج CSID · توليد XML/QR للفواتير المرحّلة. ⏭ الإغلاق الحي (OTP + 10 فواتير مقبولة) يتطلب O-01 خارج المستودع — راجع إعدادات ZATCA ثم دليل O-01.",
            "ZATCA in-repo: settings · CSID wizard · XML/QR for posted invoices. ⏭ Live OTP closure (O-01) is external — use ZATCA settings then O-01 runbook.",
            SourceId);

    public static FinancialAssistantReply RbacGuide(string? roleName) =>
        new(
            $"الصلاحيات الحالية: {roleName ?? "غير معروف"}. المدير: كامل. المحاسب: ترحيل ومحاسبة. أمين الصندوق: قبض/صرف وPOS. المشاهد: قراءة فقط. إن رُفضت شاشة فالدور لا يملك nav key المطلوب.",
            $"Current role: {roleName ?? "unknown"}. Admin: full. Accountant: posting. Cashier: treasury/POS. Viewer: read-only. Denied screens mean missing permission for that nav key.",
            SourceId);

    public static FinancialAssistantReply ProgramHelp() =>
        new(
            "مساعد BRQOSH يجيب من بيانات شركتك الحقيقية + معرفة البرنامج. اسأل: «ماذا أفعل في هذه الشاشة؟» · «كيف أرحّل فاتورة؟» · «ZATCA» · «VAT» · «ذمم» · «التدفق النقدي» · «صلاحياتي». ⏭ لا يستبدل مراجع SOCPA أو OTP الحي.",
            "BRQOSH assistant uses your live company data + in-app knowledge. Ask about this screen, posting, ZATCA, VAT, AR/AP, cash flow, permissions. ⏭ Not a substitute for live OTP or SOCPA review.",
            SourceId);

    public sealed record PageGuide(
        string Key,
        string TitleAr,
        string TitleEn,
        string GuideAr,
        string GuideEn);

    private static readonly Dictionary<string, PageGuide> Pages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = new("dashboard", "لوحة التحكم", "Dashboard",
            "لوحة تنفيذية: مبيعات الشهر، الذمم، مؤشرات سريعة. اسأل المساعد عن التدفق أو VAT للتفصيل.",
            "Executive dashboard: monthly sales, receivables, KPIs. Ask the assistant for cash flow or VAT detail."),
        ["sales_invoices"] = new("sales_invoices", "فواتير المبيعات", "Sales invoices",
            "أنشئ فاتورة → أضف سطوراً → ترحيل. بعد الترحيل: قيد مدين عميل / دائن إيراد+VAT. للZATCA: فاتورة مرحّلة + إعدادات CSID.",
            "Create invoice → lines → Post. Entry: DR AR / CR revenue+VAT. ZATCA needs posted invoice + CSID settings."),
        ["journal_entries"] = new("journal_entries", "القيود اليومية", "Journal entries",
            "قيد يدوي: مسودة → سطور مدين=دائن → ترحيل. لا ترحيل في فترة مغلقة.",
            "Manual JE: draft → balanced lines → Post. Blocked in closed fiscal periods."),
        ["zatca_settings"] = new("zatca_settings", "إعدادات ZATCA", "ZATCA settings",
            "ربط الشهادة/CSID · بيئة محاكاة أو إنتاج. OTP الحي ⏭ O-01.",
            "Certificate/CSID · simulation or production. Live OTP ⏭ O-01."),
        ["zatca_invoices"] = new("zatca_invoices", "فواتير ZATCA", "ZATCA invoices",
            "عرض حالة الامتثال للفواتير المرحّلة: ICV · QR · حالة الإرسال.",
            "Compliance status for posted invoices: ICV · QR · submission state."),
        ["receipt_vouchers"] = new("receipt_vouchers", "سندات القبض", "Receipt vouchers",
            "تحصيل من عميل → ترحيل يخفّض الذمم ويزيد النقدية/البنك.",
            "Customer collection → posting reduces AR and increases cash/bank."),
        ["payment_vouchers"] = new("payment_vouchers", "سندات الصرف", "Payment vouchers",
            "سداد مورد → ترحيل يخفّض الذمم الدائنة.",
            "Supplier payment → posting reduces AP."),
        ["trial_balance"] = new("trial_balance", "ميزان المراجعة", "Trial balance",
            "تقرير أرصدة الحسابات لفترة — تحقق أن المدين = الدائن قبل الإقفال.",
            "Account balances for a period — verify debits equal credits before close."),
        ["vat_return"] = new("vat_return", "إقرار ضريبة القيمة المضافة", "VAT return",
            "صافي ضريبة المخرجات − المدخلات للفترة. راجع قبل تقديم ZATCA.",
            "Output VAT minus input VAT for the period. Review before ZATCA filing."),
        ["customers"] = new("customers", "العملاء", "Customers",
            "بيانات العملاء · الرقم الضريبي · شروط السداد. مطلوب قبل فواتير المبيعات.",
            "Customer master · VAT ID · payment terms. Required before sales invoices."),
        ["products"] = new("products", "الأصناف", "Products",
            "أصناف مخزنية أو خدمات — تُستخدم في الفواتير والمخزون.",
            "Stock or service items for invoices and inventory."),
        ["customer_support"] = new("customer_support", "دعم العملاء", "Customer support",
            "MVP دعم: SLA · قنوات · تذاكر placeholder — G-31 in-repo.",
            "Support MVP: SLA · channels · ticket placeholder — G-31 in-repo."),
        ["pricing_plans"] = new("pricing_plans", "الباقات والتسعير", "Pricing plans",
            "عرض خطط الاشتراك من قاعدة البيانات — G-32 MVP.",
            "Subscription plans from database — G-32 MVP."),
        ["chart_of_accounts"] = new("chart_of_accounts", "دليل الحسابات", "Chart of accounts",
            "هيكل الحسابات: مجموعات + حسابات تشغيلية. أنشئ حساباً جديداً أو عدّل قبل الترحيل.",
            "Account hierarchy: groups + posting accounts. Create or edit before posting."),
        ["projects"] = new("projects", "المشاريع", "Projects",
            "مشاريع المقاولات: BOQ · عقود · مستخلصات · محتجزات. ابدأ بمشروع ثم أضف BOQ.",
            "Construction projects: BOQ · contracts · billings · retentions. Start with a project then BOQ."),
        ["pos_session"] = new("pos_session", "شاشة البيع", "POS session",
            "نقطة البيع: افتح وردية → أضف أصناف → بِع. يُولّد فاتورة مرحّلة + ZATCA عند التفعيل.",
            "POS: open shift → add items → sell. Generates posted invoice + ZATCA when enabled."),
        ["purchase_invoices"] = new("purchase_invoices", "فواتير المشتريات", "Purchase invoices",
            "فاتورة مورد → ترحيل يزيد المخزون/المصروف + VAT مدخلات.",
            "Supplier invoice → posting increases stock/expense + input VAT."),
        ["suppliers"] = new("suppliers", "الموردون", "Suppliers",
            "بيانات الموردين قبل فواتير المشتريات وأوامر الشراء.",
            "Supplier master before purchase invoices and POs."),
        ["income_statement"] = new("income_statement", "قائمة الدخل", "Income statement",
            "إيرادات − مصروفات للفترة — تحقق من صافي الدخل قبل الإقفال.",
            "Revenue minus expenses for the period — verify net income before close."),
        ["balance_sheet"] = new("balance_sheet", "قائمة المركز المالي", "Balance sheet",
            "أصول = التزامات + حقوق ملكية في تاريخ محدد.",
            "Assets = liabilities + equity at a point in time."),
        ["company_settings"] = new("company_settings", "إعدادات المنشأة", "Company settings",
            "الرقم الضريبي · ملف العمل · الشعار · الترخيص. ملف العمل يحدد الوحدات الظاهرة.",
            "VAT ID · business profile · logo · license. Profile controls visible modules."),
        ["crm_leads"] = new("crm_leads", "العملاء المحتملون", "Leads",
            "CRM MVP: عملاء محتملون قبل فرص البيع والفواتير.",
            "CRM MVP: leads before opportunities and invoices."),
        ["bom"] = new("bom", "قوائم المواد", "Bill of materials",
            "BOM: مكونات المنتج النهائي — أساس أوامر الإنتاج.",
            "BOM: components for finished goods — basis for work orders."),
        ["work_orders"] = new("work_orders", "أوامر الإنتاج", "Work orders",
            "أمر إنتاج من BOM → استهلاك خامات + إنتاج تام.",
            "Work order from BOM → consume raw materials + produce finished goods."),
        ["purchase_orders"] = new("purchase_orders", "أوامر الشراء", "Purchase orders",
            "أمر شراء للمورد → استلام بضاعة → فاتورة مشتريات.",
            "PO to supplier → goods receipt → purchase invoice."),
        ["bank_reconciliation"] = new("bank_reconciliation", "التسوية البنكية", "Bank reconciliation",
            "مطابقة كشف البنك مع القيود — فرّق غير المطابق.",
            "Match bank statement to entries — resolve unmatched items."),
        ["cash_flow_statement"] = new("cash_flow_statement", "قائمة التدفقات النقدية", "Cash flow statement",
            "تشغيلي · استثماري · تمويلي — تحقق من صافي النقد.",
            "Operating · investing · financing — verify net cash movement."),
    };
}
