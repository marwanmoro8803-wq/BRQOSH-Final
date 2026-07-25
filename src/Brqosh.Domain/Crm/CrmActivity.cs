namespace Brqosh.Domain.Crm;

public enum CrmActivityType
{
    Call,
    Meeting,
    Note,
    Email,
}

/// <summary>نشاط CRM (مكالمة/اجتماع/ملاحظة) — G-10.</summary>
public class CrmActivity
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? CustomerId { get; private set; }
    public long? OpportunityId { get; private set; }
    public CrmActivityType ActivityType { get; private set; }
    public string Subject { get; private set; }
    public string Notes { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CrmActivity()
    {
        Subject = null!;
        Notes = null!;
    }

    private CrmActivity(
        long companyId,
        CrmActivityType type,
        string subject,
        string notes,
        long? customerId,
        long? opportunityId,
        DateTime? dueAtUtc)
    {
        CompanyId = companyId;
        ActivityType = type;
        Subject = subject;
        Notes = notes;
        CustomerId = customerId;
        OpportunityId = opportunityId;
        DueAtUtc = dueAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CrmActivity Log(
        long companyId,
        CrmActivityType type,
        string subject,
        string notes = "",
        long? customerId = null,
        long? opportunityId = null,
        DateTime? dueAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("موضوع النشاط مطلوب.", nameof(subject));
        return new CrmActivity(companyId, type, subject.Trim(), (notes ?? "").Trim(),
            customerId, opportunityId, dueAtUtc);
    }
}
