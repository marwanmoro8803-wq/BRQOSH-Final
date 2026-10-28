using Brqosh.Domain.Inventory;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;

namespace Brqosh.Application.Accounting;

// ‏عقودُ خدمات الترحيل الرئيسة — في Application والتنفيذُ في Infrastructure، وتُسجَّل في DI.
// ‏فتطلبها الواجهةُ بالعقد بدل `new XPostingService(db)` (تحويلُ الواجهة لـBRQOSH UI).

public interface ISalesInvoicePostingService
{
    Task<SalesInvoice> PostAsync(SalesInvoice invoice);
}

public interface ICreditNotePostingService
{
    Task<CreditNote> PostAsync(CreditNote note);
}

public interface IPurchaseInvoicePostingService
{
    Task<PurchaseInvoice> PostAsync(PurchaseInvoice invoice);
}

public interface IDebitNotePostingService
{
    Task<DebitNote> PostAsync(DebitNote note, CancellationToken ct = default, bool acceptBelowCost = false);
}

public interface IGoodsReceiptPostingService
{
    Task<GoodsReceipt> PostAsync(GoodsReceipt receipt, CancellationToken ct = default);
}

public interface IReceiptVoucherPostingService
{
    Task<ReceiptVoucher> PostAsync(ReceiptVoucher voucher);
}

public interface IPaymentVoucherPostingService
{
    Task<PaymentVoucher> PostAsync(PaymentVoucher voucher);
}

public interface IStockMovementPostingService
{
    Task<StockMovement> PostAsync(StockMovement movement);
}

public interface IDeliveryNoteService
{
    Task<DeliveryNote> DeliverFromOrderAsync(long companyId, long orderId, DateOnly deliveryDate,
        IReadOnlyDictionary<int, decimal>? quantities = null, long? warehouseId = null,
        IReadOnlyDictionary<int, string>? batchNumbers = null, CancellationToken ct = default);

    Task<DeliveryNote> CancelAsync(long companyId, long deliveryNoteId, DateOnly cancellationDate, CancellationToken ct = default);
}
