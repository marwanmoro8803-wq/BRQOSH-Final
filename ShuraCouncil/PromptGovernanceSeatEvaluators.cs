namespace ShuraCouncil;

/// <summary>
/// Seat evaluators mapped to البرومبيت.txt ten QA roles + Superintelligence + Report Auditor.
/// </summary>
internal static class PromptGovernanceSeatEvaluators
{
    private static string SkillsRoot(EvidenceBundle e) =>
        Path.Combine(e.RepoRoot, ".cursor", "skills", "omni-qa-army");

    private static bool Skill(EvidenceBundle e, string relative) =>
        File.Exists(Path.Combine(e.RepoRoot, ".cursor", "skills", relative.Replace('/', Path.DirectorySeparatorChar)));

    private static bool Role(EvidenceBundle e, string roleFile) =>
        File.Exists(Path.Combine(SkillsRoot(e), "roles", roleFile));

    private static bool SkillsOk(EvidenceBundle e) =>
        PromptGovernanceEvidence.SkillsFoundationComplete(e.OmniQaSkillModules, e.OmniQaRoleModules);

    public static IReadOnlyList<SeatVerdict> EvaluateAll(EvidenceBundle e) =>
    [
        EvaluateS01(e), EvaluateS02(e), EvaluateS03(e), EvaluateS04(e),
        EvaluateS05(e), EvaluateS06(e), EvaluateS07(e), EvaluateS08(e),
        EvaluateS09(e), EvaluateS10(e), EvaluateS11(e), EvaluateS12(e),
    ];

    private static SeatVerdict Build(
        string id, string role, EvidenceBundle e,
        IReadOnlyList<SeatFinding> findings,
        Func<EvidenceBundle, IReadOnlyDictionary<string, VoteKind>> votes,
        string summary) =>
        new(id, role, findings, votes(e), summary);

    private static SeatVerdict EvaluateS01(EvidenceBundle e) =>
        Build("S01", "مهندس الأتمتة (QA Auto)", e,
        [
            new("S01-ROLE", Role(e, "role-automation.md"), "role-automation"),
            new("S01-PAE", e.AuditEngineExists, "PromptAuditEngine"),
            new("S01-ORCH", Skill(e, "omni-qa-army/prompt-audit-engine.md"), "prompt-audit-engine skill"),
            new("S01-LAUNCHER", Skill(e, "omni-qa-army/launcher-architecture.md"), "launcher-architecture"),
            new("S01-CORE", Skill(e, "omni-qa-army/superintelligence-core.md"), "superintelligence-core"),
        ], VotesChampion,
            Role(e, "role-automation.md") ? "إطار أتمتة مُعرَّف في المهارات — التنفيذ عند التفويض" : "فجوة role-automation");

    private static SeatVerdict EvaluateS02(EvidenceBundle e) =>
        Build("S02", "مستكشف يدوي (Manual QA)", e,
        [
            new("S02-ROLE", Role(e, "role-exploratory.md"), "role-exploratory"),
            new("S02-MINDSET", Skill(e, "omni-qa-army/exploratory-mindset.md"), "exploratory-mindset"),
            new("S02-CHAOS", Skill(e, "omni-qa-army/chaos-exploration.md"), "chaos-exploration"),
            new("S02-CODE", PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
                    || PromptGovernanceEvidence.IsPromptCodeWaveCeef(e)
                ? PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
                : e.PromptCodeAbsent, PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot) ? "CognitiveQa authorized" : "لا cognitive-qa"),
        ], VotesSkeptic,
            "استكشاف غير مُخطَّط · تخريب إبداعي — لا كود برومبيت قبل أمر المستخدم");

    private static SeatVerdict EvaluateS03(EvidenceBundle e) =>
        Build("S03", "مدقق UX/وصول (UX Auditor)", e,
        [
            new("S03-ROLE", Role(e, "role-accessibility.md"), "role-accessibility"),
            new("S03-VISION", Skill(e, "omni-qa-army/vision-perception.md"), "vision-perception"),
            new("S03-EMPATHY", Skill(e, "omni-qa-army/empathetic-ux.md"), "empathetic-ux"),
        ], VotesQuality,
            Role(e, "role-accessibility.md") ? "طبقة التعاطف والتصميم مُغطاة في المهارات" : "فجوة UX/وصول");

    private static SeatVerdict EvaluateS04(EvidenceBundle e) =>
        Build("S04", "اختبار اختراق (Pen Tester)", e,
        [
            new("S04-ROLE", Role(e, "role-pentest.md"), "role-pentest"),
            new("S04-SEC", Skill(e, "brqosh-review/security.md"), "security.md"),
            new("S04-LAW", e.PromptSourceLawExists, "prompt-source-law"),
        ], VotesSecurity,
            Role(e, "role-pentest.md") ? "أمن سطح المكتب مُعرَّف — OWASP · mitm في المهارات" : "فجوة pen-test");

    private static SeatVerdict EvaluateS05(EvidenceBundle e) =>
        Build("S05", "مختبر بيتا (Beta Tester)", e,
        [
            new("S05-ROLE", Role(e, "role-beta.md"), "role-beta"),
            new("S05-CHAOS", Skill(e, "omni-qa-army/chaos-exploration.md"), "chaos-exploration"),
        ], VotesBeta,
            Role(e, "role-beta.md") ? "سلوك المستخدم الحقيقي المُعرَّف" : "فجوة beta role");

    private static SeatVerdict EvaluateS06(EvidenceBundle e) =>
        Build("S06", "أداء وذاكرة (Perf Tester)", e,
        [
            new("S06-ROLE", Role(e, "role-performance.md"), "role-performance"),
            new("S06-DEEP", Skill(e, "omni-qa-army/deep-audit.md"), "deep-audit"),
            new("S06-FRAME", PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
                    || PromptGovernanceEvidence.IsPromptCodeWaveCeef(e)
                ? PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
                : e.PromptCodeAbsent, PromptGovernanceEvidence.IsPromptCodeWaveCeef(e) ? "profiler module" : "لا profiler مبكر"),
            new("S06-KARP", !PromptGovernanceEvidence.IsKarpathyCeef(e) ||
                PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot), "karpathy-synthesis"),
        ], VotesPerf,
            Role(e, "role-performance.md") ? "إجهاد وذاكرة مُعرَّفان في المهارات" : "فجوة performance");

    private static SeatVerdict EvaluateS07(EvidenceBundle e) =>
        Build("S07", "تدويل وعربي (L10n/I18n)", e,
        [
            new("S07-ROLE", Role(e, "role-i18n.md"), "role-i18n"),
            new("S07-AR", Skill(e, "omni-qa-army/arabic-qa.md"), "arabic-qa"),
            new("S07-RTL", e.PromptTxtExists, "البرومبيت يفرض RTL/عربي"),
        ], VotesI18n,
            Role(e, "role-i18n.md") && Skill(e, "omni-qa-army/arabic-qa.md")
                ? "إتقان عربي وRTL مُغطى — جوهر البرومبيت"
                : "فجوة i18n/عربي");

    private static SeatVerdict EvaluateS08(EvidenceBundle e) =>
        Build("S08", "DevOps وتكامل", e,
        [
            new("S08-ROLE", Role(e, "role-devops.md"), "role-devops"),
            new("S08-SHURA", e.ShuraToolExists, "ShuraCouncil harness"),
            new("S08-LAUNCHER", Skill(e, "omni-qa-army/launcher-architecture.md"), "launcher law"),
            new("S08-LEAK", e.LeakCorpusExists, "leak corpus"),
            new("S08-AICOUNCIL", e.AiCouncilIntegrated, "AiCouncil integrated"),
            new("S08-CHRONICLE", e.CouncilChronicleExists, "council chronicle"),
            new("S08-BRIDGE", e.PlatformBridgeReady, "CognitiveQa platform bridge"),
        ], VotesDevOps,
            e.PlatformBridgeReady && e.AiCouncilIntegrated
                ? "جسر المنصة v4 — AiCouncil + Chronicle + CognitiveQa"
                : Role(e, "role-devops.md") ? "تكامل CI/CD مُعرَّف — التنفيذ لاحقاً" : "فجوة devops");

    private static SeatVerdict EvaluateS09(EvidenceBundle e) =>
        Build("S09", "توافق وأجهزة (Compatibility)", e,
        [
            new("S09-ROLE", Role(e, "role-compatibility.md"), "role-compatibility"),
            new("S09-VISION", Skill(e, "omni-qa-army/vision-perception.md"), "multi-DPI vision"),
        ], VotesCompat,
            Role(e, "role-compatibility.md") ? "مصفوفة توافق مُعرَّفة" : "فجوة compatibility");

    private static SeatVerdict EvaluateS10(EvidenceBundle e) =>
        Build("S10", "نسّاخ تقني (Tech Copywriter)", e,
        [
            new("S10-ROLE", Role(e, "role-copy.md"), "role-copy"),
            new("S10-REPORT", Skill(e, "omni-qa-army/report-excellence.md"), "report-excellence"),
        ], VotesCopy,
            Role(e, "role-copy.md") ? "تدقيق microcopy مُعرَّف" : "فجوة copywriter");

    private static SeatVerdict EvaluateS11(EvidenceBundle e) =>
        Build("S11", "المراقب الخارق (Superintelligence)", e,
        [
            new("S11-CORE", Skill(e, "omni-qa-army/superintelligence-core.md"), "superintelligence-core"),
            new("S11-SHURA", e.ShuraToolExists, "ShuraCouncil v4"),
            new("S11-LEAK", e.LeakCorpusFiles >= 50, $"leaks={e.LeakCorpusFiles}"),
            new("S11-KARP", PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot), "karpathy-synthesis"),
            new("S11-AIDEV", Skill(e, "ai-dev-platform/SKILL.md"), "ai-dev-platform"),
            new("S11-CHRONICLE", e.CouncilChronicleExists, "unified chronicle"),
        ], VotesOverseer,
            Skill(e, "omni-qa-army/superintelligence-core.md") && PromptGovernanceEvidence.LeakSynthesisV4Ready(e.RepoRoot)
                ? "نواة الذكاء v4 — 14 عائلة leaks + جسر المنصة"
                : Skill(e, "omni-qa-army/superintelligence-core.md")
                    ? "نواة الذكاء الخارق مُخلَّصة — الشورى = مجلس الخبراء"
                    : "فجوة superintelligence-core");

    private static SeatVerdict EvaluateS12(EvidenceBundle e) =>
        Build("S12", "مدقق التقارير (Report Auditor)", e,
        [
            new("S12-REPORT", Skill(e, "omni-qa-army/report-excellence.md"), "report-excellence"),
            new("S12-BUGS", Skill(e, "omni-qa-army/bug-tracker-spec.md"), "bug-tracker-spec"),
            new("S12-CHRONICLE", Skill(e, "omni-qa-army/chronicle-protocol.md"), "chronicle-protocol"),
            new("S12-UNIFIED", e.CouncilChronicleExists && e.AiCouncilIntegrated, "chronicle unified"),
            new("S12-FRAME", PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
                    || PromptGovernanceEvidence.IsPromptCodeWaveCeef(e)
                ? PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
                : e.PromptCodeAbsent && SkillsOk(e), PromptGovernanceEvidence.IsPromptCodeWaveCeef(e) ? "framework ready" : "لا إغلاق وهمي"),
        ], VotesAuditor,
            e.PromptCodeAbsent && SkillsOk(e) && !PromptGovernanceEvidence.IsPromptCodeWaveCeef(e)
                ? "تقارير وbug tracker مُعرَّفان — skills فقط"
                : PromptGovernanceEvidence.IsPromptCodeWaveCeef(e) && PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
                    ? (PromptGovernanceEvidence.IsReplatformCeef(e) ? "إطار CognitiveQa .NET جاهز" : "إطار cognitive-qa جاهز")
                    : "لا توقيع إغلاق");

    private static IReadOnlyDictionary<string, VoteKind> VotesChampion(EvidenceBundle e) => VotesCore(e, Role(e, "role-automation.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesSkeptic(EvidenceBundle e) => VotesCore(e, SkillsOk(e), strictNoCode: true);
    private static IReadOnlyDictionary<string, VoteKind> VotesQuality(EvidenceBundle e) => VotesCore(e, Role(e, "role-accessibility.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesSecurity(EvidenceBundle e) => VotesCore(e, Role(e, "role-pentest.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesBeta(EvidenceBundle e) => VotesCore(e, Role(e, "role-beta.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesPerf(EvidenceBundle e) => VotesCore(e, Role(e, "role-performance.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesI18n(EvidenceBundle e) => VotesCore(e, Role(e, "role-i18n.md") && Skill(e, "omni-qa-army/arabic-qa.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesDevOps(EvidenceBundle e) =>
        VotesCore(e, Role(e, "role-devops.md") && e.ShuraToolExists);
    private static IReadOnlyDictionary<string, VoteKind> VotesCompat(EvidenceBundle e) => VotesCore(e, Role(e, "role-compatibility.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesCopy(EvidenceBundle e) => VotesCore(e, Role(e, "role-copy.md"));
    private static IReadOnlyDictionary<string, VoteKind> VotesOverseer(EvidenceBundle e) =>
        VotesCore(e, Skill(e, "omni-qa-army/superintelligence-core.md")
            && PromptGovernanceEvidence.LeakSynthesisV4Ready(e.RepoRoot));
    private static IReadOnlyDictionary<string, VoteKind> VotesAuditor(EvidenceBundle e) =>
        VotesCore(e, PromptGovernanceEvidence.IsPromptCodeWaveCeef(e)
            ? PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
            : SkillsOk(e) && e.PromptCodeAbsent);

    private static IReadOnlyDictionary<string, VoteKind> VotesCore(
        EvidenceBundle e,
        bool roleReady,
        bool strictNoCode = false)
    {
        var codeWave = PromptGovernanceEvidence.IsPromptCodeWaveCeef(e);
        var implement = PromptGovernanceEvidence.IsImplementCeef(e);
        var replatform = PromptGovernanceEvidence.IsReplatformCeef(e);
        var released = PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot);
        var dp1 = released && PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot)
            ? VoteKind.Approve
            : codeWave
                ? (released ? VoteKind.Approve : VoteKind.Reject)
                : (e.PromptCodeAbsent && e.PromptSourceLawExists
                    ? VoteKind.Approve
                    : strictNoCode ? VoteKind.Reject : VoteKind.Conditional);

        var votes = new Dictionary<string, VoteKind>
        {
            ["DP1"] = dp1,
            ["DP2"] = roleReady && SkillsOk(e) ? VoteKind.Approve : VoteKind.Conditional,
            ["DP3"] = VoteKind.Approve,
            ["DP4"] = e.LeakCorpusExists ? VoteKind.Approve : VoteKind.Conditional,
            ["DP5"] = e.ShuraToolExists && e.PromptAnalysisExists ? VoteKind.Approve : VoteKind.Reject,
            ["DP6"] = PromptGovernanceEvidence.PaeGovernanceReady(e) ? VoteKind.Approve : VoteKind.Reject,
            ["DP7"] = PromptGovernanceEvidence.P2SkillsComplete(e.RepoRoot) ? VoteKind.Approve : VoteKind.Reject,
            ["DP8"] = e.LeakCorpusExists ? VoteKind.Approve : VoteKind.Conditional,
            ["DP9"] = PromptGovernanceEvidence.P3SkillsComplete(e.RepoRoot) ? VoteKind.Approve : VoteKind.Reject,
            ["DP10"] = e.PlatformBridgeReady && e.AiCouncilIntegrated && e.CouncilChronicleExists
                ? VoteKind.Approve
                : e.PlatformBridgeReady ? VoteKind.Conditional : VoteKind.Reject,
        };

        if (implement)
        {
            votes["DP1-LIFT"] = PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot)
                && PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot)
                ? VoteKind.Approve
                : VoteKind.Reject;
        }

        if (replatform)
        {
            votes["DP2-REPLATFORM"] = PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot)
                ? VoteKind.Approve
                : VoteKind.Reject;
        }
        if (PromptGovernanceEvidence.IsKarpathyCeef(e))
        {
            votes["DK1"] = PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot)
                ? VoteKind.Approve
                : VoteKind.Reject;
        }

        return votes;
    }
}
