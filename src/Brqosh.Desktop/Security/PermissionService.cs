using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Security;

namespace Brqosh.Desktop.Security;

/// <summary>صلاحيات الشاشات والإجراءات حسب دور المستخدم — مدير النظام يصل لكل شيء.</summary>
public static class PermissionService
{
    private static readonly Dictionary<string, UserRole[]> ScreenRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier, UserRole.Viewer],
        ["chart_of_accounts"] = [UserRole.Administrator, UserRole.Accountant],
        ["journal_entries"] = [UserRole.Administrator, UserRole.Accountant],
        ["cost_centers"] = [UserRole.Administrator, UserRole.Accountant],
        ["fiscal_periods"] = [UserRole.Administrator, UserRole.Accountant],
        ["fixed_assets"] = [UserRole.Administrator, UserRole.Accountant],
        ["payroll"] = [UserRole.Administrator, UserRole.Accountant],
        ["audit_log"] = [UserRole.Administrator],
        ["users"] = [UserRole.Administrator],
        ["projects"] = [UserRole.Administrator, UserRole.Accountant],
        ["contracts"] = [UserRole.Administrator, UserRole.Accountant],
        ["boq"] = [UserRole.Administrator, UserRole.Accountant],
        ["progress_billings"] = [UserRole.Administrator, UserRole.Accountant],
        ["subcontractors"] = [UserRole.Administrator, UserRole.Accountant],
        ["retentions"] = [UserRole.Administrator, UserRole.Accountant],
        ["bom"] = [UserRole.Administrator, UserRole.Accountant],
        ["work_orders"] = [UserRole.Administrator, UserRole.Accountant],
        ["wip"] = [UserRole.Administrator, UserRole.Accountant],
        ["production_cost_sheet"] = [UserRole.Administrator, UserRole.Accountant],
        ["pos_session"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["pos_shifts"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["pos_reports"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["customers"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["customer_statement"] = [UserRole.Administrator, UserRole.Accountant],
        ["sales_quotations"] = [UserRole.Administrator, UserRole.Accountant],
        ["sales_orders"] = [UserRole.Administrator, UserRole.Accountant],
        ["sales_invoices"] = [UserRole.Administrator, UserRole.Accountant],
        ["sales_returns"] = [UserRole.Administrator, UserRole.Accountant],
        ["crm_leads"] = [UserRole.Administrator, UserRole.Accountant],
        ["crm_opportunities"] = [UserRole.Administrator, UserRole.Accountant],
        ["crm_activities"] = [UserRole.Administrator, UserRole.Accountant],
        ["suppliers"] = [UserRole.Administrator, UserRole.Accountant],
        ["supplier_statement"] = [UserRole.Administrator, UserRole.Accountant],
        ["purchase_orders"] = [UserRole.Administrator, UserRole.Accountant],
        ["purchase_requests"] = [UserRole.Administrator, UserRole.Accountant],
        ["goods_receipts"] = [UserRole.Administrator, UserRole.Accountant],
        ["purchase_invoices"] = [UserRole.Administrator, UserRole.Accountant],
        ["debit_notes"] = [UserRole.Administrator, UserRole.Accountant],
        ["products"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["warehouses"] = [UserRole.Administrator, UserRole.Accountant],
        ["stock_movements"] = [UserRole.Administrator, UserRole.Accountant],
        ["stock_count"] = [UserRole.Administrator, UserRole.Accountant],
        ["landed_cost"] = [UserRole.Administrator, UserRole.Accountant],
        ["project_profitability"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["receipt_vouchers"] = [UserRole.Administrator, UserRole.Accountant],
        ["payment_vouchers"] = [UserRole.Administrator, UserRole.Accountant],
        ["bank_accounts"] = [UserRole.Administrator, UserRole.Accountant],
        ["bank_transfers"] = [UserRole.Administrator, UserRole.Accountant],
        ["exchange_rates"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["cheques"] = [UserRole.Administrator, UserRole.Accountant],
        ["bank_reconciliation"] = [UserRole.Administrator, UserRole.Accountant],
        ["zatca_invoices"] = [UserRole.Administrator, UserRole.Accountant],
        ["zatca_settings"] = [UserRole.Administrator],
        ["trial_balance"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["income_statement"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["balance_sheet"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["cash_flow_statement"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["consolidated_trial_balance"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["consolidated_income_statement"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["consolidated_balance_sheet"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["company_groups"] = [UserRole.Administrator, UserRole.Accountant],
        ["intercompany_elimination"] = [UserRole.Administrator, UserRole.Accountant],
        ["data_quality"] = [UserRole.Administrator, UserRole.Accountant],
        ["platform_events"] = [UserRole.Administrator],
        // ‏(أسئلةُ المالك 2026-10-24) ‏الدعمُ والتفضيلاتُ لكلّ دور: من يواجه المشكلةَ ليس المديرَ دائماً، والمظهرُ تفضيلُ كلِّ مستخدم.
        ["customer_support"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier, UserRole.Viewer],
        ["preferences"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier, UserRole.Viewer],
        ["pricing_plans"] = [UserRole.Administrator],
        ["vat_return"] = [UserRole.Administrator, UserRole.Accountant],
        ["general_ledger_report"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["company_settings"] = [UserRole.Administrator],
        // ‏(م3-٢) الاستعادةُ تمحو الدفاتر — للمدير وحده.
        ["backup"] = [UserRole.Administrator],
        ["organizations"] = [UserRole.Administrator],
        ["branches"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏(م-٤ط · ن-١٨) ‏**خمسةٌ وثلاثون مفتاحاً كانت غائبةً عن هذا الجدول.**
        //
        // ‏و`CanAccess` يسقط إلى `false` عند مفتاحٍ مجهول، و`BuildNavigation` يُسقط
        // ‏المجموعةَ كلَّها إن خلت (MainWindow.xaml.cs:557) — **بلا رسالة**. فلم تكن
        // ‏النتيجةُ إخفاءَ خمسٍ وثلاثين شاشةً بل **إطفاءَ أربعِ وحداتٍ كاملة**: العقارات
        // ‏٠/٣ · الرعاية الصحية ٠/٤ · الضيافة ٠/٤ · اللوجستيات ٠/٢ — لكلِّ من ليس مديراً.
        // ‏و`ModuleCatalog` يضع كلَّ واحدةٍ منها في المِلفِّ المُسمّى باسمها، **فمستشفًى
        // ‏يشتري بَرقوش ثمّ يُنشئ محاسباً لا يرى المرضى** — وهو سببُ شرائه.
        // ‏والرواتبُ هي الحالةُ العامّة: `ModuleKey.Payroll` في المِلفّات الثمانية كلِّها،
        // ‏والمجموعةُ تُعرَض ١/٧ للمحاسب — **فمن يُشغّل الرواتب لا يفتح سجلَّ الموظفين
        // ‏ولا قسائمَ الرواتب ولا ملفَّ حماية الأجور الذي يُلزم به النظام**.
        //
        // ‏**والقرارُ هنا صريحٌ لكلِّ مفتاح**، وكلُّ إسنادٍ إمّا نقلٌ عن جارٍ مقيسٍ في
        // ‏المجموعة نفسِها أو عن حارسِ الخدمة التي تقف خلفه. ‏و«للمدير وحدَه» يُكتب
        // ‏**قراراً** — فمن خارج `CanAccess` لا يُفرَّق المُقرَّرُ للمدير من المنسيّ.

        // ‏المحاسبة العامة — `cost_centers` و`fiscal_periods` مقيسان [مدير·محاسب]،
        // ‏و`project_profitability` مقيسٌ [مدير·محاسب·مُطّلع]: الربحيّةُ تُقرأ ولا تُكتَب.
        ["branch_profitability"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["budget_variance"] = [UserRole.Administrator, UserRole.Accountant],
        ["cost_center_statement"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏المبيعات والمشتريات — `customer_statement` و`supplier_statement` مقيسان
        // ‏[مدير·محاسب]، **وأعمارُ الديون كشفُ حسابٍ مُجمَّعٌ لا أكثر**.
        ["receivable_aging"] = [UserRole.Administrator, UserRole.Accountant],
        ["recurring_plans"] = [UserRole.Administrator, UserRole.Accountant],
        ["payable_aging"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏المخزون — `warehouses` و`stock_movements` و`stock_count` مقيسةٌ [مدير·محاسب].
        // ‏و`products` وحدَه يقبل الصرّاف لأنّه يُقرأ من شاشة البيع؛ **والدفعاتُ ووحداتُ
        // ‏القياس بياناتُ ضبطٍ لا بيعٍ**، فلا تُمنَح لمن لا يضبطها.
        ["stock_batches"] = [UserRole.Administrator, UserRole.Accountant],
        ["units_of_measure"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏الخزينة — `bank_accounts` و`cheques` و`bank_reconciliation` مقيسةٌ [مدير·محاسب]
        // ‏لأنّها تُرحّل؛ **والمركزُ النقديُّ والتدفّقُ المتوقّع يُقرآن**، فيلحقان
        // ‏بـ`exchange_rates` المقيسِ [مدير·محاسب·مُطّلع].
        ["fx_revaluation"] = [UserRole.Administrator, UserRole.Accountant],
        ["monetary_revaluation"] = [UserRole.Administrator, UserRole.Accountant],
        ["bank_statements"] = [UserRole.Administrator, UserRole.Accountant],
        ["cash_position"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["cash_flow_forecast"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],

        // ‏العقارات — لا جارَ في المجموعة، فالنقلُ عن النظير: `contracts` و`work_orders`
        // ‏مقيسان [مدير·محاسب]، وعقدُ الإيجار وأمرُ الصيانة نظيراهما.
        ["properties"] = [UserRole.Administrator, UserRole.Accountant],
        ["leases"] = [UserRole.Administrator, UserRole.Accountant],
        ["maintenance_orders"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏الرعاية الصحية — **وهنا فُرِّق بالقياس لا بالتشبيه**:
        // ‏• `visits` **نقلٌ حرفيٌّ** عن `VisitBillingService.cs:48` = `EnsureCanPost`،
        // ‏  وقائمتُها [مدير·محاسب·صرّاف]؛ وقِيس أنّ شبكةَ الزيارات **لا عمودَ تشخيصٍ
        // ‏  فيها** (VisitsPage.xaml:35-41) — فهي قائمةُ فوترةٍ لا سجلٌّ سريريّ.
        // ‏  ومنعُ الصرّافِ منها يجعل تخويلَ الخدمةِ له **غيرَ قابلٍ للبلوغ من الواجهة**.
        // ‏• `patients` **شُدِّد عمداً**: الشبكةُ تعرض رقمَ الهوية (PatientsPage.xaml:43)
        // ‏  ومعه تاريخُ الميلاد، **وحمايةُ البيانات الشخصيّة (G-30) لم تبدأ بعد**
        // ‏  (MARKET_LAUNCH_GAP_ANALYSIS.md:88 · ❌/❌ · P1). فالتوسيعُ قرارُ خصوصيّةٍ
        // ‏  يملكه المُشغّل، **والتضييقُ يُرجَع عنه بسطرٍ ولا يلزمه حكم**.
        ["patients"] = [UserRole.Administrator, UserRole.Accountant],
        ["visits"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["insurance_payers"] = [UserRole.Administrator, UserRole.Accountant],
        ["insurance_claims"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏الضيافة — `reservations` نقلٌ عن `FolioService` (أربعةُ حرّاسِ `EnsureCanPost`
        // ‏تقبل الصرّاف): الاستقبالُ يُسجّل الدخولَ والخروج. **و`tip_pools` نقلٌ عن
        // ‏`TipPoolService.cs:52,114` = `EnsureCanPostAccounting`** فلا صرّاف.
        // ‏و`menu_items` وصفاتٌ كـ`bom`، و`rooms` بياناتُ ضبطٍ كـ`warehouses`.
        ["rooms"] = [UserRole.Administrator, UserRole.Accountant],
        ["reservations"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier],
        ["menu_items"] = [UserRole.Administrator, UserRole.Accountant],
        ["tip_pools"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏اللوجستيات — الرحلةُ والبوليصةُ تُرحّلان (DeliveryTripService · CodSettlementService).
        ["delivery_trips"] = [UserRole.Administrator, UserRole.Accountant],
        ["shipments"] = [UserRole.Administrator, UserRole.Accountant],

        // ‏الرواتب — `payroll` مقيسٌ [مدير·محاسب]، **ومن يُشغّل الدورةَ يفتح ما تقوم عليه**.
        ["employees"] = [UserRole.Administrator, UserRole.Accountant],
        ["payslips"] = [UserRole.Administrator, UserRole.Accountant],
        ["employee_advances"] = [UserRole.Administrator, UserRole.Accountant],
        ["leave_requests"] = [UserRole.Administrator, UserRole.Accountant],
        ["wage_protection"] = [UserRole.Administrator, UserRole.Accountant],
        // ‏نسبُ التأمينات إعدادٌ نظاميٌّ يُحرّك كلَّ دورة — كـ`zatca_settings` المقيسِ [مدير].
        ["gosi_policy"] = [UserRole.Administrator],

        // ‏الأمان والتدقيق — **ثلاثةٌ للمدير وحدَه بقرارٍ مكتوبٍ لا بإغفال**:
        // ‏`soft_deleted_rows` نقلٌ عن `SoftDeletedRowService.cs:71` = `EnsureAdministrator`
        // ‏(وجارُه `backup` مقيسٌ [مدير] بسببٍ مكتوب)، و`data_migration` يستورد بياناتٍ
        // ‏رئيسةً بالجملة كـ`organizations`، و`diagnostics` حالةُ منصّةٍ كـ`platform_events`.
        ["soft_deleted_rows"] = [UserRole.Administrator],
        ["data_migration"] = [UserRole.Administrator],
        ["diagnostics"] = [UserRole.Administrator],
    };

    /// <summary>
    /// ‏(ن-١٨) <b>المفاتيحُ المُقرَّرة</b> — سطحٌ يُميّز «للمدير وحدَه» من «منسيّ».
    ///
    /// <para><b>ولا يُستغنى عنه بحارسٍ سلوكيّ</b>: تسعةُ مداخلَ مشروعةٍ قيمتُها
    /// <c>[Administrator]</c>، فـ<see cref="CanAccess"/> يُعيد <c>false</c> لكلّ دورٍ
    /// آخرَ فيها — <b>تماماً كما يفعل بمفتاحٍ لا وجودَ له</b>. فالمُعلَنُ والمنسيُّ
    /// لا يُفرَّقان من الخارج، ولا اختبارَ سلوكيٌّ يبلغ الفرق.</para>
    /// </summary>
    public static IReadOnlyCollection<string> DecidedKeys => ScreenRoles.Keys;

    public static bool CanAccess(AppUser? user, string pageKey)
    {
        if (user is null) return false;
        if (user.Role == UserRole.Administrator) return true;
        if (!ScreenRoles.TryGetValue(pageKey, out var allowed))
            return false;
        return allowed.Contains(user.Role);
    }

    /// <summary>ترحيل مستندات تشغيلية (فواتير، مخزون، POS…) — Viewer ممنوع.</summary>
    public static bool CanPost(AppUser? user) =>
        user is not null && user.Role is not UserRole.Viewer;

    /// <summary>عمليات محاسبية متقدمة — مدير أو محاسب.</summary>
    public static bool CanPostAccounting(AppUser? user) =>
        user is not null && user.Role is UserRole.Administrator or UserRole.Accountant;

    /// <summary>إلغاء / عكس مستند مرحّل.</summary>
    public static bool CanCancelOrReverse(AppUser? user) =>
        user is not null && user.Role is UserRole.Administrator or UserRole.Accountant;

    public static bool CanManageUsers(AppUser? user) =>
        user?.Role == UserRole.Administrator;

    public static void ApplyActionVisibility(AppUser? user, params (FrameworkElement Element, Func<AppUser?, bool> Predicate)[] rules)
    {
        foreach (var (element, predicate) in rules)
            element.Visibility = predicate(user) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string DeniedMessage(string pageTitle) =>
        $"لا تملك صلاحية الوصول إلى «{pageTitle}».";

    /// <summary>
    /// ‏(ن-١٨) <b>منعُ إجراءٍ لا منعُ وصول</b> — و<see cref="DeniedMessage"/> يقول
    /// «الوصول إلى»، فاستعمالُه لزرٍّ يُنتج «لا تملك صلاحية الوصول إلى إعادة التفعيل».
    ///
    /// <para>‏و<c>LocaleManager.T</c> لا <c>$"…"</c> وحدَها: النصُّ المركَّب لا يجد
    /// نفسَه في السجلّ، <b>فيبقى عربيّاً وحيداً في واجهةٍ إنجليزية</b>.</para>
    /// </summary>
    public static string DeniedActionMessage(string actionAr, string actionEn) =>
        LocaleManager.T($"لا تملك صلاحية {actionAr}.", $"You do not have permission to {actionEn}.");
}
