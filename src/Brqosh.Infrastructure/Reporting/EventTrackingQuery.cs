using Brqosh.Application.Reporting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public class EventTrackingQuery(BrqoshDbContext db) : IEventTrackingQuery
{
    public async Task<IReadOnlyList<EventTrackingRow>> ExecuteAsync(
        long companyId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var query = db.StoredDomainEvents.Where(e => e.CompanyId == companyId);

        if (fromUtc is not null)
            query = query.Where(e => e.OccurredAtUtc >= fromUtc.Value);
        if (toUtc is not null)
            query = query.Where(e => e.OccurredAtUtc <= toUtc.Value);

        var rows = await query
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.EventType)
            .ToListAsync(ct);

        return rows.Select(r => new EventTrackingRow(r.EventType, r.Count)).ToList();
    }
}
