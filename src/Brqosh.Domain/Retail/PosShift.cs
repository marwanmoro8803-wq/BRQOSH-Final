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
    public int TransactionCount { get; private set; }
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

    public static PosShift Open(long companyId, string cashierName, decimal openingCash = 0m)
    {
        if (string.IsNullOrWhiteSpace(cashierName))
            throw new AccountingException("اسم الكاشير مطلوب.");
        if (openingCash < 0)
            throw new AccountingException("النقد الافتتاحي لا يكون سالباً.");

        return new PosShift(companyId, cashierName.Trim(),
            Math.Round(openingCash, 2, MidpointRounding.AwayFromZero));
    }

    public void RecordSale(decimal amount)
    {
        if (Status != PosShiftStatus.Open)
            throw new AccountingException("الوردية مغلقة.");
        TotalSales += Math.Round(amount, 2, MidpointRounding.AwayFromZero);
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

    public decimal ExpectedCash => OpeningCash + TotalSales;
    public decimal CashDifference => ClosingCash - ExpectedCash;
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
