namespace Brqosh.Application.Setup;

public sealed record DataMigrationRowResult(int LineNumber, bool Success, string? Error);

public sealed record DataMigrationResult(
    string EntityType,
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<DataMigrationRowResult> Details);

public interface IDataMigrationService
{
    Task<DataMigrationResult> ImportChartOfAccountsCsvAsync(long companyId, string csvContent, CancellationToken ct = default);

    Task<DataMigrationResult> ImportCustomersCsvAsync(long companyId, string csvContent, CancellationToken ct = default);

    /// <summary>‏(ح-١٣٣) الموردون — «NameAr,VatNumber» كالعملاء.</summary>
    Task<DataMigrationResult> ImportSuppliersCsvAsync(long companyId, string csvContent, CancellationToken ct = default);

    /// <summary>
    /// ‏(ح-١٣٣) الميزانُ الافتتاحيّ — «AccountCode,Debit,Credit» بتاريخ بدء التشغيل: <b>قيدٌ واحدٌ مرحَّلٌ أو لا شيء</b>.
    /// </summary>
    Task<DataMigrationResult> ImportOpeningTrialBalanceCsvAsync(
        long companyId, DateOnly openingDate, string csvContent, CancellationToken ct = default);
}
