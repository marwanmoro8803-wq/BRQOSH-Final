using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Retail;

public class PosReportQuery(BrqoshDbContext db)
{
    public record DailySaleRow(string Date, int Transactions, decimal TotalSales, decimal VatAmount);

    public async Task<IReadOnlyList<DailySaleRow>> DailySummaryAsync(long companyId, int days = 30,
        CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.InvoiceDate >= from && i.Status == SalesInvoiceStatus.Posted)
            .ToListAsync(ct);

        return invoices
            .GroupBy(i => i.InvoiceDate)
            .OrderByDescending(g => g.Key)
            .Select(g => new DailySaleRow(
                g.Key.ToString("yyyy-MM-dd"),
                g.Count(),
                g.Sum(i => i.GrandTotal()),
                g.Sum(i => i.VatAmount)))
            .ToList();
    }
}
