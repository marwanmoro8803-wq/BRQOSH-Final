namespace Brqosh.Application.Intelligence;

/// <summary>يُركّب ردود المساعد بدمج السياق + المسار + الإجابة الأساسية.</summary>
public static class ProgramReplyComposer
{
    public static FinancialAssistantReply ComposeExpert(
        ProgramAssistantContext context,
        FinancialAssistantReply core,
        string? inferredNavKey,
        IReadOnlyList<ProgramWorkflowKnowledge.WorkflowHint> workflows,
        string source)
    {
        var partsAr = new List<string>();
        var partsEn = new List<string>();

        AppendContextHeader(context, partsAr, partsEn);

        var profileHint = ProgramProfileHints.TryGet(context.BusinessProfileName, context.NavPageKey);
        if (profileHint.Ar is not null)
        {
            partsAr.Add($"🏗 {profileHint.Ar}");
            partsEn.Add($"🏗 {profileHint.En}");
        }

        var inferredGuide = inferredNavKey is not null &&
                          !string.Equals(inferredNavKey, context.NavPageKey, StringComparison.OrdinalIgnoreCase)
            ? ProgramKnowledge.TryGetPageGuide(inferredNavKey)
            : null;

        if (inferredGuide is not null &&
            !ContainsSignificantOverlap(core.AnswerAr, inferredGuide.GuideAr))
        {
            partsAr.Add($"📌 متعلق بسؤالك — {inferredGuide.TitleAr}: {inferredGuide.GuideAr}");
            partsEn.Add($"📌 Related — {inferredGuide.TitleEn}: {inferredGuide.GuideEn}");
        }

        partsAr.Add(core.AnswerAr);
        partsEn.Add(core.AnswerEn);

        foreach (var wf in workflows)
        {
            if (ContainsSignificantOverlap(core.AnswerAr, wf.StepsAr))
                continue;

            partsAr.Add($"🔹 {wf.TitleAr}: {wf.StepsAr}");
            partsEn.Add($"🔹 {wf.TitleEn}: {wf.StepsEn}");
        }

        AppendPostingTipIfNeeded(context, partsAr, partsEn);
        AppendSuggestions(context, inferredNavKey, partsAr, partsEn);

        return new FinancialAssistantReply(
            string.Join("\n\n", partsAr),
            string.Join("\n\n", partsEn),
            source);
    }

    public static FinancialAssistantReply WrapPageOnly(
        ProgramAssistantContext context,
        string bodyAr,
        string bodyEn,
        string source)
    {
        var partsAr = new List<string>();
        var partsEn = new List<string>();
        AppendContextHeader(context, partsAr, partsEn);
        partsAr.Add(bodyAr);
        partsEn.Add(bodyEn);
        AppendSuggestions(context, context.NavPageKey, partsAr, partsEn);
        return new FinancialAssistantReply(
            string.Join("\n\n", partsAr),
            string.Join("\n\n", partsEn),
            source);
    }

    private static void AppendContextHeader(
        ProgramAssistantContext context,
        List<string> partsAr,
        List<string> partsEn)
    {
        var page = ProgramKnowledge.TryGetPageGuide(context.NavPageKey)
                   ?? ProgramKnowledge.FallbackPage(context.NavPageKey);
        var role = context.UserRoleName ?? "مستخدم";
        var profile = context.BusinessProfileName ?? "عام";

        partsAr.Add($"📍 أنت على: {page.TitleAr} | الدور: {role} | الملف: {profile}");
        partsEn.Add($"📍 Screen: {page.TitleEn} | Role: {role} | Profile: {profile}");
    }

    private static void AppendPostingTipIfNeeded(
        ProgramAssistantContext context,
        List<string> partsAr,
        List<string> partsEn)
    {
        if (context.NavPageKey is null)
            return;

        var transactional = context.NavPageKey.Contains("invoice", StringComparison.OrdinalIgnoreCase)
                            || context.NavPageKey.Contains("voucher", StringComparison.OrdinalIgnoreCase)
                            || context.NavPageKey is "journal_entries" or "work_orders" or "goods_receipts";

        if (!transactional)
            return;

        partsAr.Add("💡 تذكير: مسودة → تحقق → ترحيل. بعد الترحيل التصحيح بإشعار دائن/مدين فقط.");
        partsEn.Add("💡 Reminder: draft → validate → post. After posting, correct via credit/debit notes only.");
    }

    private static void AppendSuggestions(
        ProgramAssistantContext context,
        string? focusNavKey,
        List<string> partsAr,
        List<string> partsEn)
    {
        var key = focusNavKey ?? context.NavPageKey ?? "dashboard";
        var tips = key switch
        {
            "sales_invoices" => ("اسأل أيضاً: ZATCA · التحصيل · ذمم العملاء",
                "Also ask: ZATCA · collections · AR"),
            "purchase_invoices" => ("اسأل أيضاً: المخزون · سند الصرف · ذمم الموردين",
                "Also ask: inventory · payment · AP"),
            "zatca_settings" or "zatca_invoices" => ("اسأل أيضاً: O-01 OTP · فاتورة مرحّلة",
                "Also ask: O-01 OTP · posted invoice"),
            "projects" => ("اسأل أيضاً: BOQ · مستخلصات · ربحية المشروع",
                "Also ask: BOQ · billings · profitability"),
            _ => ("اسأل: «كيف أرحّل؟» · «صلاحياتي» · «التدفق النقدي»",
                "Ask: how to post · permissions · cash flow"),
        };

        partsAr.Add($"❓ {tips.Item1}");
        partsEn.Add($"❓ {tips.Item2}");
    }

    private static bool ContainsSignificantOverlap(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(needle) || string.IsNullOrWhiteSpace(haystack))
            return false;

        var fragment = needle.Length > 48 ? needle[..48] : needle;
        return haystack.Contains(fragment, StringComparison.OrdinalIgnoreCase);
    }
}
