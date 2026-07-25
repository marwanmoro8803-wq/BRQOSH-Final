using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Security;

public class AuditService(BrqoshDbContext db)
{
    public async Task LogAsync(long companyId, string username, string action,
        string entityType, string entityKey, string details = "", string? reason = null,
        CancellationToken ct = default)
    {
        db.AuditLogEntries.Add(AuditLogEntry.Record(
            companyId, username, action, entityType, entityKey, details, reason));
        await db.SaveChangesAsync(ct);
    }
}
