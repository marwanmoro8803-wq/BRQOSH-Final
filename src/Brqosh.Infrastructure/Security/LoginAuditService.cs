using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Security;

public class LoginAuditService(BrqoshDbContext db)
{
    public async Task LogAsync(long companyId, string username, bool success, string? failureReason = null,
        CancellationToken ct = default)
    {
        db.LoginAuditEntries.Add(LoginAuditEntry.Record(companyId, username, success, failureReason));
        await db.SaveChangesAsync(ct);
    }
}
