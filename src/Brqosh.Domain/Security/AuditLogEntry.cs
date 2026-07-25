namespace Brqosh.Domain.Security;

/// <summary>سجل تدقيق — يُسجَّل عند كل عملية ترحيل أو تعديل حرج.</summary>
public class AuditLogEntry
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Username { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string EntityKey { get; private set; }
    public string Details { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private AuditLogEntry()
    {
        Username = null!;
        Action = null!;
        EntityType = null!;
        EntityKey = null!;
        Details = null!;
    }

    public static AuditLogEntry Record(long companyId, string username, string action,
        string entityType, string entityKey, string details = "", string? reason = null)
    {
        return new AuditLogEntry
        {
            CompanyId = companyId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityKey = entityKey,
            Details = details,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAtUtc = DateTime.UtcNow,
        };
    }
}
