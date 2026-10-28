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

    /// <summary>‏(Phase 7) الاسمُ القديمُ في القالب لكلّ حسابٍ صُحِّح اسمُه — لا يُعاد تسميةُ ما غيّره المالك.</summary>
    private static readonly Dictionary<string, string> PreviousTemplateNameAr = new(StringComparer.Ordinal)
    {
        ["1106"] = "مخصص الديون المشكوك في تحصيلها",
        ["1112"] = "عهد وسلف الموظفين",
        ["1113"] = "أعمال تحت التنفيذ (مستخلصات غير مفوترة)",
        ["5102"] = "مشتريات",
        ["5103"] = "مردودات ومسموحات المشتريات",
        ["5214"] = "خصم مكتسب",
    };

    /// <summary>
    /// ‏<b>تصحيحُ مسمّيات الدليل في القواعد القائمة</b>: يُعاد تسميةُ ما بقي على اسم القالب القديم، ودورُ الخصم المكتسب
    /// الباقي على 5214 (تحت 52) يُحوَّل إلى 5109 (تحت 51) — وما رُحِّل على 5214 باقٍ برصيده —، ويُوقَف 5103 إن لم تمسّه حركة.
    /// يُعاد بلا ضرر.
    /// </summary>
    public static async Task<int> ApplyNamingCorrectionsAsync(BrqoshDbContext db, CancellationToken ct = default)
    {
        var template = SaudiChartOfAccounts.Template.ToDictionary(t => t.Code, StringComparer.Ordinal);
        var codes = PreviousTemplateNameAr.Keys.ToList();
        var changed = 0;

        foreach (var companyId in await db.Companies.Select(c => c.Id).ToListAsync(ct))
        {
            var accounts = await db.Accounts
                .Where(a => a.CompanyId == companyId && codes.Contains(a.Code))
                .ToDictionaryAsync(a => a.Code, ct);

            foreach (var (code, oldName) in PreviousTemplateNameAr)
                if (accounts.TryGetValue(code, out var account) && account.NameAr == oldName)
                {
                    account.Rename(template[code].NameAr, template[code].NameEn);
                    changed++;
                }

            var discountRole = await db.PostingRoleAccounts.SingleOrDefaultAsync(r => r.CompanyId == companyId
                && r.Role == nameof(SaudiChartOfAccounts.PostingRoleCodes.PurchaseDiscountsEarned), ct);
            if (discountRole?.AccountCode == "5214"
                && await db.Accounts.AnyAsync(x => x.CompanyId == companyId
                    && x.Code == SaudiChartOfAccounts.PostingRoleCodes.PurchaseDiscountsEarned, ct))
            {
                discountRole.Assign(SaudiChartOfAccounts.PostingRoleCodes.PurchaseDiscountsEarned);
                changed++;
            }

            if (accounts.TryGetValue("5103", out var returns) && returns.IsActive && !returns.IsSystem
                && !await db.JournalEntries.Where(e => e.CompanyId == companyId)
                    .SelectMany(e => e.Lines).AnyAsync(l => l.AccountCode == "5103", ct))
            {
                returns.Deactivate();
                changed++;
            }
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
        return changed;
    }
}
