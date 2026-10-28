using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

/// <summary>
/// ‏حجزُ مخزونٍ لأمر بيعٍ مؤكَّد — يُنشأ عند التأكيد ويُفرَج عنه بالفوترة أو إلغاء الأمر،
/// ‏ويعود إن أُلغيت فاتورةٌ أعادت كمّيتَها إلى الأمر. ‏والمتاحُ للبيع = الرصيد − المحجوز المفتوح.
/// </summary>
public class StockReservation
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long SalesOrderId { get; private set; }
    public long ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ReleasedQuantity { get; private set; }

    public decimal OpenQuantity => Quantity - ReleasedQuantity;

    private StockReservation() { }

    public static StockReservation Create(long companyId, long salesOrderId, long productId, decimal quantity)
    {
        if (salesOrderId <= 0 || productId <= 0)
            throw new AccountingException("حجز المخزون غير صالح.");
        if (quantity <= 0m)
            throw new AccountingException("الكمية يجب أن تكون موجبة.");
        return new StockReservation { CompanyId = companyId, SalesOrderId = salesOrderId, ProductId = productId, Quantity = quantity };
    }

    /// <summary>يُفرج عن كمّيةٍ (لا تتجاوز المفتوح)، ويُعيد ما أُفرج عنه فعلاً.</summary>
    public decimal Release(decimal quantity)
    {
        var released = Math.Min(Math.Max(quantity, 0m), OpenQuantity);
        ReleasedQuantity += released;
        return released;
    }

    /// <summary>يُعيد حجزَ كمّيةٍ سبق الإفراجُ عنها (إلغاءُ فاتورةٍ من الأمر).</summary>
    public void Restore(decimal quantity) =>
        ReleasedQuantity = Math.Max(0m, ReleasedQuantity - Math.Max(quantity, 0m));
}
