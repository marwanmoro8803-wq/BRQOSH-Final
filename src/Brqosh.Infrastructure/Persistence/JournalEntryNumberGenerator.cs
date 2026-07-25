using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// توليد الرقم التسلسلي للقيد: JV-{السنة}-{تسلسل من 6 خانات} — تسلسل مستقل لكل شركة وسنة.
/// يستخدم قفل PostgreSQL transaction عند التوفر لتجنب التكرار (ACC-006).
/// </summary>
public class JournalEntryNumberGenerator(BrqoshDbContext db)
{
    public async Task<string> NextAsync(long companyId, DateOnly entryDate, CancellationToken ct = default)
    {
        await AcquireSequenceLockAsync(companyId, entryDate.Year, ct);

        var year = entryDate.Year;
        var prefix = $"JV-{year}-";
        var fromDb = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.EntryNumber != null && e.EntryNumber.StartsWith(prefix))
            .Select(e => e.EntryNumber)
            .ToListAsync(ct);

        var pending = db.JournalEntries.Local
            .Where(e => e.CompanyId == companyId && e.EntryNumber != null && e.EntryNumber.StartsWith(prefix))
            .Select(e => e.EntryNumber!);

        var lastNumber = fromDb.Concat(pending).OrderByDescending(n => n).FirstOrDefault();

        var next = 1;
        if (lastNumber is not null && int.TryParse(lastNumber[prefix.Length..], out var lastSeq))
            next = lastSeq + 1;

        return $"{prefix}{next:D6}";
    }

    private async Task AcquireSequenceLockAsync(long companyId, int year, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
            return;

        // قفل على مستوى المعاملة — يمنع race عند ترحيل متزامن
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            companyId, year,
            ct);
    }
}
