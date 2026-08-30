using System.Windows;
using System.Windows.Controls;
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
        ["customer_support"] = [UserRole.Administrator],
        ["pricing_plans"] = [UserRole.Administrator],
        ["vat_return"] = [UserRole.Administrator, UserRole.Accountant],
        ["general_ledger_report"] = [UserRole.Administrator, UserRole.Accountant, UserRole.Viewer],
        ["company_settings"] = [UserRole.Administrator],
        // ‏(م3-٢) الاستعادةُ تمحو الدفاتر — للمدير وحده.
        ["backup"] = [UserRole.Administrator],
        ["organizations"] = [UserRole.Administrator],
        ["branches"] = [UserRole.Administrator, UserRole.Accountant],
    };

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
}
