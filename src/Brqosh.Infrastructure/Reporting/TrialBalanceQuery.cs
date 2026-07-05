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
    public async Task<IReadOnlyList<TrialBalanceRow>> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate)
    {
        // تجميع على مستوى قاعدة البيانات — لا تحميل سطور القيود إلى الذاكرة
        var totals = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                        && e.Status == JournalEntryStatus.Posted
                        && e.EntryDate >= fromDate && e.EntryDate <= toDate)
            .SelectMany(e => e.Lines)
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
        var accounts = await db.Accounts
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
