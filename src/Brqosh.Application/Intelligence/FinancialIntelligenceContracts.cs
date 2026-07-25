namespace Brqosh.Application.Intelligence;

/// <summary>
/// تنبيه قرار مالي — Decision Support بقواعد؛ ليس نموذجاً لغوياً حياً.
///
/// (O-26) العنوان والتفصيل **ثنائيان إلزامياً**. كان الحقلان عربيَّين وحدهما، وكل
/// ردٍّ في <c>FinancialAssistantService</c> يضمّ نصّ رؤية كان يصير عربياً **حتى في
/// فرعه الإنجليزيّ** — بل مرّر بعضها النصّ العربيّ نفسه إلى الحقلين. لا قيمة
/// افتراضية هنا: حقلٌ إلزاميّ لا يُنسى، والمترجم هو الحارس.
/// </summary>
public record FinancialInsight(
    string Code,
    string Category,
    string Severity,
    string TitleAr,
    string DetailAr,
    string TitleEn,
    string DetailEn,
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
