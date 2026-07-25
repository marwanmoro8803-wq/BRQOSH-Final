using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public class SalesQuotationService(BrqoshDbContext db)
{
    public async Task<SalesQuotation> CreateAndSendAsync(SalesQuotation quotation, CancellationToken ct = default)
    {
        if (quotation.Lines.Count == 0)
            throw new Domain.Accounting.AccountingException("أضف سطراً واحداً على الأقل.");

        var number = await DocumentNumberGenerator.NextAsync(
            db.SalesQuotations.Where(q => q.CompanyId == quotation.CompanyId).Select(q => q.QuotationNumber),
            "QT", quotation.QuotationDate.Year);

        quotation.MarkSent(number);
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync(ct);
        return quotation;
    }
}
