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

        // قفل على مستوى المعاملة — يمنع race عند ترحيل متزامن.
        //
        // (CEEF-203) حرج: يجب تمرير المعاملات كمصفوفة صريحة. التحميل الزائد
        // ExecuteSqlRawAsync(string, params object[]) كان يبتلع الـ CancellationToken
        // كمعامل SQL ثالث، فيرمي EF: "no store type mapping for CancellationToken" —
        // أي أن **كل ترحيل محاسبي على PostgreSQL كان يفشل**. لم تكشفه الاختبارات لأنها
        // تعمل على SQLite حيث يعود هذا التابع مبكراً (السطر أعلاه).
        // التوقيع الموجود في PostgreSQL هو pg_advisory_xact_lock(int4, int4) —
        // تمرير companyId كـ bigint كان يرمي 42883: function does not exist.
        // أي أن حماية ACC-006 لم تكن تعمل على PostgreSQL إطلاقاً. الصبّ صريح في SQL.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, year },
            ct);
    }
}
