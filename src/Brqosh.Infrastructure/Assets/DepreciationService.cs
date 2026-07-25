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
                var alreadyRan = await db.JournalEntries.AnyAsync(e =>
                    e.CompanyId == companyId
                    && e.SourceDocument == sourceDoc
                    && e.EntryDate.Year == runDate.Year
                    && e.EntryDate.Month == runDate.Month
                    && e.Status != JournalEntryStatus.Draft, token);
                if (alreadyRan)
                    continue;

                var amount = asset.ApplyDepreciation();
                if (amount <= 0) continue;

                var accum = await db.Accounts.SingleAsync(a => a.Id == asset.GlDepreciationAccountId, token);
                var entry = JournalEntry.CreateDraft(companyId, runDate,
                    $"إهلاك {asset.NameAr}", $"Depreciation:{asset.Code}");
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
