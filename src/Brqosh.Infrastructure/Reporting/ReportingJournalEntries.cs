using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// قيود قابلة للتقرير: المرحّلة + المعكوسة (بتاريخها الأصلي).
/// استبعاد المعكوسة كان يشوّه التقارير التاريخية بعد الإلغاء لاحقاً (ACC-001).
/// </summary>
public static class ReportingJournalEntries
{
    public static IQueryable<JournalEntry> WhereReportable(
        this IQueryable<JournalEntry> query, long companyId, DateOnly? from = null, DateOnly? to = null)
    {
        query = query.Where(e => e.CompanyId == companyId
                                 && (e.Status == JournalEntryStatus.Posted
                                     || e.Status == JournalEntryStatus.Reversed));
        if (from is DateOnly f)
            query = query.Where(e => e.EntryDate >= f);
        if (to is DateOnly t)
            query = query.Where(e => e.EntryDate <= t);
        return query;
    }
}
