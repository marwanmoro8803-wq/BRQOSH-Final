using Brqosh.Application.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public sealed class PaymentRunService(BrqoshDbContext db) : IPaymentRunService
{
    private const int DefaultPaymentTermsDays = 30;

    public async Task<PaymentRunDraft> CreateDraftAsync(
        long companyId, DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        var runDate = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);
        var allocation = new PaymentAllocationService(db);
        var lines = new List<PaymentRunLine>();

        var suppliers = await db.Suppliers
            .Where(s => s.CompanyId == companyId)
            .Select(s => new { s.Id, s.NameAr })
            .ToListAsync(ct);

        foreach (var supplier in suppliers)
        {
            foreach (var doc in await allocation.OpenPurchaseDocumentsAsync(companyId, supplier.Id, ct))
            {
                var dueDate = doc.DocumentDate.AddDays(DefaultPaymentTermsDays);
                if (dueDate > runDate)
                    continue;

                lines.Add(new PaymentRunLine(
                    supplier.Id,
                    supplier.NameAr,
                    doc.DocumentId,
                    doc.DocumentNumber,
                    dueDate,
                    doc.RemainingAmount));
            }
        }

        return new PaymentRunDraft(
            runDate,
            lines.OrderBy(l => l.DueDate).ThenBy(l => l.SupplierName).ToList(),
            lines.Sum(l => l.AmountDue));
    }
}
