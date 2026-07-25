namespace Brqosh.Application.Treasury;

public interface IExchangeRateService
{
    /// <summary>وحدات toCurrency لكل 1 fromCurrency — أحدث سعر بتاريخ ≤ asOfDate.</summary>
    Task<decimal> GetRateAsync(
        long companyId,
        string fromCurrency,
        string toCurrency,
        DateOnly asOfDate,
        CancellationToken ct = default);

    /// <summary>متوسط أسعار الفترة — أو (افتتاح+إقفال)/2 عند نقص البيانات اليومية.</summary>
    Task<decimal> GetAverageRateAsync(
        long companyId,
        string fromCurrency,
        string toCurrency,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct = default);

    Task RecordRateAsync(
        long companyId,
        string fromCurrency,
        string toCurrency,
        DateOnly effectiveDate,
        decimal rate,
        string? source = null,
        CancellationToken ct = default);
}

/// <summary>سعر من مزوّد خارجي — وحدات to لكل 1 from.</summary>
public record FxRateQuote(
    string FromCurrency,
    string ToCurrency,
    DateOnly EffectiveDate,
    decimal Rate,
    string Source);

public interface IFxRateProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<FxRateQuote>> FetchRatesAsync(
        string baseCurrency,
        IReadOnlyList<string> targetCurrencies,
        DateOnly asOfDate,
        CancellationToken ct = default);
}

public record FxRateSyncResult(
    string ProviderId,
    DateOnly AsOfDate,
    int RecordedCount,
    IReadOnlyList<FxRateQuote> Quotes);

public interface IFxRateSyncService
{
    Task<FxRateSyncResult> SyncCompanyRatesAsync(
        long companyId,
        DateOnly? asOfDate = null,
        IReadOnlyList<string>? targetCurrencies = null,
        CancellationToken ct = default);
}

public sealed record BankStatementImportResult(long StatementId, int LineCount);

public sealed record BankStatementMatchResultDto(int MatchedCount, int UnmatchedAfter);

public sealed record BankDifferencePostingResultDto(long JournalEntryId, string EntryNumber, int LineIndex);

public interface IBankStatementDifferencePostingUseCase
{
    Task<BankDifferencePostingResultDto> PostUnmatchedLineAsync(
        long companyId,
        long bankStatementId,
        int lineIndex,
        string? offsetAccountCode = null,
        CancellationToken ct = default);
}

public interface IBankStatementImportUseCase
{
    Task<BankStatementImportResult> ImportCsvAsync(
        long companyId,
        long bankAccountId,
        string sourceFileName,
        string csv,
        CancellationToken ct = default);
}

public interface IBankStatementMatchUseCase
{
    Task<BankStatementMatchResultDto> AutoMatchAsync(
        long companyId,
        long bankStatementId,
        CancellationToken ct = default);
}

public interface ICashMovementReportUseCase
{
    Task<CashMovementSummaryDto> GetBankMovementAsync(
        long companyId,
        long bankAccountId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}

public sealed record CashMovementSummaryDto(
    decimal TotalInflows,
    decimal TotalOutflows,
    decimal NetMovement,
    int TransactionCount);

public sealed record CashFlowForecastWeek(
    int WeekIndex,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    decimal ProjectedInflows,
    decimal ProjectedOutflows,
    decimal NetCashFlow);

public sealed record CashFlowForecastResult(
    DateOnly AsOfDate,
    IReadOnlyList<CashFlowForecastWeek> Weeks,
    decimal TotalProjectedInflows,
    decimal TotalProjectedOutflows);

public interface ICashFlowForecastService
{
    Task<CashFlowForecastResult> ForecastAsync(
        long companyId, DateOnly? asOfDate = null, CancellationToken ct = default);
}

public sealed record PaymentRunLine(
    long SupplierId,
    string SupplierName,
    long InvoiceId,
    string InvoiceNumber,
    DateOnly DueDate,
    decimal AmountDue);

public sealed record PaymentRunDraft(
    DateOnly RunDate,
    IReadOnlyList<PaymentRunLine> Lines,
    decimal TotalAmount);

public interface IPaymentRunService
{
    Task<PaymentRunDraft> CreateDraftAsync(
        long companyId, DateOnly? asOfDate = null, CancellationToken ct = default);
}
