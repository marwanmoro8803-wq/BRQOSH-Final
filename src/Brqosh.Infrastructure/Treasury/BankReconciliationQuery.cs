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
    bool IsReconciled);

public record BankReconciliationSummary(
    decimal OpeningBalance,
    decimal PeriodMovement,
    decimal ClosingBalance,
    decimal ReconciledBalance,
    decimal UnreconciledBalance,
    int TotalLines,
    int ReconciledCount);

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٤ · <c>treasury-payroll-assets#13</c>) <b>مذكّرةُ التسوية</b>: رصيدُ البنك المعدَّل مقابلَ رصيد الدفتر المعدَّل.
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

        var reconciledIds = (await db.ReconciledBankEntries
            .Where(r => r.CompanyId == companyId && r.BankAccountId == bankAccountId)
            .Select(r => r.JournalEntryId)
            .ToListAsync()).ToHashSet();

        // ‏(جاهزيّةُ الإطلاق · ح-٩٣ · treasury-payroll-assets#13) **سطورُ البنك وحدَها من القاعدة** — وكان يُحمّل كلَّ قيدٍ في الفترة بسطوره
        // ‏كلِّها (السطورُ مملوكةٌ تُحمَّل معه) ثمّ يُرشّح الحسابَ في الذاكرة: شاشةُ التسوية والمطابقةُ الآليّةُ تقرآن الدفترَ كلَّه لحسابٍ واحد.
        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                && e.EntryDate >= from && e.EntryDate <= to)
            .SelectMany(e => e.Lines, (e, l) => new
            {
                e.Id, e.EntryDate, e.EntryNumber, EntryDescription = e.Description,
                l.AccountId, l.Description, l.Debit, l.Credit,
            })
            .Where(x => x.AccountId == glAccountId)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.EntryNumber)
            .ToListAsync();

        return lines
            .Select(x => new BankBookLine(
                x.Id, x.EntryDate, x.EntryNumber,
                x.Description ?? x.EntryDescription,
                x.Debit, x.Credit,
                reconciledIds.Contains(x.Id)))
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

    public async Task ReconcileAsync(long companyId, long bankAccountId, long journalEntryId, DateOnly date)
    {
        SecurityContext.EnsureCanPostAccounting();
        if (await db.ReconciledBankEntries.AnyAsync(r =>
            r.CompanyId == companyId && r.BankAccountId == bankAccountId && r.JournalEntryId == journalEntryId))
            return;

        var entry = ReconciledBankEntry.Create(companyId, bankAccountId, journalEntryId, date);
        await BranchAssignmentHelper.EnsureAssignedAsync(entry, db);
        db.ReconciledBankEntries.Add(entry);
        await db.SaveChangesAsync();
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(),
            "تسوية بنكية", "BankReconciliation", journalEntryId.ToString(),
            $"حساب بنكي {bankAccountId}");
    }

    public async Task UnreconcileAsync(long companyId, long bankAccountId, long journalEntryId)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        var row = await db.ReconciledBankEntries.SingleOrDefaultAsync(r =>
            r.CompanyId == companyId && r.BankAccountId == bankAccountId && r.JournalEntryId == journalEntryId);
        if (row is null)
            return;

        db.ReconciledBankEntries.Remove(row);
        await db.SaveChangesAsync();
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(),
            "إلغاء تسوية بنكية", "BankReconciliation", journalEntryId.ToString(),
            $"حساب بنكي {bankAccountId}");
    }
}
