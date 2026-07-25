namespace Brqosh.Application.Reporting;

/// <summary>
/// نتيجة فحص جودة بيانات. (O-26) الرسالة **ثنائية إلزامياً**: كانت إنجليزية وحيدة
/// فتظهر بالإنجليزية داخل واجهة عربية بالكامل — وهي ظاهرة في لوحة التحكم
/// («جودة بيانات: BRANCH_HQ_MISSING · Company has no active headquarters branch»).
/// بلا قيمة افتراضية: حقلٌ إلزاميّ لا يُنسى، والمترجم هو الحارس.
/// </summary>
public record DataQualityFinding(string Code, string Severity, string MessageAr, string MessageEn);

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
