using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

public record ProjectProfitabilityRow(
    long ProjectId,
    string Code,
    string NameAr,
    decimal ContractValue,
    decimal EstimatedTotalCost,
    decimal IncurredCost,
    decimal BilledWork,
    decimal RecognizedRevenueEstimate,
    decimal RetentionHeld,
    decimal GrossMarginEstimate,
    string CompletionMethod);

/// <summary>تقرير ربحية مشروع — عقد / تكاليف / مفوتر / تقدير هامش.</summary>
public class ProjectProfitabilityQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<ProjectProfitabilityRow>> ExecuteAsync(long companyId, CancellationToken ct = default)
    {
        Brqosh.Infrastructure.Licensing.LicenseGate.Ensure(Brqosh.Application.Licensing.LicensedCapability.ProjectsContracts);
        var projects = await db.ConstructionProjects
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync(ct);

        var result = new List<ProjectProfitabilityRow>();
        foreach (var p in projects)
        {
            var incurred = await db.ProjectCostEntries
                .Where(c => c.CompanyId == companyId && c.ProjectId == p.Id && !c.IsReversed)
                .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

            var billings = await db.ProgressBillings
                .Where(b => b.CompanyId == companyId && b.ProjectId == p.Id && b.Status == ProgressBillingStatus.Posted)
                .ToListAsync(ct);
            var billed = billings.Sum(b => b.CurrentWorkAmount);
            var retention = billings.Sum(b => b.RetentionAmount);

            string method;
            decimal revenueEst;
            if (p.EstimatedTotalCost > 0 && incurred > 0)
            {
                var pct = Math.Min(100m, Math.Round(incurred / p.EstimatedTotalCost * 100m, 4, MidpointRounding.AwayFromZero));
                revenueEst = Math.Round(p.ContractValue * pct / 100m, 2, MidpointRounding.AwayFromZero);
                method = "cost-to-cost";
            }
            else
            {
                revenueEst = billed;
                method = "billing-based";
            }

            var margin = Math.Round(revenueEst - incurred, 2, MidpointRounding.AwayFromZero);
            result.Add(new ProjectProfitabilityRow(
                p.Id, p.Code, p.NameAr, p.ContractValue, p.EstimatedTotalCost,
                incurred, billed, revenueEst, retention, margin, method));
        }

        return result;
    }
}
