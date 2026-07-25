using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

public class AuditService(BrqoshDbContext db)
{
    public async Task LogAsync(long companyId, string username, string action,
        string entityType, string entityKey, string details = "", string? reason = null,
        CancellationToken ct = default)
    {
        var entry = AuditLogEntry.Record(
            companyId, username, action, entityType, entityKey, details, reason);

        // (O-19، CEEF-212) اربط الإدخال بسلسلة الشركة قبل الحفظ.
        // القفل الاستشاري يمنع تفرّع السلسلة عند كتابتين متزامنتين لنفس الشركة
        // (كلتاهما تقرأ نفس «الأخير» فتُنتجان نفس الترتيب ونفس التجزئة السابقة).
        await AcquireChainLockAsync(companyId, ct);

        var last = await db.AuditLogEntries
            .Where(a => a.CompanyId == companyId)
            .OrderByDescending(a => a.Sequence)
            .Select(a => new { a.Sequence, a.EntryHash })
            .FirstOrDefaultAsync(ct);

        entry.ChainTo(
            (last?.Sequence ?? 0) + 1,
            last?.EntryHash ?? AuditLogEntry.GenesisHash);

        db.AuditLogEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    private async Task AcquireChainLockAsync(long companyId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
            return;

        // مفتاح ثانٍ ثابت يميّز قفل سلسلة التدقيق عن قفل ترقيم القيود لنفس الشركة.
        // التوقيع (int4, int4) وتمرير المعاملات كمصفوفة صريحة — درسا CEEF-203.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, 991 },
            ct);
    }
}
