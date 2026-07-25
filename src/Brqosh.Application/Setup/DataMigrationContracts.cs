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
}
