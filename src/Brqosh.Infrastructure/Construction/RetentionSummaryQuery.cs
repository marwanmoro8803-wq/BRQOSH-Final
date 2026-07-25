using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

public class RetentionSummaryQuery(BrqoshDbContext db)
{
    public record RetentionRow(
        long ProjectId,
        string ProjectCode,
        string ProjectName,
        decimal CustomerRetentionHeld,
        decimal CustomerRetentionReleased,
        decimal CustomerRetentionAvailable,
        decimal SubcontractRetentionHeld,
        decimal SubcontractRetentionReleased,
        decimal SubcontractRetentionAvailable);

    public async Task<IReadOnlyList<RetentionRow>> ListAsync(long companyId, CancellationToken ct = default)
    {
        var projects = await db.ConstructionProjects
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync(ct);

        var billings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync(ct);

        var customerHeld = billings
            .GroupBy(b => b.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.RetentionAmount));

        var subcontractHeld = await LoadSubcontractHeldAsync(companyId, ct);
        var released = await LoadReleasedAsync(companyId, ct);

        return projects.Select(p =>
        {
            var custReleased = released.GetValueOrDefault((p.Id, RetentionReleaseType.CustomerRelease));
            var subReleased = released.GetValueOrDefault((p.Id, RetentionReleaseType.SubcontractRelease));
            var custHeld = customerHeld.GetValueOrDefault(p.Id);
            var subHeld = subcontractHeld.GetValueOrDefault(p.Id);

            return new RetentionRow(
                p.Id, p.Code, p.NameAr,
                custHeld, custReleased, Math.Max(0, custHeld - custReleased),
                subHeld, subReleased, Math.Max(0, subHeld - subReleased));
        }).ToList();
    }

    public async Task<decimal> AvailableAsync(long companyId, long projectId,
        RetentionReleaseType type, CancellationToken ct = default)
    {
        var rows = await ListAsync(companyId, ct);
        var row = rows.FirstOrDefault(r => r.ProjectId == projectId);
        if (row is null) return 0m;
        return type == RetentionReleaseType.CustomerRelease
            ? row.CustomerRetentionAvailable
            : row.SubcontractRetentionAvailable;
    }

    private async Task<Dictionary<long, decimal>> LoadSubcontractHeldAsync(long companyId, CancellationToken ct)
    {
        var retentionAccount = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.RetentionPayableSubcontractors)
            .Select(a => a.Id)
            .SingleAsync(ct);

        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && e.Status == Domain.Accounting.JournalEntryStatus.Posted
                && e.SourceDocument != null
                && e.SourceDocument.StartsWith("SubcontractRetentionHold:"))
            .ToListAsync(ct);

        var result = new Dictionary<long, decimal>();
        foreach (var entry in entries)
        {
            // SubcontractRetentionHold:{projectId}:{agreementId}
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out var projectId)) continue;

            var withheld = entry.Lines
                .Where(l => l.AccountId == retentionAccount)
                .Sum(l => l.Credit);
            result[projectId] = result.GetValueOrDefault(projectId) + withheld;
        }

        return result;
    }

    private async Task<Dictionary<(long ProjectId, RetentionReleaseType Type), decimal>> LoadReleasedAsync(
        long companyId, CancellationToken ct)
    {
        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                && e.Status == Domain.Accounting.JournalEntryStatus.Posted
                && e.SourceDocument != null
                && e.SourceDocument.StartsWith("RetentionRelease:"))
            .ToListAsync(ct);

        var result = new Dictionary<(long, RetentionReleaseType), decimal>();
        foreach (var entry in entries)
        {
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 3) continue;
            if (!long.TryParse(parts[1], out var projectId)) continue;
            if (!int.TryParse(parts[2], out var typeInt)) continue;
            if (!Enum.IsDefined(typeof(RetentionReleaseType), typeInt)) continue;

            var key = (projectId, (RetentionReleaseType)typeInt);
            result[key] = result.GetValueOrDefault(key) + entry.TotalDebit;
        }

        return result;
    }
}
