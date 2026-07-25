using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record IncomeStatementRow(string AccountCode, string AccountNameAr, AccountType Type, decimal Amount);

public record IncomeStatementResult(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<IncomeStatementRow> RevenueRows,
    IReadOnlyList<IncomeStatementRow> ExpenseRows)
{
    public decimal TotalRevenue => RevenueRows.Sum(r => r.Amount);
    public decimal TotalExpenses => ExpenseRows.Sum(r => r.Amount);
    public decimal NetIncome => TotalRevenue - TotalExpenses;
}

/// <summary>قائمة الدخل — حركة الإيرادات والمصروفات خلال الفترة (القيود المرحّلة فقط).</summary>
public class IncomeStatementQuery(BrqoshDbContext db)
{
    public async Task<IncomeStatementResult> ExecuteAsync(long companyId, DateOnly from, DateOnly to)
    {
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, from, to);
        var revenue = rows
            .Where(r => r.Type == AccountType.Revenue)
            .Select(r => new IncomeStatementRow(r.AccountCode, r.AccountNameAr, r.Type, r.TotalCredit - r.TotalDebit))
            .Where(r => r.Amount != 0)
            .OrderBy(r => r.AccountCode)
            .ToList();

        var expenses = rows
            .Where(r => r.Type == AccountType.Expense)
            .Select(r => new IncomeStatementRow(r.AccountCode, r.AccountNameAr, r.Type, r.TotalDebit - r.TotalCredit))
            .Where(r => r.Amount != 0)
            .OrderBy(r => r.AccountCode)
            .ToList();

        return new IncomeStatementResult(from, to, revenue, expenses);
    }
}
