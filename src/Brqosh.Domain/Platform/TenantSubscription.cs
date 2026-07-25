using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Platform;

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    Suspended = 2,
}

public class TenantSubscription
{
    public long Id { get; private set; }
    public Guid TenantId { get; private set; }
    public long BillingPlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }

    private TenantSubscription() { }

    public static TenantSubscription Create(Guid tenantId, long billingPlanId, SubscriptionStatus status = SubscriptionStatus.Trial)
    {
        if (tenantId == Guid.Empty)
            throw new AccountingException("معرّف المستأجر مطلوب.");
        if (billingPlanId <= 0)
            throw new AccountingException("خطة الفوترة مطلوبة.");

        return new TenantSubscription
        {
            TenantId = tenantId,
            BillingPlanId = billingPlanId,
            Status = status,
            StartedAtUtc = DateTime.UtcNow,
        };
    }
}
