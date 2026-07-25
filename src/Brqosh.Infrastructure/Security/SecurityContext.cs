using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;

namespace Brqosh.Infrastructure.Security;

/// <summary>سياق المستخدم الحالي للتحقق من الصلاحيات على مستوى الخدمات — fail-closed.</summary>
public static class SecurityContext
{
    private static readonly AsyncLocal<UserRole?> CurrentRoleSlot = new();
    private static readonly AsyncLocal<string?> CurrentUsernameSlot = new();

    /// <summary>
    /// يُفعَّل فقط في الاختبارات الآلية عبر BRQOSH_TEST=1 — لا يُستخدم في الإنتاج.
    /// CEEF-125: مُعطَّل قسرياً في وضع الإنتاج حتى لو تسرّب المتغير لبيئة العميل.
    /// </summary>
    private static bool AutomatedTestBypass =>
        !Brqosh.Application.Setup.AppEnvironmentConfig.IsProduction &&
        string.Equals(Environment.GetEnvironmentVariable("BRQOSH_TEST"), "1", StringComparison.Ordinal);

    public static UserRole? CurrentRole
    {
        get => CurrentRoleSlot.Value;
        set => CurrentRoleSlot.Value = value;
    }

    public static string? CurrentUsername
    {
        get => CurrentUsernameSlot.Value;
        set => CurrentUsernameSlot.Value = value;
    }

    public static string ResolveUsername(string? passedUsername = null)
    {
        if (!string.IsNullOrWhiteSpace(CurrentUsername))
            return CurrentUsername.Trim();
        if (!string.IsNullOrWhiteSpace(passedUsername))
            return passedUsername.Trim();
        return "system";
    }

    public static void EnsureCanPost()
    {
        if (CurrentRole is null)
        {
            if (AutomatedTestBypass) return;
            throw new AccountingException("لا يوجد مستخدم مسجّل — الترحيل مرفوض.");
        }
        if (CurrentRole is UserRole.Viewer)
            throw new AccountingException("لا تملك صلاحية الترحيل أو الإلغاء.");
    }

    public static void EnsureCanPostAccounting()
    {
        if (CurrentRole is null)
        {
            if (AutomatedTestBypass) return;
            throw new AccountingException("لا يوجد مستخدم مسجّل — العملية المحاسبية مرفوضة.");
        }
        if (CurrentRole is UserRole.Viewer or UserRole.Cashier)
            throw new AccountingException("لا تملك صلاحية العمليات المحاسبية المتقدمة.");
    }

    public static void EnsureCanCancelOrReverse()
    {
        if (CurrentRole is null)
        {
            if (AutomatedTestBypass) return;
            throw new AccountingException("لا يوجد مستخدم مسجّل — الإلغاء أو العكس مرفوض.");
        }
        if (CurrentRole is UserRole.Viewer or UserRole.Cashier)
            throw new AccountingException("لا تملك صلاحية الإلغاء أو العكس.");
    }

    public static void EnsureAdministrator()
    {
        if (CurrentRole is null)
        {
            if (AutomatedTestBypass) return;
            throw new AccountingException("لا يوجد مستخدم مسجّل — العملية الإدارية مرفوضة.");
        }
        if (CurrentRole is not UserRole.Administrator)
            throw new AccountingException("هذه العملية تتطلب صلاحية مدير النظام.");
    }
}
