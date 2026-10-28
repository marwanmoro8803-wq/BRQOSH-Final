using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// ‏<b>ترقيةُ دليل الحسابات للمنشآت القائمة</b>: ما أُضيف للقالب بعد إنشائها (قائمةٌ صريحة لا القالبُ كلُّه، فحسابٌ حذفه
/// المالكُ لا يعود) يُنشأ تحت أبيه إن غاب. يُعاد بلا ضرر، ولا يمسّ حساباً قائماً ولا قيداً.
/// </summary>
public static class ChartTemplateSync
{
    public static async Task<int> AddMissingAsync(BrqoshDbContext db, CancellationToken ct = default)
    {
        var wanted = SaudiChartOfAccounts.Template
            .Where(t => SaudiChartOfAccounts.AddedForExistingCompanies.Contains(t.Code))
            .ToList();
        var codes = wanted.Select(t => t.Code).Concat(wanted.Select(t => t.ParentCode!)).Distinct().ToList();
        var added = 0;

        foreach (var companyId in await db.Companies.Select(c => c.Id).ToListAsync(ct))
        {
            var existing = await db.Accounts
                .Where(a => a.CompanyId == companyId && codes.Contains(a.Code))
                .Select(a => new { a.Code, a.Id })
                .ToDictionaryAsync(a => a.Code, a => a.Id, ct);
            foreach (var seed in wanted)
            {
                if (existing.ContainsKey(seed.Code) || seed.ParentCode is null
                    || !existing.TryGetValue(seed.ParentCode, out var parentId))
                    continue;
                db.Accounts.Add(Account.Create(seed.Code, seed.NameAr, seed.NameEn, seed.Type,
                    parentId, seed.IsGroup, seed.IsSystem, companyId));
                added++;
            }
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);
        return added;
    }
}
