using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Security;

/// <summary>تسجيل تدقيق موحّد عند ترحيل المستندات المالية.</summary>
public static class AuditPostingHelper
{
    public static Task LogPostAsync(
        BrqoshDbContext db,
        long companyId,
        string entityType,
        string entityKey,
        string? reason = null,
        string details = "",
        CancellationToken ct = default)
    {
        var username = SecurityContext.ResolveUsername();
        var mergedDetails = string.IsNullOrWhiteSpace(details)
            ? $"ترحيل {entityType}"
            : details;
        return new AuditService(db).LogAsync(
            companyId, username, "ترحيل", entityType, entityKey, mergedDetails, reason, ct);
    }
}
