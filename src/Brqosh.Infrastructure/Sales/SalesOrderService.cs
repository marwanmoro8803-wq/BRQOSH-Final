using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public class SalesOrderService(BrqoshDbContext db)
{
    public async Task<SalesOrder> ConfirmAsync(SalesOrder order, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        if (order.Lines.Count == 0)
            throw new Domain.Accounting.AccountingException("أضف سطراً واحداً على الأقل.");

        var number = await DocumentNumberGenerator.NextAsync(db, 
            db.SalesOrders.Where(o => o.CompanyId == order.CompanyId).Select(o => o.OrderNumber),
            "SO", order.OrderDate.Year, order.CompanyId);

        order.Confirm(number);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<SalesInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        var order = await db.SalesOrders
            .SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, ct);

        if (order.Status != SalesOrderStatus.Confirmed)
            throw new Domain.Accounting.AccountingException("يُنشأ الفاتورة من أمر بيع مؤكد فقط.");

        var invoice = SalesInvoice.CreateDraft(companyId, order.CustomerId, order.OrderDate);
        // ‏(م-٥ · ن-٥٢) الثلاثيُّ الضريبيُّ يعبر التحويل — انظر `PurchaseOrderService`.
        foreach (var line in order.Lines)
            invoice.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

        await new SalesInvoicePostingService(db).PostAsync(invoice);
        order.MarkInvoiced(invoice.Id);
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<SalesOrder> CreateFromQuotationAsync(long companyId, long quotationId, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        var quote = await db.SalesQuotations
            .SingleAsync(q => q.CompanyId == companyId && q.Id == quotationId, ct);

        if (quote.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Accepted))
            throw new Domain.Accounting.AccountingException("يُحوَّل عرض سعر مُرسَل أو مقبول فقط.");

        var order = SalesOrder.CreateDraft(companyId, quote.CustomerId, DateOnly.FromDateTime(DateTime.Today), quotationId);
        // ‏(م-٥ · ن-٥٢) **وهذا الموضعُ صار حيّاً بهذا التغيير نفسِه**: عرضُ السعر لم يكن
        // ‏يقبل فئةً ضريبيّةً أصلاً، فلم يكن ثَمّ ما يسقط. ومتى قبِلها وجب أن تعبر — وإلّا
        // ‏خُرق مبدأُ سجلّ السطر: «ولا تُتاح إلا حيث تُحوَّل».
        foreach (var line in quote.Lines)
            order.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

        return await ConfirmAsync(order, ct);
    }
}
