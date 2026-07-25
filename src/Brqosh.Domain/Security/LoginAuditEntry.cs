namespace Brqosh.Domain.Security;

/// <summary>سجل محاولات الدخول — LIFECYCLE-2–4.</summary>
public class LoginAuditEntry
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Username { get; private set; }
    public bool Success { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LoginAuditEntry()
    {
        Username = null!;
    }

    public static LoginAuditEntry Record(long companyId, string username, bool success, string? failureReason = null) =>
        new()
        {
            CompanyId = companyId,
            Username = username.Trim().ToLowerInvariant(),
            Success = success,
            FailureReason = failureReason,
            CreatedAtUtc = DateTime.UtcNow,
        };
}
