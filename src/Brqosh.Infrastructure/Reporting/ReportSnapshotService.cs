using System.Text.Json;
using Brqosh.Application.Reporting;
using Brqosh.Domain.Reporting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public class ReportSnapshotService(BrqoshDbContext db) : IReportSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task<ReportSnapshotDto> CaptureTrialBalanceAsync(
        long companyId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // ‏(م-٤ · ن-١٦) ‏**طبقةٌ ثانيةٌ في الخدمة — والأولى في المسار.**
        //
        // ‏الواجهةُ ترفض بـ403 قبل بلوغ هذا السطر، **وهذا يُغطّي ما لا مسارَ له**: سطحُ
        // ‏المكتب يستدعي هذه الدالّةَ عبر `FxRateSyncService`، ومن يُضيف مستدعياً غداً
        // ‏لا يمرّ بحارس الواجهة. **وعرفُ المستودع أن تفحص الخدمةُ بنفسها** — يُطبَّق في
        // ‏`FxClosingTranslationService.TranslatePeriodAsync` — وكان غائباً هنا كلّيّاً.
        SecurityContext.EnsureCanPostAccounting();

        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, from, to);
        var payloadJson = JsonSerializer.Serialize(rows, JsonOptions);
        var snapshot = ReportSnapshot.Create(companyId, "TrialBalance", to, payloadJson, rows.Count);

        db.ReportSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);

        return ToDto(snapshot);
    }

    public async Task<IReadOnlyList<ReportSnapshotDto>> ListAsync(
        long companyId, string? reportType = null, CancellationToken ct = default)
    {
        var query = db.ReportSnapshots.Where(s => s.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(reportType))
            query = query.Where(s => s.ReportType == reportType);

        var rows = await query
            .OrderByDescending(s => s.CapturedAtUtc)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    private static ReportSnapshotDto ToDto(ReportSnapshot s) =>
        new(s.Id, s.CompanyId, s.ReportType, s.AsOfDate, s.CapturedAtUtc, s.RowCount, s.Hash);
}
