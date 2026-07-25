namespace Brqosh.Infrastructure.Companies;

/// <summary>سياق المستأجر — يُضبط من middleware/API عبر AsyncLocal (مثل SecurityContext).</summary>
public static class TenantContext
{
    private static readonly AsyncLocal<Guid?> CurrentTenantIdSlot = new();

    /// <summary>عند BRQOSH_SAAS_MODE=1 — رفض طلبات بدون X-Tenant-Id على مسارات الشركة.</summary>
    public static bool EnforceTenantIsolation { get; set; }

    public static Guid? CurrentTenantId
    {
        get => CurrentTenantIdSlot.Value;
        set => CurrentTenantIdSlot.Value = value;
    }
}
