using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public record BankBookLine(
    long JournalEntryId,
    DateOnly EntryDate,
    string? EntryNumber,
    string? Description,
    decimal Debit,
    decimal Credit,
    bool IsReconciled,
    long JournalLineId = 0);

public record BankReconciliationSummary(
    decimal OpeningBalance,
    decimal PeriodMovement,
    decimal ClosingBalance,
    decimal ReconciledBalance,
    decimal UnreconciledBalance,
    int TotalLines,
    int ReconciledCount);

/// <summary>
/// <b>مذكّرةُ التسوية</b>: رصيدُ البنك المعدَّل مقابلَ رصيد الدفتر المعدَّل.
/// </summary>
/// <param name="StatementClosing">الختاميُّ كما يقوله البنك.</param>
/// <param name="BookBalance">رصيدُ حساب البنك في الدفتر حتى آخر يومٍ في الكشف.</param>
/// <param name="DepositsInTransit">إيداعاتٌ في الدفتر لم تُسوَّ — لم تظهر في البنك بعد.</param>
/// <param name="OutstandingPayments">مدفوعاتٌ في الدفتر لم تُسوَّ — لم تُصرف من البنك بعد.</param>
/// <param name="UnbookedBankItems">سطورُ كشفٍ لم تُطابَق — رسومٌ وفوائدُ لم تُقيَّد في الدفتر بعد.</param>
public record BankReconciliationStatement(
    DateOnly AsOf,
    decimal StatementClosing,
    decimal BookBalance,
    decimal DepositsInTransit,
    decimal OutstandingPayments,
    decimal UnbookedBankItems,
    decimal AdjustedBank,
    decimal AdjustedBook,
    decimal Difference);

public class BankReconciliationQuery(BrqoshDbContext db)
{
    /// <summary>
    /// (ح-١٥٤) <b>مذكّرةُ تسوية الكشف</b>: الختاميُّ + الإيداعاتُ في الطريق − المدفوعاتُ المعلّقة، مقابلَ الدفتر + ما في البنك ولم يُقيَّد.
    /// والفرقُ صفرٌ حين تكتمل التسوية — <b>وكان لا يُحسب أصلاً لأنّ الكشفَ بلا رصيد</b>.
    /// </summary>
    public async Task<BankReconciliationStatement> GetStatementReconciliationAsync(
        long companyId, long statementId, CancellationToken ct = default)
    {
        var statement = await db.BankStatements.AsNoTracking()
            .SingleAsync(s => s.CompanyId == companyId && s.Id == statementId, ct);
        if (statement.ClosingBalance is not decimal closing || statement.PeriodEnd is not DateOnly asOf)
            throw new AccountingException("الكشفُ بلا رصيدٍ ختاميّ — أعد استيرادَه برصيدَيه لتُبنى مذكّرةُ التسوية.");

        var book = await GetOpeningBalanceAsync(companyId, statement.BankAccountId, asOf.AddDays(1));
        var unreconciled = (await GetBookLinesAsync(companyId, statement.BankAccountId, DateOnly.MinValue, asOf))
            .Where(l => !l.IsReconciled)
            .ToList();
        var inTransit = unreconciled.Sum(l => l.Debit);
        var outstanding = unreconciled.Sum(l => l.Credit);
        var unbooked = statement.Lines.Where(l => !l.IsMatched).Sum(l => l.SignedAmount);

        var adjustedBank = closing + inTransit - outstanding;
        var adjustedBook = book + unbooked;
        return new BankReconciliationStatement(asOf, closing, book, inTransit, outstanding, unbooked,
            adjustedBank, adjustedBook, adjustedBank - adjustedBook);
    }

    public async Task<IReadOnlyList<BankBookLine>> GetBookLinesAsync(
        long companyId, long bankAccountId, DateOnly from, DateOnly to)
    {
        var bank = await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId);
        var glAccountId = bank.GlAccountId;

        // ‏(ح-١٥٥) ‏**المُسوّى سطرٌ لا قيد** — والسجلُّ القائمُ بلا سطرٍ (صفر) يُسوّي سطورَ قيده كلَّها كما كان.
        var reconciled = await db.ReconciledBankEntries
            .Where(r => r.CompanyId == companyId && r.BankAccountId == bankAccountId)
            .Select(r => new { r.JournalEntryId, r.JournalLineId })
            .ToListAsync();
        var wholeEntries = reconciled.Where(r => r.JournalLineId == 0).Select(r => r.JournalEntryId).ToHashSet();
        var reconciledLines = reconciled.Where(r => r.JournalLineId != 0).Select(r => r.JournalLineId).ToHashSet();

        // ‏ **سطورُ البنك وحدَها من القاعدة** — وكان يُحمّل كلَّ قيدٍ في الفترة بسطوره
        // ‏كلِّها (السطورُ مملوكةٌ تُحمَّل معه) ثمّ يُرشّح الحسابَ في الذاكرة: شاشةُ التسوية والمطابقةُ الآليّةُ تقرآن الدفترَ كلَّه لحسابٍ واحد.
        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                && e.EntryDate >= from && e.EntryDate <= to)
            .SelectMany(e => e.Lines, (e, l) => new
            {
                e.Id, e.EntryDate, e.EntryNumber, EntryDescription = e.Description,
                LineId = EF.Property<long>(l, "id"),
                l.AccountId, l.Description, l.Debit, l.Credit,
            })
            .Where(x => x.AccountId == glAccountId)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.EntryNumber).ThenBy(x => x.LineId)
            .ToListAsync();

        return lines
            .Select(x => new BankBookLine(
                x.Id, x.EntryDate, x.EntryNumber,
                x.Description ?? x.EntryDescription,
                x.Debit, x.Credit,
                wholeEntries.Contains(x.Id) || reconciledLines.Contains(x.LineId),
                x.LineId))
            .ToList();
    }

    public async Task<decimal> GetOpeningBalanceAsync(
        long companyId, long bankAccountId, DateOnly from)
    {
        var bank = await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId);
        var glAccountId = bank.GlAccountId;

        // ‏(ح-٩٣) **وكان يُحمّل كلَّ قيدٍ قبل الفترة** — تاريخَ المنشأة كلَّه — لرصيد حسابٍ واحد. والجمعُ في الذاكرة على سطور البنك وحدَها:
        // ‏SQLite لا يجمع `decimal`.
        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                && e.EntryDate < from)
            .SelectMany(e => e.Lines, (e, l) => new { l.AccountId, l.Debit, l.Credit })
            .Where(x => x.AccountId == glAccountId)
            .Select(x => new { x.Debit, x.Credit })
            .ToListAsync();

        return lines.Sum(x => x.Debit - x.Credit);
    }

    public async Task<BankReconciliationSummary> GetSummaryAsync(
        long companyId, long bankAccountId, DateOnly from, DateOnly to)
    {
        var opening = await GetOpeningBalanceAsync(companyId, bankAccountId, from);
        var lines = await GetBookLinesAsync(companyId, bankAccountId, from, to);
        var periodMovement = lines.Sum(l => l.Debit - l.Credit);
        var closing = opening + periodMovement;
        var reconciled = lines.Where(l => l.IsReconciled).Sum(l => l.Debit - l.Credit);
        return new BankReconciliationSummary(
            opening, periodMovement, closing, reconciled, periodMovement - reconciled,
            lines.Count, lines.Count(l => l.IsReconciled));
    }

    /// <summary>
    /// ‏يُسوّي قيداً مع كشف البنك — <b>سطراً بعينه</b> إن مُرِّر <paramref name="journalLineId"/>، أو سطورَه على البنك كلَّها بالصفر.
    ///
    /// <para>‏(ح-١٥٥) ‏<b>وكانت التسويةُ للقيد كلِّه دائماً</b>: قيدٌ فيه شيكان على البنك (١٠٠٠ و٥٠٠) يُطابَق أحدُهما بالكشف
    /// ‏فيُعدّ الآخرُ مُسوًّى ولم يظهر في البنك — فتختفي «مدفوعاتٌ معلّقة» من مذكّرة التسوية، ولا يُطابَق سطرُه الحقيقيُّ أبداً.</para>
    /// </summary>
    public async Task ReconcileAsync(
        long companyId, long bankAccountId, long journalEntryId, DateOnly date, long journalLineId = 0)
    {
        SecurityContext.EnsureCanPostAccounting();
        if (await db.ReconciledBankEntries.AnyAsync(r =>
            r.CompanyId == companyId && r.BankAccountId == bankAccountId && r.JournalEntryId == journalEntryId
            && (r.JournalLineId == 0 || r.JournalLineId == journalLineId)))
            return;

        if (journalLineId != 0)
            await EnsureBankLineAsync(companyId, bankAccountId, journalEntryId, journalLineId);

        var entry = ReconciledBankEntry.Create(companyId, bankAccountId, journalEntryId, date, journalLineId);
        await BranchAssignmentHelper.EnsureAssignedAsync(entry, db);
        db.ReconciledBankEntries.Add(entry);
        await db.SaveChangesAsync();
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(),
            "تسوية بنكية", "BankReconciliation", journalEntryId.ToString(),
            journalLineId == 0 ? $"حساب بنكي {bankAccountId}" : $"حساب بنكي {bankAccountId} · سطر {journalLineId}");
    }

    /// <summary>
    /// ‏يُلغي تسويةَ سطرٍ بعينه، أو القيدِ كلِّه بلا سطر (مساراتُ العكس).
    /// <para>‏(ح-١٥٥) ‏<b>وإلغاءُ سطرٍ من قيدٍ سُوّي كلُّه</b> يُبقي إخوتَه مُسوَّين: يُستبدل بسجلّ القيد سجلٌّ لكلّ سطرٍ آخرَ على البنك.</para>
    /// </summary>
    public async Task UnreconcileAsync(long companyId, long bankAccountId, long journalEntryId, long? journalLineId = null)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        var rows = await db.ReconciledBankEntries
            .Where(r => r.CompanyId == companyId && r.BankAccountId == bankAccountId && r.JournalEntryId == journalEntryId)
            .ToListAsync();
        if (rows.Count == 0)
            return;

        if (journalLineId is not long lineId)
        {
            db.ReconciledBankEntries.RemoveRange(rows);
        }
        else if (rows.SingleOrDefault(r => r.JournalLineId == lineId) is { } lineRow)
        {
            db.ReconciledBankEntries.Remove(lineRow);
        }
        else if (rows.SingleOrDefault(r => r.JournalLineId == 0) is { } wholeRow)
        {
            db.ReconciledBankEntries.Remove(wholeRow);
            foreach (var sibling in (await BankLineIdsAsync(companyId, bankAccountId, journalEntryId)).Where(id => id != lineId))
            {
                var kept = ReconciledBankEntry.Create(companyId, bankAccountId, journalEntryId, wholeRow.ReconciledDate, sibling);
                await BranchAssignmentHelper.EnsureAssignedAsync(kept, db);
                db.ReconciledBankEntries.Add(kept);
            }
        }
        else
        {
            return;
        }

        await db.SaveChangesAsync();
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(),
            "إلغاء تسوية بنكية", "BankReconciliation", journalEntryId.ToString(),
            journalLineId is long l ? $"حساب بنكي {bankAccountId} · سطر {l}" : $"حساب بنكي {bankAccountId}");
    }

    private async Task<List<long>> BankLineIdsAsync(long companyId, long bankAccountId, long journalEntryId)
    {
        var bank = await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId);
        return await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.Id == journalEntryId)
            .SelectMany(e => e.Lines, (e, l) => new { l.AccountId, LineId = EF.Property<long>(l, "id") })
            .Where(x => x.AccountId == bank.GlAccountId)
            .Select(x => x.LineId)
            .ToListAsync();
    }

    /// <summary>‏(ح-١٥٥) ‏السطرُ المُسوّى سطرٌ من هذا القيد على حساب هذا البنك — لا رقمٌ يُمرَّر من غيره.</summary>
    private async Task EnsureBankLineAsync(long companyId, long bankAccountId, long journalEntryId, long journalLineId)
    {
        if (!(await BankLineIdsAsync(companyId, bankAccountId, journalEntryId)).Contains(journalLineId))
            throw new AccountingException("السطرُ ليس من هذا القيد على حساب هذا البنك — لا يُسوّى.");
    }
}
