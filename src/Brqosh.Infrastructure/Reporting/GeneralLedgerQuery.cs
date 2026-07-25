using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record GeneralLedgerLine(
    DateOnly EntryDate,
    string EntryNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record GeneralLedgerAccount(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    IReadOnlyList<GeneralLedgerLine> Lines,
    decimal ClosingBalance);

/// <summary>دفتر الأستاذ العام — حركة كل حساب مع رصيد متحرك (القيود المرحّلة فقط).</summary>
public class GeneralLedgerQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<GeneralLedgerAccount>> ExecuteAsync(
        long companyId, DateOnly from, DateOnly to, string? accountCodeFilter = null)
    {
        var entries = await db.JournalEntries
            .WhereReportable(companyId, from, to)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.EntryNumber)
            .ToListAsync();

        var priorEntries = await db.JournalEntries
            .WhereReportable(companyId, to: from.AddDays(-1))
            .Include(e => e.Lines)
            .ToListAsync();

        var accountIds = entries.SelectMany(e => e.Lines).Select(l => l.AccountId)
            .Concat(priorEntries.SelectMany(e => e.Lines).Select(l => l.AccountId))
            .Distinct().ToList();
        var accounts = await db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var openingByAccount = priorEntries
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => SumDelta(accounts[g.Key], g));

        var byAccount = entries
            .SelectMany(e => e.Lines.Select(l => new { Entry = e, Line = l }))
            .GroupBy(x => x.Line.AccountId);

        var result = new List<GeneralLedgerAccount>();
        foreach (var group in byAccount.OrderBy(g => accounts[g.Key].Code))
        {
            var account = accounts[group.Key];
            if (accountCodeFilter is not null && !account.Code.StartsWith(accountCodeFilter, StringComparison.Ordinal))
                continue;

            var opening = openingByAccount.GetValueOrDefault(account.Id, 0m);
            var running = opening;
            var lines = new List<GeneralLedgerLine>();

            if (opening != 0)
            {
                lines.Add(new GeneralLedgerLine(
                    from,
                    "",
                    "رصيد افتتاحي",
                    account.Type.IsDebitNature() && opening > 0 ? opening : 0m,
                    account.Type.IsDebitNature() && opening < 0 ? -opening :
                    !account.Type.IsDebitNature() && opening > 0 ? opening :
                    !account.Type.IsDebitNature() && opening < 0 ? -opening : 0m,
                    opening));
            }

            lines.AddRange(group
                .OrderBy(x => x.Entry.EntryDate).ThenBy(x => x.Entry.EntryNumber)
                .Select(x =>
                {
                    var delta = account.Type.IsDebitNature()
                        ? x.Line.Debit - x.Line.Credit
                        : x.Line.Credit - x.Line.Debit;
                    running += delta;
                    return new GeneralLedgerLine(
                        x.Entry.EntryDate,
                        x.Entry.EntryNumber ?? "",
                        string.IsNullOrWhiteSpace(x.Line.Description) ? x.Entry.Description : x.Line.Description,
                        x.Line.Debit, x.Line.Credit, running);
                }));

            result.Add(new GeneralLedgerAccount(account.Code, account.NameAr, account.Type, lines, running));
        }

        return result;
    }

    private static decimal SumDelta(Account account, IEnumerable<JournalLine> lines) =>
        lines.Sum(l => account.Type.IsDebitNature() ? l.Debit - l.Credit : l.Credit - l.Debit);
}
