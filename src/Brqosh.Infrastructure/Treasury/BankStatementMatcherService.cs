using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public sealed record BankMatchPair(long StatementLineIndex, long JournalEntryId, decimal Amount);

public sealed record BankStatementMatchReport(
    int UnmatchedBefore,
    int MatchedCount,
    int UnmatchedAfter,
    IReadOnlyList<BankMatchPair> Pairs);

/// <summary>مطابقة تلقائية: نفس التاريخ + نفس المبلغ + سطر دفتر غير مُسوّى.</summary>
public sealed class BankStatementMatcherService(BrqoshDbContext db)
{
    public async Task<BankStatementMatchReport> AutoMatchAsync(
        long companyId,
        long bankStatementId,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        var statement = await db.BankStatements
            .Include(s => s.Lines)
            .SingleAsync(s => s.CompanyId == companyId && s.Id == bankStatementId, ct);

        var from = statement.PeriodStart ?? DateOnly.FromDateTime(DateTime.Today);
        var to = statement.PeriodEnd ?? from;

        var recon = new BankReconciliationQuery(db);
        var bookLines = (await recon.GetBookLinesAsync(companyId, statement.BankAccountId, from, to))
            .Where(l => !l.IsReconciled)
            .ToList();

        var unmatchedStatement = statement.Lines
            .Select((line, index) => (line, index))
            .Where(x => !x.line.IsMatched)
            .ToList();

        // ‏(ح-١٥٥) ‏**كلُّ سطرٍ في الدفتر يُطابَق مرّةً** — لا كلُّ قيد: قيدٌ بشيكين يُطابَق كلُّ شيكٍ بسطره في الكشف.
        var usedLines = new HashSet<long>();
        var pairs = new List<BankMatchPair>();

        foreach (var (line, index) in unmatchedStatement)
        {
            var candidate = bookLines.FirstOrDefault(b =>
                !usedLines.Contains(b.JournalLineId)
                && b.EntryDate == line.TransactionDate
                && b.Debit == line.ExpectedBookDebit
                && b.Credit == line.ExpectedBookCredit);

            if (candidate is null)
                continue;

            line.Match(candidate.JournalEntryId);
            usedLines.Add(candidate.JournalLineId);
            pairs.Add(new BankMatchPair(index, candidate.JournalEntryId, line.SignedAmount));

            await recon.ReconcileAsync(companyId, statement.BankAccountId, candidate.JournalEntryId, line.TransactionDate,
                candidate.JournalLineId);
        }

        if (pairs.Count > 0)
            await db.SaveChangesAsync(ct);

        var unmatchedAfter = statement.Lines.Count(l => !l.IsMatched);
        return new BankStatementMatchReport(
            unmatchedStatement.Count,
            pairs.Count,
            unmatchedAfter,
            pairs);
    }
}
