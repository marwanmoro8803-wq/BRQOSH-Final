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

/// <summary>
/// (CEEF-229 · م1-١٠ / Z-12) <b>وعاءٌ وضريبةٌ لفئةٍ واحدة — على الحدّ الذي تقرؤه الشاشة.</b>
///
/// <para>التفكيكُ كان محسوباً في <c>VatReturnQuery</c> منذ A-15 <b>ولا يعبر هذا الحدّ</b>:
/// صفرُ قارئٍ لـ<c>SalesByCategory</c> خارج ملفّ الاستعلام. فكلُّ سطحٍ يراه المستخدم —
/// الشاشةُ والمطبوعة — يعرض مجموعاً كلّياً بنسبةٍ مخلوطة.</para>
///
/// <para>وهو نمطُ «الوصلة» الذي أُغلق مرّتين (م2-٢٥/٢٦): <b>الطرفان مقيسان والوسطُ مُستنتَج.</b></para>
/// </summary>
/// <para><b>الرمزُ وحده — لا تسمية.</b> نصوصُ العرض تسكن طبقةَ العرض، وإلا أفلتت من بوّابة
/// الترجمة (تمسح <c>src/Brqosh.Desktop</c> وحدها) فبقيت عربيةً عند التبديل إلى الإنجليزية.</para>
public record VatReturnCategoryDto(string Code, decimal TaxableAmount, decimal VatAmount);

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦٥ · <c>tax-zatca#12</c>) خانةٌ من نموذج الإقرار في بوّابة الهيئة — رقمُها ومبلغُها
/// وتعديلاتُها والضريبةُ لكلٍّ منهما. والعنوانُ من <c>VatReturnBox</c> بالرقم.
/// </summary>
public record VatReturnBoxDto(int Box, decimal Amount, decimal Adjustments, decimal VatAmount, decimal VatAdjustments);

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤٠) <b>صافي الإقرار — مالكٌ واحد.</b>
///
/// <para><b>وكان له مالكان</b>: <c>VatReturnResult</c> (يقرؤه المطبوع) و<c>VatReturnDto</c>
/// (تقرؤه الشاشة)، <b>كلٌّ يشتقّ الصافيَ بنفسه</b>. فأُضيفت تسويةُ الإلغاء (CEEF-219)
/// والضريبةُ المقيّدة بلا مستند (CEEF-232 · ض-١) إلى الأوّل <b>وبقي الثاني على المعادلة
/// الأقدم</b> — فبلغ الإصلاحان المطبوعةَ ولم يبلغا الشاشة. وقِيس: ربعٌ يُقدَّم بـ١٤٬٨٠٠
/// <b>والشاشةُ تقول ١٥٬٠٠٠</b>، وبيعُ أصلٍ ثابتٍ يختفي منها كلُّه.</para>
/// </summary>
public static class VatReturnNet
{
    /// <summary>
    /// المخرجاتُ: المبيعاتُ والمستخلصات، <b>ناقصاً</b> الإشعاراتِ الدائنةَ وتسويةَ إلغاء
    /// مستندات الفترات السابقة، <b>زائداً</b> ما قُيّد على المخرجات بلا مستند.
    /// </summary>
    public static decimal Output(
        decimal outputVat, decimal progressBillingsVat, decimal creditNotesVat,
        decimal cancelledOutputVat, decimal journalOutputVat) =>
        outputVat + progressBillingsVat - creditNotesVat - cancelledOutputVat + journalOutputVat;

    /// <summary>المدخلاتُ القابلةُ للخصم — بالبنية نفسِها.</summary>
    public static decimal Input(
        decimal deductibleInputVat, decimal debitNotesDeductibleVat,
        decimal cancelledInputVat, decimal journalInputVat) =>
        deductibleInputVat - debitNotesDeductibleVat - cancelledInputVat + journalInputVat;
}

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤٠) <b>والحقولُ الأربعة إلزاميّةٌ بلا قيمةٍ افتراضيّة</b> — وموضعُها
/// قبل الاختياريّة عمداً. <b>فالافتراضيُّ صفراً هو بعينه ما أدخل العطل</b>: مُنشِئٌ لا يعرف
/// الحدَّ يمرّر صفراً ولا يشكو أحد. <b>وبغير افتراضيٍّ يُسمّيه كلُّ مُنتِجٍ أو لا يُترجَم.</b>
/// </summary>
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
    decimal CancelledOutputVat,
    decimal CancelledInputVat,
    decimal JournalOutputVat,
    decimal JournalInputVat,
    // (جاهزيّةُ الإطلاق · ح-٤١) ‏الأبُ الذي تُعرض أوعيةُ الفئات تحته — صافي الوعاء من
    // ‏إجماليّات المستندات، **لا من الأوعية نفسِها**؛ فتصير المساواةُ تحته فحصاً لا تحصيلاً.
    decimal SalesBaseNet,
    decimal PurchasesBaseNet,
    decimal? GlVatNetPayableBalance = null,
    IReadOnlyList<VatReturnCategoryDto>? SalesByCategory = null,
    IReadOnlyList<VatReturnCategoryDto>? PurchasesByCategory = null,
    IReadOnlyList<VatReturnBoxDto>? SalesBoxes = null,
    IReadOnlyList<VatReturnBoxDto>? PurchaseBoxes = null,
    decimal JournalOutputVatInBoxes = 0m)
{
    public decimal NetOutputVat => VatReturnNet.Output(
        OutputVat, ProgressBillingsVat, CreditNotesVat, CancelledOutputVat, JournalOutputVat);
    public decimal NetDeductibleInputVat => VatReturnNet.Input(
        DeductibleInputVat, DebitNotesDeductibleVat, CancelledInputVat, JournalInputVat);
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
