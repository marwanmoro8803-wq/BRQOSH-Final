namespace Brqosh.Application.Reporting;

/// <summary>
/// ‏سطرُ مكوِّنٍ في قائمة التغيّرات في حقوق الملكية (IAS 1 الفقرة 106): رصيدُه أوّلَ الفترة، وربحُ الفترة،
/// والدخلُ الشاملُ الآخر، والمعاملاتُ مع الملّاك، والتحويلاتُ بين المكوّنات — فرصيدُه آخرَها.
/// </summary>
public record EquityComponentMovementDto(
    string AccountCode,
    string AccountNameAr,
    decimal Opening,
    decimal ProfitOrLoss,
    decimal OtherComprehensiveIncome,
    decimal TransactionsWithOwners,
    decimal TransfersWithinEquity)
{
    public decimal Closing => Opening + ProfitOrLoss + OtherComprehensiveIncome + TransactionsWithOwners + TransfersWithinEquity;
}

/// <summary>‏قائمةُ التغيّرات في حقوق الملكية لفترة — وإجماليُّ إقفالها يساوي حقوقَ الملكية في المركز المالي آخرَ الفترة.</summary>
public record StatementOfChangesInEquityDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<EquityComponentMovementDto> Components)
{
    public decimal TotalOpening => Components.Sum(c => c.Opening);
    public decimal TotalProfitOrLoss => Components.Sum(c => c.ProfitOrLoss);
    public decimal TotalOtherComprehensiveIncome => Components.Sum(c => c.OtherComprehensiveIncome);
    public decimal TotalComprehensiveIncome => TotalProfitOrLoss + TotalOtherComprehensiveIncome;
    public decimal TotalTransactionsWithOwners => Components.Sum(c => c.TransactionsWithOwners);
    public decimal TotalTransfersWithinEquity => Components.Sum(c => c.TransfersWithinEquity);
    public decimal TotalClosing => Components.Sum(c => c.Closing);
}

public interface IStatementOfChangesInEquityQuery
{
    Task<StatementOfChangesInEquityDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
