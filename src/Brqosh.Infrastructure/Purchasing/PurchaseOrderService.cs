using Brqosh.Domain.Inventory;
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
                "PO", order.OrderDate, order.CompanyId);

            order.Approve(orderNumber);
            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync(ct);
            return order;
        });
    }

    public async Task<PurchaseInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId,
        string supplierInvoiceReference, DateOnly invoiceDate, bool vatDeductible, long? warehouseId = null) =>
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
                // ‏(جاهزيّةُ الإطلاق · ح-١٠٦ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**الاستلامُ الآليُّ لا يخترع دفعة.** سطرُ
                // ‏أمر الشراء لا يحمل رقمَ دفعةٍ ولا انتهاء، والمَخنَقُ كان يرفض المتتبَّعَ «بلا هويّة» دون أن يقول ما العمل —
                // ‏فيُرفض هنا باسم الصنف ويُدَلّ على إذن الاستلام، وهو يُسجّل الدفعةَ وانتهاءها ثمّ تُنشأ الفاتورة منه.
                var products = await new InventoryService(db).LoadProductsForLinesAsync(
                    companyId, order.Lines.Select(l => l.ProductId));
                var tracked = products.Values.FirstOrDefault(p => p.IsStockable() && p.IsTracked);
                if (tracked is not null)
                    throw new Domain.Accounting.AccountingException(
                        $"الصنف '{tracked.NameAr}' يُتتبَّع بالدفعة — استلِمه بإذن استلامٍ يُسجّل دفعتَه وانتهاءها، ثمّ أنشئ الفاتورة من الأمر.");

                // ‏(جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**الاستلامُ الآليُّ يدخل المستودعَ المختار** — لا الافتراضيَّ أبداً؛ والموقوفُ يُرفض باسمه.
                var warehouse = await PurchaseInvoicePostingService.ResolveWarehouseAsync(
                    db, new InventoryService(db), companyId, warehouseId, ct);
                var receipt = GoodsReceipt.CreateDraft(
                    companyId, order.SupplierId, orderId, warehouse.Id, invoiceDate);
                foreach (var line in order.Lines)
                    receipt.AddLine(line.Description, line.Quantity, line.Quantity, line.UnitPrice, line.ProductId);
                await new GoodsReceiptPostingService(db).PostAsync(receipt, ct);
            }

            var invoice = PurchaseInvoice.CreateDraft(companyId, order.SupplierId, invoiceDate,
                supplierInvoiceReference, vatDeductible);

            // ‏(م-٥ · ن-٥٢) **الثلاثيُّ الضريبيُّ يعبر التحويل.** كان يُمرَّر أربعةُ وسائطَ من
            // ‏سبعة، فيُعاد تصنيفُ سطرٍ معفًى «أساسيّاً» صامتاً — **وشاشةُ أمر الشراء تعرض
            // ‏العمودَ فعلاً** (`PurchaseOrderDialog.xaml`، ويُثبّته `VatCategoryUiTests`).
            // ‏وأثرُه مقيس: ضريبةُ مدخلاتٍ ٣٠٠ حيث قال الأمرُ ١٥٠، ووعاءُ المعفى يختفي من
            // ‏«المشتريات بالفئة» في الإقرار. والاصطلاحُ الصحيحُ قائمٌ في
            // ‏`RecurringInvoiceService` منذ CEEF-230.
            foreach (var line in order.Lines)
                invoice.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                    line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

            invoice.LinkPurchaseOrder(orderId);
            invoice.SetWarehouse(warehouseId);
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
