namespace Brqosh.Application.Intelligence;

/// <summary>رد المساعد المالي — قواعد شفافة؛ ليس LLM خارجياً.</summary>
public record FinancialAssistantReply(
    string AnswerAr,
    string AnswerEn,
    string Source);

public interface IFinancialAssistantService
{
    Task<FinancialAssistantReply> AskAsync(
        long companyId, string question, CancellationToken ct = default);
}
