using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

/// <summary>
/// ‏كمّيةٌ فُوترت من سطرٍ في أمر البيع بفاتورةٍ بعينها — مصدرُ «المفوتر» و«المتبقّي» لكلّ سطر،
/// ‏فيُفوتر الأمرُ على دفعات. ‏وإلغاءُ الفاتورة يُعلِّم سطورَها ملغاةً (لا حذف) فتعود كمّيتُها متبقّية.
/// </summary>
public class SalesOrderInvoiceLine
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long SalesOrderId { get; private set; }
    public long SalesInvoiceId { get; private set; }
    public int LineIndex { get; private set; }
    public decimal Quantity { get; private set; }
    public bool IsCancelled { get; private set; }

    private SalesOrderInvoiceLine() { }

    public static SalesOrderInvoiceLine Create(long companyId, long salesOrderId, long salesInvoiceId, int lineIndex, decimal quantity)
    {
        if (salesOrderId <= 0 || salesInvoiceId <= 0 || lineIndex < 0)
            throw new AccountingException("ربط سطر أمر البيع بالفاتورة غير صالح.");
        if (quantity <= 0m)
            throw new AccountingException("الكمية يجب أن تكون موجبة.");
        return new SalesOrderInvoiceLine
        {
            CompanyId = companyId, SalesOrderId = salesOrderId, SalesInvoiceId = salesInvoiceId,
            LineIndex = lineIndex, Quantity = quantity,
        };
    }

    public void MarkCancelled() => IsCancelled = true;
}
