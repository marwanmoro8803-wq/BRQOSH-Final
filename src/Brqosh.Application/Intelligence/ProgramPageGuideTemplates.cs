namespace Brqosh.Application.Intelligence;

/// <summary>أدلة تشغيلية مفصّلة للشاشات غير المغطاة في <see cref="ProgramKnowledge"/> الرئيسي.</summary>
public static class ProgramPageGuideTemplates
{
    private static readonly Dictionary<string, (string Ar, string En)> Templates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cost_centers"] = (
                "أنشئ/عدّل مركز تكلفة واربطه بالمستندات والقيود. التقارير تُفلتر حسب المركز بعد الترحيل.",
                "Create/edit cost centers and link to documents and entries. Reports filter by center after posting."),
            ["fiscal_periods"] = (
                "افتح فترة مالية قبل الترحيل · أغلقها بعد الإقفال. لا ترحيل في فترة مغلقة.",
                "Open fiscal period before posting · close after month-end. No posting in closed periods."),
            ["contracts"] = (
                "عقد مشروع: ربط بمشروع مقاولات · قيمة · مراحل. يغذي المستخلصات والمحتجزات.",
                "Project contract: link to construction project · value · milestones. Feeds billings and retentions."),
            ["boq"] = (
                "جدول كميات (BOQ): أضف بنود الكميات للمشروع · الوحدة · السعر · الكمية المخططة.",
                "BOQ: add line items · unit · rate · planned quantity for the project."),
            ["progress_billings"] = (
                "مستخلص: نسبة إنجاز أو كميات منفّذة → ترحيل يُولّد إيراد مقاولات وذمم.",
                "Progress billing: % complete or executed qty → posting generates construction revenue and AR."),
            ["subcontractors"] = (
                "مقاول باطن: بيانات المورد + عقد فرعي · ربط بمشروع · محتجزات عند الصرف.",
                "Subcontractor: vendor data + sub-contract · project link · retentions on payment."),
            ["retentions"] = (
                "محتجزات: نسبة محجوزة من مستخلص/مقاول · تُحرّر لاحقاً بقيد عكسي.",
                "Retentions: withheld % from billing/subcontract · released later via reversing entry."),
            ["project_profitability"] = (
                "تقرير ربحية: إيرادات المشروع − تكاليف مباشرة وغير مباشرة. يعتمد على مستندات مرحّلة.",
                "Profitability: project revenue minus direct/indirect costs. Requires posted documents."),
            ["wip"] = (
                "أعمال تحت التشغيل: رصيد أوامر الإنتاج غير المغلقة · يُصفّر عند إغلاق أمر الإنتاج.",
                "WIP: open work order balances · cleared when work order closes."),
            ["pos_shifts"] = (
                "وردية POS: افتح قبل البيع · أغلق في نهاية اليوم · راجع الفروقات النقدية.",
                "POS shift: open before sales · close end of day · review cash variances."),
            ["pos_reports"] = (
                "تقارير POS: مبيعات الوردية · طرق الدفع · ZATCA للفواتير المرحّلة.",
                "POS reports: shift sales · payment methods · ZATCA for posted invoices."),
            ["customer_statement"] = (
                "كشف عميل: اختر العميل والفترة · يعرض فواتير · قبض · رصيد.",
                "Customer statement: pick customer and period · invoices · receipts · balance."),
            ["sales_quotations"] = (
                "عرض سعر: مسودة → إرسال للعميل → تحويل لأمر بيع/فاتورة عند القبول.",
                "Quotation: draft → send → convert to SO/invoice when accepted."),
            ["sales_orders"] = (
                "أمر بيع: حجز كميات · تحويل لفاتورة عند التسليم · لا ترحيل محاسبي حتى الفاتورة.",
                "Sales order: reserve qty · invoice on delivery · no GL until invoice posted."),
            ["sales_returns"] = (
                "مردود مبيعات: إشعار دائن من فاتورة مرحّلة · يعكس الإيراد والضريبة والذمم.",
                "Sales return: credit note from posted invoice · reverses revenue, VAT, AR."),
            ["crm_opportunities"] = (
                "فرصة بيع: مرحلة · قيمة متوقعة · ربط بعميل محتمل · تحويل لفاتورة لاحقاً.",
                "Opportunity: stage · expected value · lead link · convert to invoice later."),
            ["crm_activities"] = (
                "أنشطة CRM: مكالمات · زيارات · متابعات — لا ترحيل محاسبي.",
                "CRM activities: calls · visits · follow-ups — no accounting posting."),
            ["supplier_statement"] = (
                "كشف مورد: فواتير مشتريات · مدفوعات · رصيد مستحق.",
                "Supplier statement: purchase invoices · payments · outstanding balance."),
            ["purchase_requests"] = (
                "طلب شراء: موافقات workflow · تحويل لأمر شراء عند الاعتماد.",
                "Purchase request: workflow approvals · convert to PO when approved."),
            ["goods_receipts"] = (
                "إذن استلام: استلام بضاعة مقابل أمر شراء · يزيد المخزون عند الترحيل.",
                "Goods receipt: receive against PO · increases stock on posting."),
            ["debit_notes"] = (
                "إشعار مدين (مشتريات): تصحيح فاتورة مورد مرحّلة · يخفّض الذمم الدائنة.",
                "Debit note (purchases): correct posted supplier invoice · reduces AP."),
            ["warehouses"] = (
                "مستودعات: تعريف مواقع التخزين · ربط بحركات المخزون والجرد.",
                "Warehouses: storage locations · linked to movements and stock count."),
            ["stock_movements"] = (
                "حركة مخزون: تحويل بين مستودعات أو تسوية · ترحيل يحدّث الأرصدة.",
                "Stock movement: transfer between warehouses or adjustment · posting updates balances."),
            ["stock_count"] = (
                "جرد: عدّ فعلي مقابل نظام · فروقات تُرحّل كتسوية مخزون.",
                "Stock count: physical vs system · variances post as inventory adjustment."),
            ["landed_cost"] = (
                "تكلفة إضافية: شحن/جمارك تُوزّع على أصناف الاستلام · ترفع تكلفة المخزون.",
                "Landed cost: freight/duty allocated to receipt lines · raises inventory cost."),
            ["bank_accounts"] = (
                "حساب بنكي: ربط بدفتر الحسابات · يُستخدم في سندات القبض/الصرف.",
                "Bank account: GL link · used in receipt/payment vouchers."),
            ["bank_transfers"] = (
                "تحويل بنكي: من حساب لآخر · قيد داخلي بين نقدية/بنوك.",
                "Bank transfer: between accounts · internal cash/bank journal."),
            ["exchange_rates"] = (
                "أسعار صرف: إدخال يدوي أو مزامنة · تُستخدم في إعادة التقييم والفواتير الأجنبية.",
                "Exchange rates: manual or sync · used in revaluation and foreign invoices."),
            ["cheques"] = (
                "شيكات: تحت التحصيل/الصرف · حالة (مودع · محصل · مرتد) تؤثر على الخزينة.",
                "Cheques: receivable/payable · status (deposited · cleared · bounced) affects treasury."),
            ["consolidated_trial_balance"] = (
                "ميزان موحّد: تجميع شركات المجموعة · تحقق من التوازن قبل الإقفال.",
                "Consolidated TB: group companies rollup · verify balance before close."),
            ["consolidated_income_statement"] = (
                "قائمة دخل موحّدة: إيرادات ومصروفات المجموعة بعد التحويلات.",
                "Consolidated P&L: group revenue/expense after translation."),
            ["consolidated_balance_sheet"] = (
                "مركز مالي موحّد: أصول والتزامات المجموعة في تاريخ واحد.",
                "Consolidated balance sheet: group assets/liabilities at one date."),
            ["intercompany_elimination"] = (
                "إزالة بين الشركات: قيود لإلغاء معاملات داخل المجموعة قبل التقارير الموحّدة.",
                "IC elimination: entries to remove intercompany transactions before consolidated reports."),
            ["general_ledger_report"] = (
                "دفتر الأستاذ: حركات حساب محدد لفترة · مدين/دائن/رصيد.",
                "General ledger: account movements for period · debit/credit/balance."),
            ["fixed_assets"] = (
                "أصول ثابتة: تسجيل · إهلاك دوري · استبعاد · قيود تلقائية عند الترحيل.",
                "Fixed assets: register · periodic depreciation · disposal · auto entries on post."),
            ["payroll"] = (
                "رواتب: دورة · استحقاقات · استقطاعات · ترحيل يُولّد قيود الرواتب.",
                "Payroll: cycle · earnings · deductions · posting generates payroll entries."),
            ["organizations"] = (
                "منشآت: هيكل المستأجر/الشركة الأم · يغذي التوحيد والفروع.",
                "Organizations: tenant/parent structure · feeds consolidation and branches."),
            ["company_groups"] = (
                "مجموعات شركات: تجميع للتقارير الموحّدة والإزالة بين الشركات.",
                "Company groups: rollup for consolidated reporting and IC elimination."),
            ["branches"] = (
                "فروع: BranchId على المستندات · منتقي الفرع في الجلسة.",
                "Branches: BranchId on documents · branch picker in session."),
            ["users"] = (
                "مستخدمون: حساب · دور · MustChangePassword · ربط بصلاحيات التنقل.",
                "Users: account · role · password policy · nav permissions."),
            ["audit_log"] = (
                "سجل تدقيق: كل ترحيل/تعديل حرج · غير قابل للحذف in-repo.",
                "Audit log: every critical post/change · immutable in-repo."),
            ["data_quality"] = (
                "جودة البيانات: فحوصات ذمم · فترات · أرصدة · تنبيهات قبل الإقفال.",
                "Data quality: AR/period/balance checks · alerts before close."),
            ["platform_events"] = (
                "أحداث المنصة: سجل أحداث النظام والتكامل · للمراقبة التشغيلية.",
                "Platform events: system/integration event log for operations monitoring."),
        };

    public static bool TryGet(string navKey, out (string Ar, string En) guide) =>
        Templates.TryGetValue(navKey.Trim(), out guide!);

    public static int Count => Templates.Count;
}
