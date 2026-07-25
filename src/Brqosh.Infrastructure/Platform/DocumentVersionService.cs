using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Platform;

public class DocumentVersionService(BrqoshDbContext db)
{
    public async Task RecordPostAsync(
        long companyId, string documentType, string documentKey, CancellationToken ct = default)
    {
        var next = await db.DocumentVersionHistories
            .Where(v => v.CompanyId == companyId && v.DocumentType == documentType && v.DocumentKey == documentKey)
            .Select(v => (int?)v.Version)
            .MaxAsync(ct) ?? 0;

        db.DocumentVersionHistories.Add(DocumentVersionHistory.RecordPost(
            companyId, documentType, documentKey, next + 1, SecurityContext.ResolveUsername()));
        await db.SaveChangesAsync(ct);
    }
}
