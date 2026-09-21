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

public class BankReconciliationQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<BankBookLine>> GetBookLinesAsync(
        long companyId, long bankAccountId, DateOnly from, DateOnly to)
    {
        var bank = await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId);
        var glAccountId = bank.GlAccountId;

        var reconciledIds = (await db.ReconciledBankEntries
            .Where(r => r.CompanyId == companyId && r.BankAccountId == bankAccountId)
            .Select(r => r.JournalEntryId)
            .ToListAsync()).ToHashSet();

        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                && e.EntryDate >= from && e.EntryDate <= to)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.EntryNumber)
            .ToListAsync();

        var lines = new List<BankBookLine>();
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.Where(l => l.AccountId == glAccountId))
            {
                lines.Add(new BankBookLine(
                    entry.Id, entry.EntryDate, entry.EntryNumber,
                    line.Description ?? entry.Description,
                    line.Debit, line.Credit,
                    reconciledIds.Contains(entry.Id)));
            }
        }

        return lines;
    }

    public async Task<decimal> GetOpeningBalanceAsync(
        long companyId, long bankAccountId, DateOnly from)
    {
        var bank = await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId);
        var glAccountId = bank.GlAccountId;

        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                && e.EntryDate < from)
            .ToListAsync();

        decimal balance = 0m;
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.Where(l => l.AccountId == glAccountId))
                balance += line.Debit - line.Credit;
        }

        return balance;
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
