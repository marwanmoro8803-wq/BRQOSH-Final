using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// توليد الرقم التسلسلي للقيد: JV-{السنة}-{تسلسل من 6 خانات} — تسلسل مستقل لكل شركة وسنة.
/// </summary>
public class JournalEntryNumberGenerator(BrqoshDbContext db)
{
    public async Task<string> NextAsync(long companyId, DateOnly entryDate)
    {
        var year = entryDate.Year;
        var prefix = $"JV-{year}-";
        var lastNumber = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.EntryNumber != null && e.EntryNumber.StartsWith(prefix))
            .OrderByDescending(e => e.EntryNumber)
            .Select(e => e.EntryNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (lastNumber is not null && int.TryParse(lastNumber[prefix.Length..], out var lastSeq))
            next = lastSeq + 1;

        return $"{prefix}{next:D6}";
    }
}
