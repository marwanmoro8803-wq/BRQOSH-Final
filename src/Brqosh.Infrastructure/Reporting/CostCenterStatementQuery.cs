using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>سطرُ مركزِ تكلفةٍ في التقرير — حسابٌ واحدٌ داخل مركزٍ واحد.</summary>
public record CostCenterStatementRow(
    long? CostCenterId,
    string CostCenterCode,
    string CostCenterNameAr,
    string AccountCode,
    string AccountNameAr,
    AccountType AccountType,
    decimal Debit,
    decimal Credit)
{
    /// <summary>‏الصافي بمنطق الحساب: المصروفُ مدينٌ موجب، والإيرادُ دائنٌ موجب.</summary>
    public decimal Net => AccountType == AccountType.Revenue ? Credit - Debit : Debit - Credit;
}

/// <summary>
/// (CEEF-230 · ط1-٧) <b>الفعليُّ بمركز التكلفة — وهو التقريرُ الذي لم يكن.</b>
///
/// <para><b>ما كان:</b> شاشةٌ كاملةٌ لإدخال مراكز التكلفة، وحقلٌ لها على سطر القيد،
/// <b>ولا تقريرَ واحدٍ في النظام يقرؤه</b> — لا ميزانَ مراجعةٍ ولا قائمةَ دخلٍ ولا أستاذَ
/// عامّ يستطيع الترشيحَ أو التجميعَ به. <b>فالمستخدم يظنّ أنّه يُصنّف مصروفاته وهو
/// لا يُصنّف شيئاً</b>: القيمةُ تُكتب في القاعدة ولا تخرج منها أبداً.</para>
///
/// <para><b>وسطرٌ بلا مركز يُعرض ولا يُحذف</b> — تحت «غير مُصنَّف». فتقريرٌ يُخفي غيرَ
/// المصنَّف يجعل مجموعَه أصغرَ من قائمة الدخل بلا سببٍ ظاهر، <b>ويُقرأ نقصُه دقّةً</b>.
/// وظهورُه هو ما يُخبر المالكَ كم من مصروفه لم يُصنَّف بعد.</para>
/// </summary>
public class CostCenterStatementQuery(BrqoshDbContext db)
{
    /// <param name="costCenterId">مركزٌ بعينه، أو <c>null</c> لكلّ المراكز.</param>
    /// <param name="profitAndLossOnly">
    /// ‏الإيراداتُ والمصروفاتُ وحدها — وهو الأصل. <b>ومركزُ التكلفة مفهومُ أداءٍ لا مركزٍ
    /// ماليّ</b>: تصنيفُ ذمّةٍ مدينةٍ على مركزٍ لا يُجيب سؤالاً يُسأل.
    /// </param>
    public async Task<IReadOnlyList<CostCenterStatementRow>> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate,
        long? costCenterId = null, bool profitAndLossOnly = true,
        CancellationToken ct = default)
    {
        var totals = await db.JournalEntries
            .WhereReportable(companyId, fromDate, toDate)
            .SelectMany(e => e.Lines)
            .Where(l => costCenterId == null || l.CostCenterId == costCenterId)
            .GroupBy(l => new { l.CostCenterId, l.AccountId })
            .Select(g => new
            {
                g.Key.CostCenterId,
                g.Key.AccountId,
                Debit = g.Sum(l => l.Debit),
                Credit = g.Sum(l => l.Credit),
            })
            .ToListAsync(ct);

        if (totals.Count == 0) return [];

        var accountIds = totals.Select(t => t.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var centerIds = totals.Where(t => t.CostCenterId is not null)
            .Select(t => t.CostCenterId!.Value).Distinct().ToList();
        var centers = await db.CostCenters.IgnoreQueryFilters().AsNoTracking()
            .Where(c => centerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        return totals
            .Select(t =>
            {
                var account = accounts[t.AccountId];
                var center = t.CostCenterId is long id && centers.TryGetValue(id, out var c) ? c : null;
                return new CostCenterStatementRow(
                    t.CostCenterId,
                    center?.Code ?? "—",
                    center?.NameAr ?? "غير مُصنَّف",
                    account.Code, account.NameAr, account.Type,
                    t.Debit, t.Credit);
            })
            .Where(r => !profitAndLossOnly
                        || r.AccountType is AccountType.Revenue or AccountType.Expense)
            .OrderBy(r => r.CostCenterCode, StringComparer.Ordinal)
            .ThenBy(r => r.AccountCode, StringComparer.Ordinal)
            .ToList();
    }
}
