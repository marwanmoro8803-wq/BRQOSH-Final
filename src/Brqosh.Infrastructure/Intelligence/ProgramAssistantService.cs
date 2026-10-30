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
        var pageHowTo = inferredNav is not null &&
                        ContainsAny(lower, q, "كيف أعمل في", "how do i work in");
        var topic = pageHowTo ? ProgramTopic.HowTo : Classify(q, lower);

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
        // Audit fix: specific topics (ZATCA, permissions, posting verbs) are checked
        // before the generic "how to" bucket. Previously the panel's own quick prompts
        // "كيف أرحّل؟" / "How to post?" fell into HowTo and answered with the current
        // page guide, and "صلاحياتي" matched nothing and was sent to the financial engine.
        if (ContainsAny(lower, original, "هذه الشاشة", "الصفحة", "الشاشة", "أين أنا", "where am i", "this screen", "current page", "ماذا أفعل", "what do i do"))
            return ProgramTopic.CurrentPage;
        if (ContainsAny(lower, original, "زاتكا", "zatca", "فاتورة إلكترون", "e-invoice", "csid", "otp"))
            return ProgramTopic.Zatca;
        if (ContainsAny(lower, original, "صلاحي", "permission", "دوري", "role", "rbac", "لماذا مرفوض", "مرفوض"))
            return ProgramTopic.Permissions;
        if (ContainsAny(lower, original, "ترحيل", "أرحل", "ارحل", "نرحل", "يرحل", "مرحل", "post"))
            return ProgramTopic.Posting;
        if (ContainsAny(lower, original, "كيف", "how to", "how do", "خطوات", "steps", "أنشئ", "انشئ", "create", "أضف", "add new"))
            return ProgramTopic.HowTo;
        if (ContainsAny(lower, original, "مسودة", "draft"))
            return ProgramTopic.Posting;
        if (ContainsAny(lower, original, "مساعدة", "help", "ماذا يمكن", "what can"))
            return ProgramTopic.Help;

        if (ContainsAny(lower, original,
                "تدفق", "نقد", "ضريبة", "vat", "ذمم", "عملاء", "مورد", "مبيعات", "مخزون",
                "cash", "receivable", "payable", "sales", "inventory", "anomaly", "تنبيه", "رصيد", "إيراد"))
            return ProgramTopic.Financial;

        return ProgramTopic.General;
    }

    /// <summary>
    /// Arabic needles are matched after removing diacritics/tatweel and unifying alef forms
    /// (so "أرحّل" matches "ارحل"); Latin needles must start at a word boundary
    /// (so "vat" no longer matches inside "private", nor "post" inside "compost").
    /// </summary>
    private static bool ContainsAny(string lower, string original, params string[] needles)
    {
        var text = NormalizeArabic(lower);
        foreach (var n in needles)
        {
            var needle = NormalizeArabic(n.ToLowerInvariant());
            if (needle.All(c => c < 128))
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        text, @"(?<![a-z0-9])" + System.Text.RegularExpressions.Regex.Escape(needle)))
                    return true;
            }
            else if (text.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        _ = original;
        return false;
    }

    private static string NormalizeArabic(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is >= '\u064B' and <= '\u0652' or '\u0640' or '\u0670')
                continue;
            sb.Append(c is 'أ' or 'إ' or 'آ' ? 'ا' : c);
        }

        return sb.ToString();
    }

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
        var targetKey = inferredNav ?? workflows.FirstOrDefault()?.RelatedNavKey ?? context.NavPageKey;
        var guide = targetKey is not null
            ? ProgramKnowledge.TryGetPageGuide(targetKey) ?? ProgramKnowledge.FallbackPage(targetKey)
            : null;

        if (workflows.Count > 0)
        {
            var workflow = workflows[0];
            var guideAr = guide is null ? string.Empty : $"\n\nدليل الشاشة «{guide.TitleAr}»: {guide.GuideAr}";
            var guideEn = guide is null ? string.Empty : $"\n\nScreen guide for “{guide.TitleEn}”: {guide.GuideEn}";
            return new FinancialAssistantReply(
                $"{workflow.TitleAr}: {workflow.StepsAr}{guideAr}",
                $"{workflow.TitleEn}: {workflow.StepsEn}{guideEn}",
                ProgramKnowledge.SourceId);
        }

        if (guide is not null)
        {
            return new FinancialAssistantReply(
                $"لا أملك خطوات موثّقة لهذا الإجراء بعينه؛ إليك دليل الشاشة المتاح «{guide.TitleAr}»: {guide.GuideAr}",
                $"I don't have verified steps for that specific action; here is the available screen guide for “{guide.TitleEn}”: {guide.GuideEn}",
                ProgramKnowledge.SourceId);
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
