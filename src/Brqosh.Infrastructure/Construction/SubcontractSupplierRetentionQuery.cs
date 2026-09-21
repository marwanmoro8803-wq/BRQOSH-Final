using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>
/// محتجزات مقاولي الباطن المسجّلة على ذمم الموردين — من قيود SubcontractRetentionHold.
/// </summary>
public class SubcontractSupplierRetentionQuery(BrqoshDbContext db)
{
    public record WithholdLine(DateOnly Date, string AgreementNumber, decimal Amount);

    public async Task<decimal> WithheldForSupplierAsync(long companyId, long supplierId, CancellationToken ct = default)
    {
        var gross = await GrossWithheldForSupplierAsync(companyId, supplierId, ct);
        var released = await ReleasedForSupplierAsync(companyId, supplierId, ct);
        return Math.Max(0m, gross - released);
    }

    /// <summary>محتجز باطن إجمالي قبل طرح الإفراجات.</summary>
    public async Task<decimal> GrossWithheldForSupplierAsync(long companyId, long supplierId, CancellationToken ct = default)
    {
        var map = await AgreementSupplierMapAsync(companyId, ct);
        return await SumWithheldAsync(companyId, map, supplierId, ct);
    }

    /// <summary>إفراجات محتجز مقاول باطن المُسجَّلة على ذمم المورد (تُخصَّص حسب نسبة المحتجز لكل مشروع).</summary>
    public Task<decimal> ReleasedForSupplierAsync(long companyId, long supplierId, CancellationToken ct = default) =>
        ComputeReleasedForSupplierAsync(companyId, supplierId, ct);

    public async Task<IReadOnlyDictionary<long, decimal>> AllWithheldBySupplierAsync(long companyId, CancellationToken ct = default)
    {
        var map = await AgreementSupplierMapAsync(companyId, ct);
        var result = new Dictionary<long, decimal>();
        foreach (var supplierId in map.Values.Distinct())
        {
            var net = await WithheldForSupplierAsync(companyId, supplierId, ct);
            if (net != 0) result[supplierId] = net;
        }
        return result;
    }

    public async Task<IReadOnlyList<WithholdLine>> StatementLinesForSupplierAsync(
        long companyId, long supplierId, CancellationToken ct = default)
    {
        var map = await AgreementSupplierMapAsync(companyId, ct);
        var agreementIds = map.Where(kv => kv.Value == supplierId).Select(kv => kv.Key).ToHashSet();
        if (agreementIds.Count == 0) return [];

        var payableAccount = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable)
            .Select(a => a.Id)
            .SingleAsync(ct);

        var entries = await db.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.SourceDocument != null
                        && e.SourceDocument.StartsWith("SubcontractRetentionHold:"))
            .ToListAsync(ct);

        var agreementNumbers = await db.SubcontractAgreements
            .Where(a => a.CompanyId == companyId && agreementIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.AgreementNumber, ct);

        var lines = new List<WithholdLine>();
        foreach (var entry in entries)
        {
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 3 || !long.TryParse(parts[2], out var agreementId)) continue;
            if (!agreementIds.Contains(agreementId)) continue;

            var amount = entry.Lines.Where(l => l.AccountId == payableAccount).Sum(l => l.Debit);
            if (amount == 0) continue;

            lines.Add(new WithholdLine(
                entry.EntryDate,
                agreementNumbers.GetValueOrDefault(agreementId, agreementId.ToString()),
                amount));
        }

        return lines;
    }

    private async Task<decimal> ComputeReleasedForSupplierAsync(long companyId, long supplierId, CancellationToken ct)
    {
        var agreementSuppliers = await AgreementSupplierMapAsync(companyId, ct);
        var supplierAgreementIds = agreementSuppliers.Where(kv => kv.Value == supplierId).Select(kv => kv.Key).ToList();
        if (supplierAgreementIds.Count == 0) return 0m;

        var projectIds = await db.SubcontractAgreements
            .Where(a => a.CompanyId == companyId && supplierAgreementIds.Contains(a.Id))
            .Select(a => a.ProjectId)
            .Distinct()
            .ToListAsync(ct);

        var heldByProject = await HeldByProjectAndSupplierAsync(companyId, agreementSuppliers, ct);
        var releasedByProject = await LoadSubcontractReleasedByProjectAsync(companyId, ct);

        decimal total = 0m;
        foreach (var projectId in projectIds)
        {
            if (!releasedByProject.TryGetValue(projectId, out var projectReleased) || projectReleased == 0)
                continue;

            var projectHeld = heldByProject.GetValueOrDefault((projectId, supplierId));
            var totalProjectHeld = heldByProject
                .Where(kv => kv.Key.ProjectId == projectId)
                .Sum(kv => kv.Value);
            if (totalProjectHeld == 0) continue;

            total += Math.Round(projectReleased * projectHeld / totalProjectHeld, 2, MidpointRounding.AwayFromZero);
        }

        return total;
    }

    private async Task<Dictionary<(long ProjectId, long SupplierId), decimal>> HeldByProjectAndSupplierAsync(
        long companyId, IReadOnlyDictionary<long, long> agreementSuppliers, CancellationToken ct)
    {
        var payableAccount = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable)
            .Select(a => a.Id)
            .SingleAsync(ct);

        var agreementToSupplier = agreementSuppliers.ToDictionary(kv => kv.Key, kv => kv.Value);
        var entries = await db.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.SourceDocument != null
                        && e.SourceDocument.StartsWith("SubcontractRetentionHold:"))
            .ToListAsync(ct);

        var result = new Dictionary<(long, long), decimal>();
        foreach (var entry in entries)
        {
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 3 || !long.TryParse(parts[1], out var projectId)) continue;
            if (!long.TryParse(parts[2], out var agreementId)) continue;
            if (!agreementToSupplier.TryGetValue(agreementId, out var supId)) continue;

            var amount = entry.Lines.Where(l => l.AccountId == payableAccount).Sum(l => l.Debit);
            if (amount == 0) continue;

            var key = (projectId, supId);
            result[key] = result.GetValueOrDefault(key) + amount;
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadSubcontractReleasedByProjectAsync(long companyId, CancellationToken ct)
    {
        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.SourceDocument != null
                        && e.SourceDocument.StartsWith("RetentionRelease:"))
            .ToListAsync(ct);

        var result = new Dictionary<long, decimal>();
        foreach (var entry in entries)
        {
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 3) continue;
            if (!long.TryParse(parts[1], out var projectId)) continue;
            if (!int.TryParse(parts[2], out var typeInt)) continue;
            if (typeInt != (int)RetentionReleaseType.SubcontractRelease) continue;

            result[projectId] = result.GetValueOrDefault(projectId) + entry.TotalDebit;
        }

        return result;
    }

    private async Task<Dictionary<long, long>> AgreementSupplierMapAsync(long companyId, CancellationToken ct) =>
        await db.SubcontractAgreements
            .Where(a => a.CompanyId == companyId)
            .ToDictionaryAsync(a => a.Id, a => a.SupplierId, ct);

    private async Task<decimal> SumWithheldAsync(
        long companyId, IReadOnlyDictionary<long, long> agreementSuppliers, long supplierId, CancellationToken ct)
    {
        var agreementIds = agreementSuppliers
            .Where(kv => kv.Value == supplierId)
            .Select(kv => kv.Key)
            .ToHashSet();
        if (agreementIds.Count == 0) return 0m;

        var payableAccount = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable)
            .Select(a => a.Id)
            .SingleAsync(ct);

        var entries = await db.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.SourceDocument != null
                        && e.SourceDocument.StartsWith("SubcontractRetentionHold:"))
            .ToListAsync(ct);

        decimal total = 0;
        foreach (var entry in entries)
        {
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 3 || !long.TryParse(parts[2], out var agreementId)) continue;
            if (!agreementIds.Contains(agreementId)) continue;
            total += entry.Lines.Where(l => l.AccountId == payableAccount).Sum(l => l.Debit);
        }

        return total;
    }
}
