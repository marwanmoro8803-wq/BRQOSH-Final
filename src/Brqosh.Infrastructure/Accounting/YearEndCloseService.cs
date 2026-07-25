using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

public record YearEndCloseResult(int Year, long ClosingEntryId, string EntryNumber, decimal NetIncome);

/// <summary>
/// إقفال سنوي SOCPA: إغلاق الإيرادات والمصروفات في 3104 ثم ترحيل النتيجة إلى 3103،
/// وإغلاق جميع الفترات المالية للسنة.
/// </summary>
public class YearEndCloseService(BrqoshDbContext db)
{
    public async Task<YearEndCloseResult> CloseYearAsync(
        long companyId, int year, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);

            var existingClose = await db.JournalEntries.AnyAsync(e =>
                e.CompanyId == companyId
                && e.SourceDocument == $"YearEndClose:{year}"
                && e.Status != JournalEntryStatus.Draft, token);
            if (existingClose)
                throw new AccountingException($"السنة {year} مقفلة مسبقاً.");

            var openOutside = await db.JournalEntries.AnyAsync(e =>
                e.CompanyId == companyId
                && e.Status == JournalEntryStatus.Posted
                && e.EntryDate > yearEnd, token);
            if (openOutside)
                throw new AccountingException(
                    $"توجد قيود مرحّلة بعد {yearEnd:yyyy-MM-dd} — أغلق الفترات اللاحقة أو انقل القيود قبل الإقفال.");

            var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, yearStart, yearEnd);
            var closingDate = yearEnd;

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, closingDate);

            var currentYear = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.CurrentYearProfit, token);
            var retained = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.RetainedEarnings, token);

            var entry = JournalEntry.CreateDraft(
                companyId, closingDate,
                $"إقفال السنة المالية {year}",
                $"YearEndClose:{year}");

            foreach (var row in pnl.RevenueRows)
            {
                var account = await AccountAsync(companyId, row.AccountCode, token);
                entry.AddLine(account, row.Amount, 0m, $"إقفال إيراد {row.AccountCode}");
                entry.AddLine(currentYear, 0m, row.Amount, $"تحويل إيراد {row.AccountCode}");
            }

            foreach (var row in pnl.ExpenseRows)
            {
                var account = await AccountAsync(companyId, row.AccountCode, token);
                entry.AddLine(currentYear, row.Amount, 0m, $"تحويل مصروف {row.AccountCode}");
                entry.AddLine(account, 0m, row.Amount, $"إقفال مصروف {row.AccountCode}");
            }

            // ترحيل صافي الدخل إلى الأرباح المبقاة
            if (pnl.NetIncome > 0)
            {
                entry.AddLine(currentYear, pnl.NetIncome, 0m, "إقفال أرباح العام");
                entry.AddLine(retained, 0m, pnl.NetIncome, "ترحيل للأرباح المبقاة");
            }
            else if (pnl.NetIncome < 0)
            {
                var loss = Math.Abs(pnl.NetIncome);
                entry.AddLine(retained, loss, 0m, "ترحيل خسائر العام");
                entry.AddLine(currentYear, 0m, loss, "إقفال خسائر العام");
            }

            if (entry.Lines.Count < 2)
            {
                await CloseAllYearPeriodsAsync(companyId, year, username, token);
                await new AuditService(db).LogAsync(companyId, username, "إقفال سنوي",
                    "FiscalYear", year.ToString(), "سنة بلا حركة — إغلاق فترات فقط");
                return new YearEndCloseResult(year, 0, "", 0m);
            }

            if (!entry.IsBalanced)
                throw new AccountingException(
                    $"قيد الإقفال غير متوازن: مدين {entry.TotalDebit:N2} / دائن {entry.TotalCredit:N2}.");

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(companyId, closingDate, token);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await CloseAllYearPeriodsAsync(companyId, year, username, token);

            await new AuditService(db).LogAsync(companyId, username, "إقفال سنوي",
                "FiscalYear", year.ToString(),
                $"قيد {entryNumber} — صافي الدخل {pnl.NetIncome:N2}");

            return new YearEndCloseResult(year, entry.Id, entryNumber, pnl.NetIncome);
        }, ct);
    }

    private async Task CloseAllYearPeriodsAsync(long companyId, int year, string username, CancellationToken token)
    {
        var periods = await db.FiscalPeriods
            .Where(p => p.CompanyId == companyId
                        && p.StartDate.Year == year
                        && p.Status == FiscalPeriodStatus.Open)
            .ToListAsync(token);

        foreach (var period in periods)
        {
            period.Close();
            await new AuditService(db).LogAsync(companyId, username, "إغلاق فترة مالية (إقفال سنوي)",
                "FiscalPeriod", period.Code, $"سنة {year}");
        }

        await db.SaveChangesAsync(token);
    }

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
