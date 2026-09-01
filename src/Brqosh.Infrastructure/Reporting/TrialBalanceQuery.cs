using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record TrialBalanceRow(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal TotalDebit,
    decimal TotalCredit)
{
    /// <summary>الرصيد المدين الصافي (يظهر عندما يغلب المدين).</summary>
    public decimal BalanceDebit => TotalDebit > TotalCredit ? TotalDebit - TotalCredit : 0m;

    /// <summary>الرصيد الدائن الصافي (يظهر عندما يغلب الدائن).</summary>
    public decimal BalanceCredit => TotalCredit > TotalDebit ? TotalCredit - TotalDebit : 0m;
}

/// <summary>
/// ميزان المراجعة بالمجاميع والأرصدة — من القيود المرحّلة فقط (المسودات لا تدخل الأرصدة).
/// </summary>
public class TrialBalanceQuery(BrqoshDbContext db)
{
    /// <param name="excludeYearEndClosing">
    /// استبعاد قيود الإقفال السنوي — تستخدمه قائمة الدخل لعرض أداء الفترة (ع-R2).
    /// الافتراضي false: ميزان المراجعة سجل خام للأستاذ، وقيد الإقفال قيد مرحّل حقيقي فيه.
    /// </param>
    /// <param name="costCenterId">
    /// (CEEF-230 · ط1-٧) ‏مركزُ تكلفةٍ بعينه — و<c>null</c> يعني الكلّ، وهو الأصل.
    ///
    /// <para><b>وميزانٌ مُرشَّحٌ بمركزٍ لا يتوازن بالضرورة</b>، ولا يُنتظر منه ذلك: هو
    /// مقطعٌ من الأستاذ لا دفترٌ قائمٌ بذاته. <b>وقارئٌ يتوقّع توازنَه يظنّ العطلَ حيث
    /// لا عطل</b> — ولهذا يُقال هنا صراحة.</para>
    /// </param>
    public async Task<IReadOnlyList<TrialBalanceRow>> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, bool excludeYearEndClosing = false,
        long? costCenterId = null)
    {
        var entries = db.JournalEntries.WhereReportable(companyId, fromDate, toDate);
        if (excludeYearEndClosing)
            entries = entries.ExcludeYearEndClosing();

        // تجميع على مستوى قاعدة البيانات — لا تحميل سطور القيود إلى الذاكرة
        var totals = await entries
            .SelectMany(e => e.Lines)
            .Where(l => costCenterId == null || l.CostCenterId == costCenterId)
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(l => l.Debit),
                Credit = g.Sum(l => l.Credit),
            })
            .ToListAsync();

        if (totals.Count == 0)
            return [];

        var accountIds = totals.Select(t => t.AccountId).ToList();
        // التقارير التاريخية يجب أن ترى الحسابات المعطَّلة (حذف ناعم) وإلا سقط الميزان
        // بـ KeyNotFoundException عند أول حساب يُوقَف وعليه حركة مرحّلة.
        var accounts = await db.Accounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        return totals
            .Select(t =>
            {
                var account = accounts[t.AccountId];
                return new TrialBalanceRow(account.Code, account.NameAr, account.Type, t.Debit, t.Credit);
            })
            .OrderBy(r => r.AccountCode)
            .ToList();
    }
}
