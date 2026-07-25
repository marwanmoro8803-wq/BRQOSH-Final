namespace Brqosh.Application.Intelligence;

/// <summary>تنبيه قرار مالي — Decision Support بقواعد؛ ليس نموذجاً لغوياً حياً.</summary>
public record FinancialInsight(
    string Code,
    string Category,
    string Severity,
    string TitleAr,
    string DetailAr,
    decimal? Metric = null);

public record FinancialIntelligenceReport(
    long CompanyId,
    DateTime GeneratedAtUtc,
    string Engine,
    IReadOnlyList<FinancialInsight> Insights);

public interface IFinancialIntelligenceService
{
    Task<FinancialIntelligenceReport> AnalyzeAsync(
        long companyId, DateOnly? asOf = null, CancellationToken ct = default);
}
