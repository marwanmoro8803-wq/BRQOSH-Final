using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// بوابة الترحيل الموحدة: كل قيد يمر من هنا — يتحقق من الفترة المالية قبل الترحيل،
/// وينشئ الفترة مفتوحةً تلقائياً إن لم توجد (أول استخدام للشهر).
/// </summary>
public class JournalPostingService(BrqoshDbContext db)
{
    public async Task<JournalEntry> PostAsync(JournalEntry entry)
    {
        await EnsurePeriodOpenAsync(entry.CompanyId, entry.EntryDate);

        var number = await new JournalEntryNumberGenerator(db).NextAsync(entry.CompanyId, entry.EntryDate);
        entry.Post(number);

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task EnsurePeriodOpenAsync(long companyId, DateOnly date)
    {
        var code = FiscalPeriod.CodeFor(date);
        var period = await db.FiscalPeriods
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == code);

        if (period is null)
        {
            period = FiscalPeriod.Create(companyId, date.Year, date.Month);
            db.FiscalPeriods.Add(period);
            await db.SaveChangesAsync();
            return;
        }

        if (period.Status == FiscalPeriodStatus.Closed)
            throw new AccountingException(
                $"الفترة المالية {code} مقفلة — لا يمكن الترحيل فيها. أعد فتح الفترة أولاً إن كنت مخوّلاً.");
    }
}
