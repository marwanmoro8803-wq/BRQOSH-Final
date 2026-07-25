using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// تجميع إجماليات مستندات السطور — المسار الوحيد الآمن لـ SQL.
/// يجمع Subtotal + VatAmount المُخزَّنين؛ لا يستخدم GrandTotal المحسوب.
/// </summary>
public static class DocumentTotalsQueryable
{
    public static async Task<decimal> SumGrandTotalAsync<T>(this IQueryable<T> query, CancellationToken ct = default)
        where T : class, ILineDocumentTotals =>
        await query.SumAsync(d => (decimal?)(d.Subtotal + d.VatAmount), ct) ?? 0m;

    public static async Task<decimal> SumVatAsync<T>(this IQueryable<T> query, CancellationToken ct = default)
        where T : class, ILineDocumentTotals =>
        await query.SumAsync(d => (decimal?)d.VatAmount, ct) ?? 0m;

    /// <summary>تجميع في الذاكرة بعد ToList — بديل Sum(x => x.GrandTotal).</summary>
    public static decimal SumGrandTotal<T>(this IEnumerable<T> source)
        where T : ILineDocumentTotals =>
        source.Sum(d => d.Subtotal + d.VatAmount);
}
