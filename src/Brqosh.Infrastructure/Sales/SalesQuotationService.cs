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

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.SalesQuotations.Where(q => q.CompanyId == quotation.CompanyId).Select(q => q.QuotationNumber),
            "QT", quotation.QuotationDate.Year, quotation.CompanyId);

        quotation.MarkSent(number);
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync(ct);
        return quotation;
    }

    /// <summary>‏(ح-١١٥ · ‏`order-to-cash-procure-to-pay-inventory#8`) ‏قبولُ عرضٍ مُرسَل وحفظُه.</summary>
    public Task<SalesQuotation> AcceptAsync(long companyId, long quotationId, CancellationToken ct = default) =>
        TransitionAsync(companyId, quotationId, q => q.Accept(), ct);

    /// <summary>‏(ح-١١٥) ‏رفضُ عرضٍ مُرسَل وحفظُه — والمرفوضُ لا يُحوَّل إلى أمر.</summary>
    public Task<SalesQuotation> RejectAsync(long companyId, long quotationId, CancellationToken ct = default) =>
        TransitionAsync(companyId, quotationId, q => q.Reject(), ct);

    private async Task<SalesQuotation> TransitionAsync(long companyId, long quotationId,
        Action<SalesQuotation> transition, CancellationToken ct)
    {
        Security.SecurityContext.EnsureCanPost();
        var quote = await db.SalesQuotations
            .SingleAsync(q => q.CompanyId == companyId && q.Id == quotationId, ct);
        transition(quote);
        await db.SaveChangesAsync(ct);
        return quote;
    }
}
