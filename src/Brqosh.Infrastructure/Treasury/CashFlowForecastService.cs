using Brqosh.Application.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public sealed class CashFlowForecastService(BrqoshDbContext db) : ICashFlowForecastService
{
    private const int WeekCount = 13;
    private const int DefaultPaymentTermsDays = 30;

    public async Task<CashFlowForecastResult> ForecastAsync(
        long companyId, DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        var anchor = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);
        var weeks = new List<CashFlowForecastWeek>();

        for (var i = 0; i < WeekCount; i++)
        {
            var start = anchor.AddDays(i * 7);
            var end = start.AddDays(6);
            weeks.Add(new CashFlowForecastWeek(i + 1, start, end, 0m, 0m, 0m));
        }

        var allocation = new PaymentAllocationService(db);
        var customers = await db.Customers.Where(c => c.CompanyId == companyId).Select(c => c.Id).ToListAsync(ct);
        foreach (var customerId in customers)
        {
            foreach (var doc in await allocation.OpenSalesDocumentsAsync(companyId, customerId, ct))
            {
                var dueDate = doc.DocumentDate.AddDays(DefaultPaymentTermsDays);
                AddToWeek(weeks, anchor, dueDate, doc.RemainingAmount, isInflow: true);
            }
        }

        var suppliers = await db.Suppliers.Where(s => s.CompanyId == companyId).Select(s => s.Id).ToListAsync(ct);
        foreach (var supplierId in suppliers)
        {
            foreach (var doc in await allocation.OpenPurchaseDocumentsAsync(companyId, supplierId, ct))
            {
                var dueDate = doc.DocumentDate.AddDays(DefaultPaymentTermsDays);
                AddToWeek(weeks, anchor, dueDate, doc.RemainingAmount, isInflow: false);
            }
        }

        for (var i = 0; i < weeks.Count; i++)
        {
            var w = weeks[i];
            weeks[i] = w with { NetCashFlow = w.ProjectedInflows - w.ProjectedOutflows };
        }

        return new CashFlowForecastResult(
            anchor,
            weeks,
            weeks.Sum(w => w.ProjectedInflows),
            weeks.Sum(w => w.ProjectedOutflows));
    }

    private static void AddToWeek(
        IList<CashFlowForecastWeek> weeks, DateOnly anchor, DateOnly dueDate, decimal amount, bool isInflow)
    {
        if (amount <= 0m)
            return;

        var weekIndex = Math.Max(0, (dueDate.DayNumber - anchor.DayNumber) / 7);
        if (weekIndex >= weeks.Count)
            weekIndex = weeks.Count - 1;

        var week = weeks[weekIndex];
        weeks[weekIndex] = week with
        {
            ProjectedInflows = week.ProjectedInflows + (isInflow ? amount : 0m),
            ProjectedOutflows = week.ProjectedOutflows + (isInflow ? 0m : amount),
        };
    }
}
