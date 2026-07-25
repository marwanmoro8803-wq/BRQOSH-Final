namespace ShuraCouncil;

internal static class PromptConsensusEngine
{
    private static readonly DecisionSpec[] Decisions =
    [
        new("DP1", "تنفيذ البرومبيت مُصرَّح — CognitiveQa + PAE", true,
            e => PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
                 && PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot)
                ? VoteKind.Approve
                : e.PromptCodeAbsent && e.PromptSourceLawExists ? VoteKind.Approve : VoteKind.Conditional),
        new("DP2", "أساس omni-qa-army (P0+P1 skills) كافٍ للمرحلة الحالية", true, _ => VoteKind.Approve),
        new("DP3", "تأجيل موجة كود Python — مهارات P2/P3 مكتملة", false, _ => VoteKind.Approve),
        new("DP4", "مسار leaks grep (279) معتمد للتخليق", false, _ => VoteKind.Approve),
        new("DP5", "الشورى v4 Supreme يحكم البرومبيت عبر أدلة skills + leaks", true, _ => VoteKind.Approve),
        new("DP6", "محرك PromptAuditEngine + CognitiveQa — تدقيق متعدد اللغات مُصرَّح", true,
            e => PromptGovernanceEvidence.PaeGovernanceReady(e) ? VoteKind.Approve : VoteKind.Reject),
        new("DP7", "مهارات P2 deep engines مكتملة (5 وحدات)", true, e => PromptGovernanceEvidence.P2SkillsComplete(e.RepoRoot) ? VoteKind.Approve : VoteKind.Reject),
        new("DP8", "ميزانية leaks grep محترمة — لا لصق bulk", false, e => e.LeakCorpusExists ? VoteKind.Approve : VoteKind.Conditional),
        new("DP9", "قانون P3 launcher + orchestrator مكتمل (skills)", false, e => PromptGovernanceEvidence.P3SkillsComplete(e.RepoRoot) ? VoteKind.Approve : VoteKind.Reject),
        new("DP10", "جسر المنصة v4 — AiCouncil + Chronicle + CognitiveQa bridge", false,
            e => e.PlatformBridgeReady && e.AiCouncilIntegrated && e.CouncilChronicleExists
                ? VoteKind.Approve
                : e.PlatformBridgeReady ? VoteKind.Conditional : VoteKind.Reject),
    ];

    public static CouncilResult Run(EvidenceBundle evidence, IReadOnlyList<SeatVerdict> seats)
    {
        var decisionList = Decisions.ToList();
        if (PromptGovernanceEvidence.IsImplementCeef(evidence))
        {
            decisionList.Insert(0, new DecisionSpec(
                "DP1-LIFT",
                "تفويض المستخدم — موجة تنفيذ cognitive-qa مُصرَّحة",
                true,
                e => PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
                     && PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
                    ? VoteKind.Approve
                    : VoteKind.Reject));
        }

        if (PromptGovernanceEvidence.IsReplatformCeef(evidence))
        {
            decisionList.Insert(0, new DecisionSpec(
                "DP2-REPLATFORM",
                "CognitiveQa .NET 9 / FlaUI — المنصة الأساسية (CEEF-REPLATFORM-001 M1)",
                true,
                e => PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot)
                    ? VoteKind.Approve
                    : VoteKind.Reject));
        }

        if (PromptGovernanceEvidence.IsKarpathyCeef(evidence))
        {
            decisionList.Add(new DecisionSpec(
                "DK1",
                "تخليق Karpathy في المهارات — لا قاعدة alwaysApply مكررة",
                true,
                e => PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot)
                    ? VoteKind.Approve
                    : VoteKind.Reject));
        }

        var outcomes = new List<PromptConsensusEngineHelpers.DecisionSpecOutcome>();
        foreach (var spec in decisionList)
        {
            var approve = 0;
            var conditional = 0;
            var reject = 0;
            foreach (var seat in seats)
            {
                if (!seat.DecisionVotes.TryGetValue(spec.Id, out var vote)) continue;
                switch (vote)
                {
                    case VoteKind.Approve: approve++; break;
                    case VoteKind.Conditional: conditional++; break;
                    case VoteKind.Reject: reject++; break;
                }
            }

            var supportive = approve + conditional;
            var consensusMet = supportive >= 9 && reject <= 1;
            if (spec.IsP0 && reject > 1) consensusMet = false;

            var resultVote = reject > approve + conditional ? VoteKind.Reject
                : reject > 0 && approve < 9 ? VoteKind.Conditional
                : approve >= conditional ? VoteKind.Approve : VoteKind.Conditional;

            outcomes.Add(new PromptConsensusEngineHelpers.DecisionSpecOutcome(
                spec, approve, conditional, reject, resultVote, consensusMet));
        }

        var p0Pass = outcomes.Where(o => o.Spec.IsP0).All(o => o.ConsensusMet);
        var dp1Lift = outcomes.FirstOrDefault(o => o.Spec.Id == "DP1-LIFT");
        var dp2Replatform = outcomes.FirstOrDefault(o => o.Spec.Id == "DP2-REPLATFORM");
        var dp1 = outcomes.First(o => o.Spec.Id == "DP1");
        var dp2 = outcomes.First(o => o.Spec.Id == "DP2");
        var overall = p0Pass && dp1.ConsensusMet && dp2.ConsensusMet;
        if (PromptGovernanceEvidence.IsImplementCeef(evidence) && dp1Lift is not null)
            overall = overall && dp1Lift.ConsensusMet;
        if (PromptGovernanceEvidence.IsReplatformCeef(evidence) && dp2Replatform is not null)
            overall = overall && dp2Replatform.ConsensusMet;

        var decisionOutcomes = outcomes.Select(o => o.ToOutcome()).ToList();
        var adversarial = AdversarialVerifier.VerifyCriticalClaims(evidence);
        var actions = BuildActionPlan(evidence, adversarial);

        return CouncilResultAssembler.Assemble(
            evidence, seats, decisionOutcomes, adversarial, overall, actions,
            (consensus, facilitator) => BuildUnifiedVerdict(evidence, consensus, facilitator),
            (_, adv) => !adv.Any(a => !a.Survived && a.ClaimId is
                "C-PROMPT-LAW" or "C-PROMPT-SKILLS" or "C-PROMPT-ENGINE" or "C-KARP-SYNTH"));
    }

    private static string BuildUnifiedVerdict(EvidenceBundle e, bool consensus, FacilitatorSynthesis facilitator)
    {
        if (!consensus)
            return facilitator.SummaryAr + " — راجع DP1/DP2 والادعاءات المقتولة في 3b.";

        if (PromptGovernanceEvidence.IsReplatformCeef(e))
        {
            return $"Program Developer — إعادة المنصة (DP2-REPLATFORM): CognitiveQa .NET 9 / FlaUI M1؛ " +
                   $"omni-qa-army ({e.OmniQaSkillModules} modules · {e.OmniQaRoleModules} roles)؛ " +
                   $"PAE strength={e.AuditStrengthScore}%. [{facilitator.SummaryAr}]";
        }

        if (PromptGovernanceEvidence.IsImplementCeef(e))
        {
            return $"Program Developer — تنفيذ البرومبيت مُصرَّح (DP1-LIFT): cognitive-qa جاهز؛ " +
                   $"omni-qa-army ({e.OmniQaSkillModules} modules · {e.OmniQaRoleModules} roles)؛ " +
                   $"PAE strength={e.AuditStrengthScore}%؛ leaks={e.LeakCorpusFiles}. [{facilitator.SummaryAr}]";
        }

        if (PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
            && PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot))
        {
            return $"Program Developer — تنفيذ مُصرَّح (PS01-RELEASED): CognitiveQa .NET + PAE {e.AuditStrengthScore}%؛ " +
                   $"جسر v4 (AiCouncil·Chronicle)؛ omni-qa-army ({e.OmniQaSkillModules} modules · {e.OmniQaRoleModules} roles)؛ " +
                   $"leaks={e.LeakCorpusFiles}. [{facilitator.SummaryAr}]";
        }

        return $"Program Developer — حوكمة البرومبيت: الملف الخام «أفكار للشورى والمهارات» فقط؛ " +
               $"omni-qa-army ({e.OmniQaSkillModules} modules · {e.OmniQaRoleModules} roles)؛ " +
               $"محرك PAE strength={e.AuditStrengthScore}%؛ " +
               $"لا cognitive-qa في المستودع؛ leaks={e.LeakCorpusFiles} ملف مفهرس؛ " +
               $"التنفيذ من البرومبيت مُجمَّد حتى أمر صريح. [{facilitator.SummaryAr}]";
    }

    private static IReadOnlyList<string> BuildActionPlan(
        EvidenceBundle e,
        IReadOnlyList<AdversarialVerdict> adversarial)
    {
        var items = new List<string>();
        foreach (var failed in adversarial.Where(a => !a.Survived))
            items.Add($"P0: إعادة verify ادعاء {failed.ClaimId} — {failed.ClaimText}");
        if (!e.PromptSourceLawExists)
            items.Add("P0: إنشاء prompt-source-law.md — verify: Test-Path");
        if (!PromptGovernanceEvidence.SkillsFoundationComplete(e.OmniQaSkillModules, e.OmniQaRoleModules))
            items.Add($"P0: إكمال omni-qa-army — modules={e.OmniQaSkillModules} roles={e.OmniQaRoleModules}");
        if (!e.PromptCodeAbsent && !PromptGovernanceEvidence.IsPromptCodeWaveCeef(e)
            && !PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot))
            items.Add("P0: إزالة cognitive-qa — البرومبيت أفكار فقط");
        if (PromptGovernanceEvidence.IsPromptCodeWaveCeef(e) && !PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot))
            items.Add("P0: إكمال CognitiveQa — AuditOrchestrator مفقود");
        if (!e.AuditEngineExists)
            items.Add("P0: تشغيل PromptAuditEngine — verify: .\\scripts\\ops\\run-prompt-audit-engine.ps1");
        items.Add("P2: عند تفويض المستخدم — موجة تنفيذ برومبيت عبر Shura SAVE جديد");
        return items;
    }
}

internal static class PromptConsensusEngineHelpers
{
    internal sealed record DecisionSpecOutcome(
        DecisionSpec Spec,
        int Approve,
        int Conditional,
        int Reject,
        VoteKind Result,
        bool ConsensusMet)
    {
        public DecisionOutcome ToOutcome() =>
            new(Spec.Id, Spec.TitleAr, Spec.IsP0, Approve, Conditional, Reject, Result, ConsensusMet);
    }
}
