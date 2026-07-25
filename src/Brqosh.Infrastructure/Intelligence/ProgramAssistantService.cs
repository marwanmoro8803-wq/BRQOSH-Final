using Brqosh.Application.Intelligence;

namespace Brqosh.Infrastructure.Intelligence;

/// <summary>
/// مساعد البرنامج الذكي v2 — يدمج سياق الشاشة + مسارات العمل + KPIs في كل رد.
/// </summary>
public sealed class ProgramAssistantService(
    IFinancialAssistantService financialAssistant) : IProgramAssistantService
{
    public const string EngineId = "brqosh-program-assistant-v2";

    public async Task<FinancialAssistantReply> AskAsync(
        ProgramAssistantContext context,
        string question,
        CancellationToken ct = default)
    {
        var q = question.Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            var welcome = ContextualWelcome(context);
            return ProgramReplyComposer.WrapPageOnly(context, welcome.AnswerAr, welcome.AnswerEn, EngineId);
        }

        var inferredNav = ProgramQuestionMatcher.GuessNavKey(q);
        var workflows = ProgramWorkflowKnowledge.Match(q);
        var lower = q.ToLowerInvariant();
        var topic = Classify(q, lower);

        var core = topic switch
        {
            ProgramTopic.CurrentPage => PageReply(context),
            ProgramTopic.Posting => ProgramKnowledge.PostingGuide(),
            ProgramTopic.Zatca => ProgramKnowledge.ZatcaGuide(),
            ProgramTopic.Permissions => ProgramKnowledge.RbacGuide(context.UserRoleName),
            ProgramTopic.Help => ProgramKnowledge.ProgramHelp(),
            ProgramTopic.Financial => await financialAssistant.AskAsync(context.CompanyId, q, ct),
            ProgramTopic.HowTo => HowToReply(context, inferredNav, workflows),
            _ => await GeneralReplyAsync(context, q, inferredNav, ct),
        };

        return ProgramReplyComposer.ComposeExpert(context, core, inferredNav, workflows, EngineId);
    }

    private static ProgramTopic Classify(string original, string lower)
    {
        if (ContainsAny(lower, original, "هذه الشاشة", "الصفحة", "الشاشة", "أين أنا", "where am i", "this screen", "current page", "ماذا أفعل", "what do i do"))
            return ProgramTopic.CurrentPage;
        if (ContainsAny(lower, original, "كيف", "how to", "how do", "خطوات", "steps", "أنشئ", "انشئ", "create", "أضف", "add new"))
            return ProgramTopic.HowTo;
        if (ContainsAny(lower, original, "ترحيل", "post", "posted", "مسودة", "draft"))
            return ProgramTopic.Posting;
        if (ContainsAny(lower, original, "زاتكا", "zatca", "فاتورة إلكترون", "e-invoice", "csid", "otp"))
            return ProgramTopic.Zatca;
        if (ContainsAny(lower, original, "صلاحية", "permission", "دوري", "role", "rbac", "لماذا مرفوض", "مرفوض"))
            return ProgramTopic.Permissions;
        if (ContainsAny(lower, original, "مساعدة", "help", "ماذا يمكن", "what can"))
            return ProgramTopic.Help;

        if (ContainsAny(lower, original,
                "تدفق", "نقد", "ضريبة", "vat", "ذمم", "عملاء", "مورد", "مبيعات", "مخزون",
                "cash", "receivable", "payable", "sales", "inventory", "anomaly", "تنبيه", "رصيد", "إيراد"))
            return ProgramTopic.Financial;

        return ProgramTopic.General;
    }

    private static bool ContainsAny(string lower, string original, params string[] needles) =>
        needles.Any(n => lower.Contains(n, StringComparison.OrdinalIgnoreCase)
                         || original.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static FinancialAssistantReply PageReply(ProgramAssistantContext context)
    {
        var guide = ProgramKnowledge.TryGetPageGuide(context.NavPageKey)
                    ?? ProgramKnowledge.FallbackPage(context.NavPageKey);
        return new FinancialAssistantReply(
            $"{guide.GuideAr}",
            $"{guide.GuideEn}",
            ProgramKnowledge.SourceId);
    }

    private static FinancialAssistantReply ContextualWelcome(ProgramAssistantContext context)
    {
        var guide = ProgramKnowledge.TryGetPageGuide(context.NavPageKey);
        if (guide is null)
        {
            return new FinancialAssistantReply(
                "اسأل عن الشاشة الحالية، الترحيل، ZATCA، مسارات العمل (مبيعات · مشتريات · خزينة)، أو المؤشرات المالية.",
                "Ask about this screen, posting, ZATCA, workflows (sales · purchasing · treasury), or financial KPIs.",
                EngineId);
        }

        return new FinancialAssistantReply(
            $"{guide.GuideAr}",
            $"{guide.GuideEn}",
            EngineId);
    }

    private static FinancialAssistantReply HowToReply(
        ProgramAssistantContext context,
        string? inferredNav,
        IReadOnlyList<ProgramWorkflowKnowledge.WorkflowHint> workflows)
    {
        var targetKey = inferredNav ?? context.NavPageKey;
        var guide = targetKey is not null
            ? ProgramKnowledge.TryGetPageGuide(targetKey) ?? ProgramKnowledge.FallbackPage(targetKey)
            : null;

        if (guide is not null)
        {
            return new FinancialAssistantReply(
                $"كيفية العمل في {guide.TitleAr}: {guide.GuideAr}",
                $"How to work in {guide.TitleEn}: {guide.GuideEn}",
                ProgramKnowledge.SourceId);
        }

        if (workflows.Count > 0)
        {
            var wf = workflows[0];
            return new FinancialAssistantReply(wf.StepsAr, wf.StepsEn, ProgramKnowledge.SourceId);
        }

        return ProgramKnowledge.ProgramHelp();
    }

    private async Task<FinancialAssistantReply> GeneralReplyAsync(
        ProgramAssistantContext context,
        string question,
        string? inferredNav,
        CancellationToken ct)
    {
        if (inferredNav is not null)
        {
            var guide = ProgramKnowledge.TryGetPageGuide(inferredNav);
            if (guide is not null)
            {
                return new FinancialAssistantReply(guide.GuideAr, guide.GuideEn, ProgramKnowledge.SourceId);
            }
        }

        return await financialAssistant.AskAsync(context.CompanyId, question, ct);
    }

    private enum ProgramTopic
    {
        General,
        CurrentPage,
        HowTo,
        Posting,
        Zatca,
        Permissions,
        Help,
        Financial,
    }
}
