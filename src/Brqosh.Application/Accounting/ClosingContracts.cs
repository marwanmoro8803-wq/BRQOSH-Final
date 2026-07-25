namespace Brqosh.Application.Accounting;

public sealed record MonthEndCloseResult(
    string PeriodCode,
    bool TrialBalanceBalanced,
    decimal TrialBalanceDifference,
    int UnreconciledBankLineCount,
    bool PeriodClosed);

public interface IMonthEndCloseUseCase
{
    Task<MonthEndCloseResult> ClosePeriodAsync(
        long companyId,
        long periodId,
        string username,
        CancellationToken ct = default);
}
