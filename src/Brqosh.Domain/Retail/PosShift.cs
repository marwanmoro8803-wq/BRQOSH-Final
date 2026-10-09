using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Retail;

public enum PosShiftStatus
{
    Open = 0,
    Closed = 1,
}

/// <summary>وردية نقطة بيع — تفتح وتُغلق مع عد نقدي.</summary>
public class PosShift : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string CashierName { get; private set; }
    public DateTime OpenedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public decimal OpeningCash { get; private set; }
    public decimal ClosingCash { get; private set; }
    public decimal TotalSales { get; private set; }

    /// <summary>
    /// (CEEF-228) المبيعات النقدية وحدها — وهي ما يُتوقَّع وجوده في الدرج.
    /// كانت `ExpectedCash` تُحسب من `TotalSales` الذي يضمّ الشبكة والدفع المختلط،
    /// فوردية بمبيعات شبكة 10,000 تُظهر عجزاً وهمياً 10,000 عند كل إغلاق.
    /// </summary>
    public decimal CashSales { get; private set; }

    public int TransactionCount { get; private set; }

    /// <summary>(ح-١١٤ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>مستودعُ بيع الوردية</b> — فارغُه المستودعُ الافتراضيّ كما كانت كلُّ وردية.</summary>
    public long? WarehouseId { get; private set; }
    public PosShiftStatus Status { get; private set; } = PosShiftStatus.Open;

    private PosShift()
    {
        CashierName = null!;
    }

    private PosShift(long companyId, string cashierName, decimal openingCash)
    {
        CompanyId = companyId;
        CashierName = cashierName;
        OpeningCash = openingCash;
        OpenedAtUtc = DateTime.UtcNow;
    }

    public static PosShift Open(long companyId, string cashierName, decimal openingCash = 0m, long? warehouseId = null)
    {
        if (warehouseId is <= 0)
            throw new AccountingException("المستودع غير صالح.");
        if (string.IsNullOrWhiteSpace(cashierName))
            throw new AccountingException("اسم الكاشير مطلوب.");
        if (openingCash < 0)
            throw new AccountingException("النقد الافتتاحي لا يكون سالباً.");

        return new PosShift(companyId, cashierName.Trim(),
            Math.Round(openingCash, 2, MidpointRounding.AwayFromZero)) { WarehouseId = warehouseId };
    }

    /// <summary>
    /// طريقة الدفع **إلزامية بلا قيمة افتراضية** عمداً — فالمترجم هو الحارس الذي يمنع
    /// إضافة مسار بيع جديد ينسى تصنيف النقد، وهو أقوى من أيّ مراجعة.
    /// </summary>
    public void RecordSale(decimal amount, PosPaymentMethod paymentMethod)
    {
        if (Status != PosShiftStatus.Open)
            throw new AccountingException("الوردية مغلقة.");

        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        TotalSales += rounded;
        if (paymentMethod == PosPaymentMethod.Cash)
            CashSales += rounded;
        TransactionCount++;
    }

    public void Close(decimal closingCash)
    {
        if (Status != PosShiftStatus.Open)
            throw new AccountingException("الوردية مغلقة مسبقاً.");
        ClosingCash = Math.Round(closingCash, 2, MidpointRounding.AwayFromZero);
        ClosedAtUtc = DateTime.UtcNow;
        Status = PosShiftStatus.Closed;
    }

    public decimal ExpectedCash => OpeningCash + CashSales;
    public decimal CashDifference => ClosingCash - ExpectedCash;
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
