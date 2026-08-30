using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

public class PurchaseOrderService(BrqoshDbContext db)
{
    /// <summary>
    /// (CEEF-229 · م1-١٤) داخل معاملة — فقفلُ التسلسل مربوطٌ بها، وخارجَها يُحرَّر فوراً
    /// فيقرأ متزاحمان الرقمَ نفسَه. وكانت القراءةُ والكتابة هنا بلا معاملةٍ إطلاقاً.
    /// </summary>
    public Task<PurchaseOrder> ApproveAsync(PurchaseOrder order)
    {
        SecurityContext.EnsureCanPost();
        return UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            var orderNumber = await DocumentNumberGenerator.NextAsync(
                db, db.PurchaseOrders.Where(o => o.CompanyId == order.CompanyId).Select(o => o.OrderNumber),
                "PO", order.OrderDate.Year, order.CompanyId);

            order.Approve(orderNumber);
            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync(ct);
            return order;
        });
    }

    public async Task<PurchaseInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId,
        string supplierInvoiceReference, DateOnly invoiceDate, bool vatDeductible) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            SecurityContext.EnsureCanPost();
            var order = await db.PurchaseOrders
                .SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, ct);

            if (order.Status != PurchaseOrderStatus.Approved)
                throw new Domain.Accounting.AccountingException("يُنشأ الفاتورة من أمر شراء معتمد فقط.");

            var hasGrn = await db.GoodsReceipts.AnyAsync(g =>
                g.CompanyId == companyId
                && g.PurchaseOrderId == orderId
                && g.Status == GoodsReceiptStatus.Posted, ct);
            if (!hasGrn)
            {
                var warehouse = await new InventoryService(db).GetDefaultWarehouseAsync(companyId);
                var receipt = GoodsReceipt.CreateDraft(
                    companyId, order.SupplierId, orderId, warehouse.Id, invoiceDate);
                foreach (var line in order.Lines)
                    receipt.AddLine(line.Description, line.Quantity, line.Quantity, line.UnitPrice, line.ProductId);
                await new GoodsReceiptPostingService(db).PostAsync(receipt, ct);
            }

            var invoice = PurchaseInvoice.CreateDraft(companyId, order.SupplierId, invoiceDate,
                supplierInvoiceReference, vatDeductible);

            foreach (var line in order.Lines)
                invoice.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId);

            invoice.LinkPurchaseOrder(orderId);
            await new PurchaseInvoicePostingService(db).PostAsync(invoice);
            order.MarkFulfilled();
            await db.SaveChangesAsync(ct);
            return invoice;
        });

    public async Task CancelAsync(long companyId, long orderId, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        var order = await db.PurchaseOrders
            .SingleAsync(o => o.CompanyId == companyId && o.Id == orderId, ct);
        order.Cancel();
        await db.SaveChangesAsync(ct);
    }
}
