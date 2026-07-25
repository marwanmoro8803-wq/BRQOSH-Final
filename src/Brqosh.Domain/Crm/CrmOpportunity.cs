namespace Brqosh.Domain.Crm;

public enum CrmOpportunityStage
{
    Qualification,
    Proposal,
    Negotiation,
    Won,
    Lost,
}

/// <summary>فرصة بيع — G-10 CRM pipeline.</summary>
public class CrmOpportunity
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? CustomerId { get; private set; }
    public long? LeadId { get; private set; }
    public string Title { get; private set; }
    public CrmOpportunityStage Stage { get; private set; } = CrmOpportunityStage.Qualification;
    public decimal Amount { get; private set; }
    public DateOnly? ExpectedCloseDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CrmOpportunity() => Title = null!;

    private CrmOpportunity(long companyId, string title, decimal amount, long? customerId, long? leadId, DateOnly? expectedClose)
    {
        CompanyId = companyId;
        Title = title;
        Amount = amount;
        CustomerId = customerId;
        LeadId = leadId;
        ExpectedCloseDate = expectedClose;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CrmOpportunity Create(
        long companyId,
        string title,
        decimal amount,
        long? customerId = null,
        long? leadId = null,
        DateOnly? expectedCloseDate = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("عنوان الفرصة مطلوب.", nameof(title));
        if (amount < 0)
            throw new ArgumentException("قيمة الفرصة لا يمكن أن تكون سالبة.", nameof(amount));
        return new CrmOpportunity(companyId, title.Trim(), Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            customerId, leadId, expectedCloseDate);
    }

    public void AdvanceTo(CrmOpportunityStage stage) => Stage = stage;

    public void MarkWon() => Stage = CrmOpportunityStage.Won;

    public void MarkLost() => Stage = CrmOpportunityStage.Lost;
}
