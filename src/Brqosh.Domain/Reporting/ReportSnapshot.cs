using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Domain.Reporting;

/// <summary>لقطة تقرير JSON محفوظة — أساس Data Intelligence دون BI كامل.</summary>
public class ReportSnapshot
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string ReportType { get; private set; }
    public DateOnly AsOfDate { get; private set; }
    public DateTime CapturedAtUtc { get; private set; }
    public string PayloadJson { get; private set; }
    public int RowCount { get; private set; }
    public string Hash { get; private set; }

    private ReportSnapshot()
    {
        ReportType = null!;
        PayloadJson = null!;
        Hash = null!;
    }

    public static ReportSnapshot Create(
        long companyId, string reportType, DateOnly asOfDate, string payloadJson, int rowCount)
    {
        if (string.IsNullOrWhiteSpace(reportType))
            throw new ArgumentException("ReportType required.", nameof(reportType));
        if (reportType.Length > 64)
            throw new ArgumentException("ReportType max 64 chars.", nameof(reportType));
        if (string.IsNullOrEmpty(payloadJson))
            throw new ArgumentException("PayloadJson required.", nameof(payloadJson));

        return new ReportSnapshot
        {
            CompanyId = companyId,
            ReportType = reportType.Trim(),
            AsOfDate = asOfDate,
            CapturedAtUtc = DateTime.UtcNow,
            PayloadJson = payloadJson,
            RowCount = rowCount,
            Hash = ComputeSha256(payloadJson),
        };
    }

    private static string ComputeSha256(string payloadJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
