using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public sealed record CashMovementSummary(
    decimal TotalInflows,
    decimal TotalOutflows,
    decimal NetMovement,
    int TransactionCount);

/// <summary>ملخص حركة نقدية لحساب بنكي خلال فترة — خطوة LIFECYCLE 70.</summary>
public sealed class CashMovementReportService(BrqoshDbContext db)
{
    public async Task<CashMovementSummary> GetBankMovementAsync(
        long companyId,
        long bankAccountId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var lines = await new BankReconciliationQuery(db)
            .GetBookLinesAsync(companyId, bankAccountId, from, to);

        var inflows = lines.Sum(l => l.Debit);
        var outflows = lines.Sum(l => l.Credit);
        return new CashMovementSummary(inflows, outflows, inflows - outflows, lines.Count);
    }

    public async Task<decimal> GetConsolidatedCashPositionAsync(long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        var banks = await db.BankAccounts
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .Select(b => b.Id)
            .ToListAsync(ct);

        decimal total = 0m;
        var recon = new BankReconciliationQuery(db);
        foreach (var bankId in banks)
        {
            var opening = await recon.GetOpeningBalanceAsync(companyId, bankId, asOf.AddDays(1));
            var dayLines = await recon.GetBookLinesAsync(companyId, bankId, asOf, asOf);
            total += opening + dayLines.Sum(l => l.Debit - l.Credit);
        }

        return total;
    }
}
