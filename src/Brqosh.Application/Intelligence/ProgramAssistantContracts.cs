namespace Brqosh.Application.Intelligence;

/// <summary>سياق الجلسة للمساعد — يربط الرد بالصفحة والدور الفعلي.</summary>
public sealed record ProgramAssistantContext(
    long CompanyId,
    string? NavPageKey,
    string? UserRoleName,
    string? BusinessProfileName);

public interface IProgramAssistantService
{
    Task<FinancialAssistantReply> AskAsync(
        ProgramAssistantContext context,
        string question,
        CancellationToken ct = default);
}
