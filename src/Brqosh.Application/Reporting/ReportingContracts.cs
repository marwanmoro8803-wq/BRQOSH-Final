using Brqosh.Domain.Accounting;

namespace Brqosh.Application.Reporting;

public record TrialBalanceDto(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal TotalDebit,
    decimal TotalCredit)
{
    public decimal BalanceDebit => TotalDebit > TotalCredit ? TotalDebit - TotalCredit : 0m;
    public decimal BalanceCredit => TotalCredit > TotalDebit ? TotalCredit - TotalDebit : 0m;
}

public record ProjectProfitabilityDto(
    long ProjectId,
    string Code,
    string NameAr,
    decimal ContractValue,
    decimal EstimatedTotalCost,
    decimal IncurredCost,
    decimal BilledWork,
    decimal RecognizedRevenueEstimate,
    decimal RetentionHeld,
    decimal GrossMarginEstimate,
    string CompletionMethod);

public interface ITrialBalanceQuery
{
    Task<IReadOnlyList<TrialBalanceDto>> ExecuteAsync(long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public interface IProjectProfitabilityQuery
{
    Task<IReadOnlyList<ProjectProfitabilityDto>> ExecuteAsync(long companyId, CancellationToken ct = default);
}

public record GeneralLedgerLineDto(
    DateOnly EntryDate,
    string EntryNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record GeneralLedgerAccountDto(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    IReadOnlyList<GeneralLedgerLineDto> Lines,
    decimal ClosingBalance);

public interface IGeneralLedgerQuery
{
    Task<IReadOnlyList<GeneralLedgerAccountDto>> ExecuteAsync(
        long companyId,
        DateOnly fromDate,
        DateOnly toDate,
        string? accountCodeFilter = null,
        CancellationToken ct = default);
}

public record BalanceSheetRowDto(string AccountCode, string AccountNameAr, AccountType Type, decimal Amount);

public record BalanceSheetResultDto(
    DateOnly AsOfDate,
    IReadOnlyList<BalanceSheetRowDto> Assets,
    IReadOnlyList<BalanceSheetRowDto> Liabilities,
    IReadOnlyList<BalanceSheetRowDto> Equity)
{
    public decimal TotalAssets => Assets.Sum(a => a.Amount);
    public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
    public decimal TotalEquity => Equity.Sum(e => e.Amount);
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => TotalAssets == TotalLiabilitiesAndEquity;
}

public interface IBalanceSheetQuery
{
    Task<BalanceSheetResultDto> ExecuteAsync(long companyId, DateOnly asOfDate, CancellationToken ct = default);
}

public record IncomeStatementRowDto(string AccountCode, string AccountNameAr, AccountType Type, decimal Amount);

public record IncomeStatementResultDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<IncomeStatementRowDto> RevenueRows,
    IReadOnlyList<IncomeStatementRowDto> ExpenseRows)
{
    public decimal TotalRevenue => RevenueRows.Sum(r => r.Amount);
    public decimal TotalExpenses => ExpenseRows.Sum(r => r.Amount);
    public decimal NetIncome => TotalRevenue - TotalExpenses;
}

public interface IIncomeStatementQuery
{
    Task<IncomeStatementResultDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public record CashFlowLineDto(string Label, decimal Amount);

public record CashFlowSectionDto(string NameAr, IReadOnlyList<CashFlowLineDto> Lines)
{
    public decimal Subtotal => Lines.Sum(l => l.Amount);
}

public record CashFlowStatementResultDto(
    DateOnly FromDate,
    DateOnly ToDate,
    CashFlowSectionDto Operating,
    CashFlowSectionDto Investing,
    CashFlowSectionDto Financing,
    decimal NetCashChange,
    decimal OpeningCash,
    decimal ClosingCash);

public interface ICashFlowStatementQuery
{
    Task<CashFlowStatementResultDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public record VatReturnDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal SalesSubtotal,
    decimal OutputVat,
    decimal ProgressBillingsSubtotal,
    decimal ProgressBillingsVat,
    decimal CreditNotesSubtotal,
    decimal CreditNotesVat,
    decimal PurchasesSubtotal,
    decimal DeductibleInputVat,
    decimal DebitNotesSubtotal,
    decimal DebitNotesVat,
    decimal DebitNotesDeductibleVat,
    decimal? GlVatNetPayableBalance = null)
{
    public decimal NetOutputVat => OutputVat + ProgressBillingsVat - CreditNotesVat;
    public decimal NetDeductibleInputVat => DeductibleInputVat - DebitNotesDeductibleVat;
    public decimal NetVatPayable => NetOutputVat - NetDeductibleInputVat;
    // (CEEF-229 · م2-١٦) null = «لا مطابقة ممكنة» وهو غير الصفر = «طابق تماماً».
    public decimal? GlVariance =>
        GlVatNetPayableBalance is decimal gl ? NetVatPayable - gl : null;
}

public interface IVatReturnQuery
{
    Task<VatReturnDto> ExecuteAsync(long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public record CustomerStatementLineDto(
    DateOnly Date,
    string DocumentNumber,
    string DocumentType,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record CustomerStatementResultDto(
    IReadOnlyList<CustomerStatementLineDto> Lines,
    decimal ClosingBalance);

public interface ICustomerStatementQuery
{
    Task<CustomerStatementResultDto> ExecuteAsync(long companyId, long customerId, CancellationToken ct = default);
}

public record SupplierStatementLineDto(
    DateOnly Date,
    string DocumentNumber,
    string DocumentType,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record SupplierStatementResultDto(
    IReadOnlyList<SupplierStatementLineDto> Lines,
    decimal ClosingBalance);

public interface ISupplierStatementQuery
{
    Task<SupplierStatementResultDto> ExecuteAsync(long companyId, long supplierId, CancellationToken ct = default);
}

public record ConsolidatedTrialBalanceDto(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal TotalDebit,
    decimal TotalCredit,
    int CompanyCount)
{
    public decimal BalanceDebit => TotalDebit > TotalCredit ? TotalDebit - TotalCredit : 0m;
    public decimal BalanceCredit => TotalCredit > TotalDebit ? TotalCredit - TotalDebit : 0m;
}

public interface IConsolidatedTrialBalanceQuery
{
    Task<IReadOnlyList<ConsolidatedTrialBalanceDto>> ExecuteAsync(
        long groupId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default);
}

public record ConsolidatedIncomeStatementDto(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal Amount,
    int CompanyCount);

public record ConsolidatedIncomeStatementResultDto(
    long GroupId,
    string ReportingCurrencyCode,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ConsolidatedIncomeStatementDto> RevenueRows,
    IReadOnlyList<ConsolidatedIncomeStatementDto> ExpenseRows)
{
    public decimal TotalRevenue => RevenueRows.Sum(r => r.Amount);
    public decimal TotalExpenses => ExpenseRows.Sum(r => r.Amount);
    public decimal NetIncome => TotalRevenue - TotalExpenses;
}

public interface IConsolidatedIncomeStatementQuery
{
    Task<ConsolidatedIncomeStatementResultDto> ExecuteAsync(
        long groupId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default);
}

public record ConsolidatedBalanceSheetDto(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal Amount,
    int CompanyCount);

public record ConsolidatedBalanceSheetResultDto(
    long GroupId,
    string ReportingCurrencyCode,
    DateOnly AsOfDate,
    IReadOnlyList<ConsolidatedBalanceSheetDto> Assets,
    IReadOnlyList<ConsolidatedBalanceSheetDto> Liabilities,
    IReadOnlyList<ConsolidatedBalanceSheetDto> Equity)
{
    public decimal TotalAssets => Assets.Sum(a => a.Amount);
    public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
    public decimal TotalEquity => Equity.Sum(e => e.Amount);
    public bool IsBalanced => TotalAssets == TotalLiabilities + TotalEquity;
}

public interface IConsolidatedBalanceSheetQuery
{
    Task<ConsolidatedBalanceSheetResultDto> ExecuteAsync(
        long groupId,
        DateOnly asOfDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default);
}

public record IntercompanyEliminationDto(string AccountCode, decimal EliminatedDebit, decimal EliminatedCredit);

public interface IIntercompanyEliminationQuery
{
    Task<IReadOnlyList<IntercompanyEliminationDto>> ExecuteAsync(
        long groupId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
}
