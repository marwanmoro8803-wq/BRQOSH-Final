namespace Brqosh.Domain.Crm;

public enum CrmLeadStatus
{
    New,
    Qualified,
    Converted,
    Disqualified,
}

/// <summary>عميل محتمل قبل التحويل إلى Customer — G-10 CRM.</summary>
public class CrmLead : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Name { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; }
    public string Source { get; private set; }
    public CrmLeadStatus Status { get; private set; } = CrmLeadStatus.New;
    public long? CustomerId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CrmLead()
    {
        Name = null!;
        Phone = null!;
        Email = null!;
        Source = null!;
    }

    private CrmLead(long companyId, string name, string phone, string email, string source)
    {
        CompanyId = companyId;
        Name = name;
        Phone = phone;
        Email = email;
        Source = source;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CrmLead Create(long companyId, string name, string phone = "", string email = "", string source = "مباشر")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم العميل المحتمل مطلوب.", nameof(name));
        return new CrmLead(companyId, name.Trim(), (phone ?? "").Trim(), (email ?? "").Trim(), (source ?? "مباشر").Trim());
    }

    public void MarkQualified()
    {
        if (Status is CrmLeadStatus.Converted or CrmLeadStatus.Disqualified)
            throw new InvalidOperationException("لا يمكن تأهيل عميل محتمل مُحوَّل أو مرفوض.");
        Status = CrmLeadStatus.Qualified;
    }

    public void MarkConverted(long customerId)
    {
        if (customerId <= 0)
            throw new ArgumentException("معرّف العميل مطلوب.", nameof(customerId));
        Status = CrmLeadStatus.Converted;
        CustomerId = customerId;
    }

    public void Disqualify()
    {
        if (Status == CrmLeadStatus.Converted)
            throw new InvalidOperationException("لا يمكن رفض عميل محتمل مُحوَّل.");
        Status = CrmLeadStatus.Disqualified;
    }

    public void Deactivate()
    {
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }
}
