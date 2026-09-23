using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

public class DepreciationService(BrqoshDbContext db)
{
    public async Task<int> RunMonthlyAsync(long companyId, DateOnly runDate, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var assets = (await db.FixedAssets
                .Where(a => a.CompanyId == companyId && a.IsActive)
                .ToListAsync(token))
                .Where(a => a.BookValue > a.SalvageValue)
                .ToList();

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, runDate);

            var depExpense = await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "5211", token);
            int count = 0;

            foreach (var asset in assets)
            {
                var sourceDoc = $"Depreciation:{asset.Code}";

                // ‏(جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11) ‏**آخرُ شهرٍ أُهلك — ثمّ كلُّ ما استحقّ بعده.**
                // ‏وكان السؤالُ «هل أُهلك هذا الشهرُ بعينه؟» فيُحمَّل شهرٌ واحد: أصلٌ لم يُقتنَ بعدُ يُهلَك، وشهرٌ فات
                // ‏لا يُستدرَك، وشهرٌ سابقٌ بعد لاحقه يُحمَّل ثانيةً. **والقائمُ من `WhereStanding`** (ح-٤٧) لا
                // ‏`Status != Draft` — فالمعكوسُ ليس إهلاكاً قائماً.
                var lastDepreciated = await db.JournalEntries
                    .Where(e => e.CompanyId == companyId && e.SourceDocument == sourceDoc)
                    .WhereStanding(db.JournalEntries)
                    .OrderByDescending(e => e.EntryDate)
                    .Select(e => (DateOnly?)e.EntryDate)
                    .FirstOrDefaultAsync(token);

                var months = asset.MonthsDue(lastDepreciated, runDate);
                var amount = asset.ApplyDepreciation(months);
                if (amount <= 0) continue;

                // ‏**والاستدراكُ قيدٌ واحدٌ بتاريخ التشغيل** — أوّلُ فترةٍ مفتوحة، مذهبُ SAP وOracle؛ والفتراتُ التي
                // ‏فاتت قد تكون أُقفلت، فلا يُكتب فيها.
                var accum = await db.Accounts.SingleAsync(a => a.Id == asset.GlDepreciationAccountId, token);
                var entry = JournalEntry.CreateDraft(companyId, runDate,
                    months == 1 ? $"إهلاك {asset.NameAr}" : $"إهلاك {asset.NameAr} — {months} أشهر (استدراك)",
                    sourceDoc);
                entry.AddLine(depExpense, amount, 0m, asset.NameAr);
                entry.AddLine(accum, 0m, amount, "مجمع إهلاك");
                var num = await new JournalEntryNumberGenerator(db).NextAsync(companyId, runDate);
                entry.Post(num);
                db.JournalEntries.Add(entry);
                count++;
            }

            if (count > 0)
            {
                await db.SaveChangesAsync(token);
                await new AuditService(db).LogAsync(companyId, username, "إهلاك شهري",
                    "FixedAsset", runDate.ToString("yyyy-MM"), $"{count} أصل");
            }

            return count;
        }, ct);
    }
}
