using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>سطرُ انحراف — مُخطَّطٌ وفعليٌّ لحسابٍ في فترة.</summary>
public record BudgetVarianceRow(
    string AccountCode,
    string AccountNameAr,
    AccountType AccountType,
    long? CostCenterId,
    string CostCenterNameAr,
    decimal Planned,
    decimal Actual)
{
    /// <summary>
    /// الانحراف — <b>موجبٌ يعني صالحاً للمنشأة</b> في الحالتين.
    ///
    /// <para>فالمصروفُ أقلَّ من مُخطَّطه <b>وفرٌ</b>، والإيرادُ أكثرَ من مُخطَّطه <b>زيادة</b>.
    /// <b>وإشارةٌ واحدةٌ لمعنيين متعاكسين تُقرأ خطأً حتماً</b> — فتُقلَب للإيراد هنا مرّةً
    /// واحدةً بدل أن يقلبها كلُّ قارئ.</para>
    /// </summary>
    public decimal Variance => AccountType == AccountType.Revenue
        ? Actual - Planned
        : Planned - Actual;

    /// <summary>نسبةُ الاستهلاك من المُخطَّط — و<c>null</c> إن لم يُخطَّط شيء.</summary>
    public decimal? ConsumedPercent => Planned == 0m
        ? null
        : Math.Round(Actual / Planned * 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>‏تجاوزٌ — للمصروف وحدَه، فالإيرادُ «يتجاوز» مدحاً لا ذمّاً.</summary>
    public bool IsOverspent => AccountType == AccountType.Expense && Actual > Planned;
}

/// <summary>
/// <b>المُخطَّطُ مقابلَ الفعليّ.</b>
///
/// <para><b>وثلاثُ حالاتٍ تُعرض كلُّها لا اثنتان:</b> ما خُطّط وأُنفق · وما خُطّط ولم
/// يُنفق · <b>وما أُنفق ولم يُخطَّط</b>. والثالثةُ أهمُّها وأكثرُ ما يُنسى: تقريرٌ يبدأ من
/// سطور الموازنة وحدها <b>يُخفي كلَّ مصروفٍ لم يُخطَّط له أصلاً</b> — وهو بالضبط ما
/// يبحث عنه مَن يقرأ موازنة.</para>
///
/// <para><b>والمقارنةُ على مفتاح الفترة نفسِه</b> (<c>YYYY-MM</c>) الذي يستعمله
/// <see cref="FiscalPeriod"/> — فلا يُترجَم زمنٌ إلى زمن.</para>
/// </summary>
public class BudgetVarianceQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<BudgetVarianceRow>> ExecuteAsync(
        long companyId, long budgetId, DateOnly fromDate, DateOnly toDate,
        CancellationToken ct = default)
    {
        var budget = await db.Budgets.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == budgetId && b.CompanyId == companyId, ct)
            ?? throw new AccountingException("الموازنة غير موجودة.");

        // ‏أشهرُ المدى بصيغة الفترة المالية — والمقارنةُ على المفتاح لا على التواريخ.
        var months = new HashSet<string>(StringComparer.Ordinal);
        for (var d = new DateOnly(fromDate.Year, fromDate.Month, 1);
             d <= toDate;
             d = d.AddMonths(1))
            months.Add(FiscalPeriod.CodeFor(d));

        var planned = budget.Lines
            .Where(l => months.Contains(l.PeriodCode))
            .GroupBy(l => (l.AccountCode, l.CostCenterId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));

        var actual = (await db.JournalEntries
                .WhereReportable(companyId, fromDate, toDate)
                .SelectMany(e => e.Lines)
                .GroupBy(l => new { l.AccountCode, l.CostCenterId })
                .Select(g => new
                {
                    g.Key.AccountCode,
                    g.Key.CostCenterId,
                    Debit = g.Sum(l => l.Debit),
                    Credit = g.Sum(l => l.Credit),
                })
                .ToListAsync(ct))
            .ToDictionary(x => (x.AccountCode, x.CostCenterId), x => (x.Debit, x.Credit));

        // ‏**اتّحادُ المفتاحين** — لا سطورُ الموازنة وحدها: ما أُنفق ولم يُخطَّط هو أوّلُ
        // ‏ما يُبحث عنه، وإخفاؤه يجعل التقريرَ يمدح موازنةً لم تُتجاوَز لأنّها لم تُكتب.
        var keys = planned.Keys.Concat(actual.Keys).Distinct().ToList();
        if (keys.Count == 0) return [];

        var codes = keys.Select(k => k.Item1).Distinct().ToList();
        var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.CompanyId == companyId && codes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, ct);

        var centerIds = keys.Where(k => k.Item2 is not null).Select(k => k.Item2!.Value).Distinct().ToList();
        var centers = await db.CostCenters.IgnoreQueryFilters().AsNoTracking()
            .Where(c => centerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = new List<BudgetVarianceRow>();
        foreach (var key in keys)
        {
            if (!accounts.TryGetValue(key.Item1, out var account)) continue;

            // ‏الفعليُّ بمنطق الحساب: المصروفُ مدينٌ موجب، والإيرادُ دائنٌ موجب.
            var (debit, credit) = actual.TryGetValue(key, out var a) ? a : (0m, 0m);
            var actualAmount = account.Type == AccountType.Revenue ? credit - debit : debit - credit;

            var center = key.Item2 is long id && centers.TryGetValue(id, out var c) ? c : null;

            rows.Add(new BudgetVarianceRow(
                account.Code, account.NameAr, account.Type,
                key.Item2, center?.NameAr ?? "غير مُصنَّف",
                planned.TryGetValue(key, out var p) ? p : 0m,
                actualAmount));
        }

        return rows
            .Where(r => r.AccountType is AccountType.Revenue or AccountType.Expense)
            .Where(r => r.Planned != 0m || r.Actual != 0m)
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .ThenBy(r => r.CostCenterNameAr, StringComparer.Ordinal)
            .ToList();
    }
}
