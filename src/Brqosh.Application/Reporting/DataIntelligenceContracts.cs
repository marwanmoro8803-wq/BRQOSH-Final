namespace Brqosh.Application.Reporting;

public record DataQualityFinding(string Code, string Severity, string Message);

public record DataQualityReport(long CompanyId, DateTime CheckedAtUtc, IReadOnlyList<DataQualityFinding> Findings);

public record EventTrackingRow(string EventType, int Count);

public record ReportSnapshotDto(
    long Id,
    long CompanyId,
    string ReportType,
    DateOnly AsOfDate,
    DateTime CapturedAtUtc,
    int RowCount,
    string Hash);

public interface IDataQualityService
{
    Task<DataQualityReport> RunAsync(long companyId, CancellationToken ct = default);
}

public interface IReportSnapshotService
{
    Task<ReportSnapshotDto> CaptureTrialBalanceAsync(
        long companyId, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<ReportSnapshotDto>> ListAsync(
        long companyId, string? reportType = null, CancellationToken ct = default);
}

public interface IEventTrackingQuery
{
    Task<IReadOnlyList<EventTrackingRow>> ExecuteAsync(
        long companyId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);
}
