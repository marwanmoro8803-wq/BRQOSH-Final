using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>
/// IFRS 15 — الاعتراف بالإيراد:
/// 1) إن وُجدت EstimatedTotalCost وتكاليف مسجّلة → cost-to-cost.
/// 2) وإلا → نسبة الأعمال المفوترة / قيمة العقد (billing-based).
/// الفرق بين الأعمال الحالي والإيراد المعترف يُرحَّل كإيراد مؤجل (2105).
/// </summary>
public static class ProgressBillingIfrs15
{
    public static async Task<(decimal RecognizedRevenue, decimal CumulativePercent, string Method)> ComputeAsync(
        BrqoshDbContext db, ProgressBilling billing, CancellationToken ct = default)
    {
        var project = await db.ConstructionProjects.SingleAsync(
            p => p.Id == billing.ProjectId && p.CompanyId == billing.CompanyId, ct);

        if (project.ContractValue <= 0)
            return (billing.CurrentWorkAmount, 0m, "full");

        var incurred = await db.ProjectCostEntries
            .Where(c => c.CompanyId == billing.CompanyId
                        && c.ProjectId == billing.ProjectId
                        && !c.IsReversed)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        if (project.EstimatedTotalCost > 0 && incurred > 0)
            return await CostToCostAsync(db, billing, project, incurred, ct);

        return await BillingBasedAsync(db, billing, project, ct);
    }

    private static async Task<(decimal, decimal, string)> CostToCostAsync(
        BrqoshDbContext db, ProgressBilling billing, ConstructionProject project,
        decimal incurred, CancellationToken ct)
    {
        var percent = Math.Min(100m,
            Math.Round(incurred / project.EstimatedTotalCost * 100m, 4, MidpointRounding.AwayFromZero));
        var revenueToDate = Math.Round(project.ContractValue * percent / 100m, 2, MidpointRounding.AwayFromZero);

        var previouslyRecognized = await PreviouslyRecognizedRevenueAsync(db, billing, ct);
        var period = Math.Round(revenueToDate - previouslyRecognized, 2, MidpointRounding.AwayFromZero);
        if (period < 0) period = 0m;
        if (period > billing.CurrentWorkAmount)
            period = billing.CurrentWorkAmount;

        return (period, percent, "cost-to-cost");
    }

    private static async Task<(decimal, decimal, string)> BillingBasedAsync(
        BrqoshDbContext db, ProgressBilling billing, ConstructionProject project, CancellationToken ct)
    {
        var priorPosted = await db.ProgressBillings
            .Where(b => b.CompanyId == billing.CompanyId
                        && b.ProjectId == billing.ProjectId
                        && b.Status == ProgressBillingStatus.Posted
                        && b.Id != billing.Id)
            .SumAsync(b => (decimal?)b.CurrentWorkAmount, ct) ?? 0m;

        var cumulativeWork = priorPosted + billing.CurrentWorkAmount;
        var cumulativePercent = Math.Min(100m,
            Math.Round(cumulativeWork / project.ContractValue * 100m, 4, MidpointRounding.AwayFromZero));

        var remainingContract = Math.Max(0m, project.ContractValue - priorPosted);
        var periodRevenue = Math.Min(billing.CurrentWorkAmount, remainingContract);
        var revenueToDate = Math.Round(project.ContractValue * cumulativePercent / 100m, 2, MidpointRounding.AwayFromZero);
        periodRevenue = Math.Min(periodRevenue, Math.Max(0m, revenueToDate - priorPosted));
        if (periodRevenue <= 0)
            periodRevenue = Math.Min(billing.CurrentWorkAmount, remainingContract);

        return (periodRevenue, cumulativePercent, "billing-based");
    }

    private static async Task<decimal> PreviouslyRecognizedRevenueAsync(
        BrqoshDbContext db, ProgressBilling billing, CancellationToken ct)
    {
        var billingNumbers = await db.ProgressBillings
            .Where(b => b.CompanyId == billing.CompanyId
                        && b.ProjectId == billing.ProjectId
                        && b.Status == ProgressBillingStatus.Posted
                        && b.Id != billing.Id
                        && b.BillingNumber != null)
            .Select(b => b.BillingNumber!)
            .ToListAsync(ct);

        if (billingNumbers.Count == 0)
            return 0m;

        var sources = billingNumbers.Select(n => $"ProgressBilling:{n}").ToList();
        var revenue = await db.Accounts.SingleAsync(a =>
            a.CompanyId == billing.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.ContractRevenue, ct);

        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == billing.CompanyId
                        && e.SourceDocument != null
                        && sources.Contains(e.SourceDocument)
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed))
            .ToListAsync(ct);

        return entries.SelectMany(e => e.Lines)
            .Where(l => l.AccountId == revenue.Id)
            .Sum(l => l.Credit - l.Debit);
    }
}
