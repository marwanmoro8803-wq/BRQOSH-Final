using Brqosh.Domain.Sales;

namespace Brqosh.Application.Sales;

/// <summary>
/// ‏حالاتُ استخدام أمر البيع — العقدُ في Application والتنفيذُ في Infrastructure، فتعتمد الواجهةُ على
/// ‏العقد عبر حقن التبعيات بدل <c>new SalesOrderService(db)</c>. ‏(أوّلُ شريحةٍ من نقل حالات الاستخدام.)
/// </summary>
public interface ISalesOrderService
{
    Task<SalesOrder> ConfirmAsync(SalesOrder order, CancellationToken ct = default);

    Task<SalesOrder> CreateFromQuotationAsync(long companyId, long quotationId,
        DateOnly? orderDate = null, CancellationToken ct = default);

    Task<SalesInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId,
        DateOnly invoiceDate, long? warehouseId = null, CancellationToken ct = default);

    /// <summary>‏فاتورةٌ بجزءٍ من الأمر — كمّيةٌ لكلّ رقم سطر، و<c>null</c> = كلُّ المتبقّي؛ و<paramref name="postNow"/> = false مسودة.</summary>
    Task<SalesInvoice> CreatePartialInvoiceFromOrderAsync(long companyId, long orderId,
        DateOnly invoiceDate, IReadOnlyDictionary<int, decimal>? quantities, long? warehouseId = null,
        CancellationToken ct = default, bool postNow = true);

    Task CancelAsync(long companyId, long orderId, CancellationToken ct = default);

    Task<decimal> ReservedQuantityAsync(long companyId, long productId, CancellationToken ct = default);

    Task<decimal> AvailableToSellAsync(long companyId, long productId, CancellationToken ct = default);

    Task<int> ExpireOverdueQuotationsAsync(long companyId, DateOnly asOf, CancellationToken ct = default);
}
