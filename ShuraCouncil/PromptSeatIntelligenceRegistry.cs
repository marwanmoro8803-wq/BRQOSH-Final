namespace ShuraCouncil;

/// <summary>
/// Shura seats aligned to البرومبيت.txt — 10 QA roles + Superintelligence Overseer + Report Auditor.
/// v4: leak synthesis 14 families · platform bridge · Karpathy · ai-dev orchestration.
/// </summary>
internal static class PromptSeatIntelligenceRegistry
{
    internal static SeatIntelligenceRegistry.SeatIntel? Get(string seatId) =>
        All.FirstOrDefault(s => s.SeatId == seatId);

    internal static IReadOnlyList<SeatIntelligenceRegistry.SeatIntel> GetAll() => All;

    private static readonly IReadOnlyList<SeatIntelligenceRegistry.SeatIntel> All =
    [
        new("S01", "مهندس الأتمتة (QA Auto)",
            ["C01", "C11"],
            ["Anthropic/Claude Code/batch", "OpenAI/Codex"],
            ["omni-qa-army/roles/role-automation.md", "omni-qa-army/superintelligence-core.md", "omni-qa-army/orchestrator-law.md"],
            "framework architecture · CI/CD exit codes · Codex persistence · state machine · TEST_FRAMEWORK_ARCHITECTURE"),

        new("S02", "مستكشف يدوي (Manual QA)",
            ["C11", "C13"],
            ["Microsoft/copilot-cli", "Anthropic/Fable"],
            ["omni-qa-army/roles/role-exploratory.md", "omni-qa-army/exploratory-mindset.md", "shura-intelligence-core.md"],
            "unscripted exploration · Fable classify · devil's advocate · chaos heuristics · 3b voter"),

        new("S03", "مدقق UX/وصول (UX Auditor)",
            ["C03", "C11"],
            ["Anthropic/Claude Code/verify", "Google/gemini-cli", "Anthropic/code-review"],
            ["omni-qa-army/roles/role-accessibility.md", "omni-qa-army/vision-perception.md", "brqosh-ui-design-system"],
            "WCAG 2.1 AA · empathy layer · findings-first · design precision · UX_ACCESSIBILITY_COMPLIANCE_REPORT"),

        new("S04", "اختبار اختراق (Pen Tester)",
            ["C07"],
            ["Anthropic/security-review"],
            ["omni-qa-army/roles/role-pentest.md", "brqosh-review/security.md"],
            "OWASP desktop · mitm · local storage · conflict priority #1 · PENTEST_FINDINGS"),

        new("S05", "مختبر بيتا (Beta Tester)",
            ["C03", "C10"],
            ["xAI/grok-build"],
            ["omni-qa-army/roles/role-beta.md", "omni-qa-army/exploratory-mindset.md"],
            "irrational user · emotional journey · steelman edge cases · BETA_TESTER_USER_JOURNEY_FEEDBACK"),

        new("S06", "أداء وذاكرة (Perf Tester)",
            ["C11", "C14"],
            ["OpenAI/Codex", "Karpathy"],
            ["omni-qa-army/roles/role-performance.md", "supreme-programming-mastery/karpathy-synthesis.md"],
            "stress loops · think-before-code · minimal diff · PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT"),

        new("S07", "تدويل وعربي (L10n/I18n)",
            ["C03", "C10"],
            ["Anthropic/Fable", "Perplexity"],
            ["omni-qa-army/roles/role-i18n.md", "omni-qa-army/arabic-qa.md"],
            "RTL · Arabic payloads · fan-out synthesis · pseudo-l10n · I18N_AND_L10N_TEST_REPORT"),

        new("S08", "DevOps وتكامل",
            ["C08", "C01"],
            ["Anthropic/Claude Code/doctor", "OpenAI/Codex", "chronicle-unified"],
            ["omni-qa-army/roles/role-devops.md", "brqosh-closure", "CognitiveQa.Core/PlatformPaths.cs"],
            "headless · platform bridge · AiCouncil launcher · chronicle merge · DEVOPS_INTEGRATION_GUIDE"),

        new("S09", "توافق وأجهزة (Compatibility)",
            ["C03", "C11"],
            ["Cursor"],
            ["omni-qa-army/roles/role-compatibility.md", "omni-qa-army/vision-perception.md"],
            "DPI · resolution matrix · parallel tools · COMPATIBILITY_TEST_MATRIX"),

        new("S10", "نسّاخ تقني (Tech Copywriter)",
            ["C10"],
            ["OpenAI/Codex"],
            ["omni-qa-army/roles/role-copy.md", "brqosh-communication"],
            "microcopy audit · tone · MICROCOPY_AND_DOCS_AUDIT"),

        new("S11", "المراقب الخارق (Superintelligence)",
            ["C13", "C02"],
            ["Anthropic/deep-research", "Perplexity", "ai-dev-platform", "Karpathy"],
            ["omni-qa-army/superintelligence-core.md", "shura-intelligence-core.md", "ai-dev-platform/SKILL.md"],
            "state graph · leak v4 14 families · oracles · omniscient synthesis · cross-correlation"),

        new("S12", "مدقق التقارير (Report Auditor)",
            ["C08", "C11"],
            ["Anthropic/code-review", "Anthropic/Fable", "chronicle-unified"],
            ["omni-qa-army/report-excellence.md", "omni-qa-army/bug-tracker-spec.md", "omni-qa-army/chronicle-protocol.md"],
            "severity-first · chronicle unified · reproducibility · no false closure · 3b voter · AUDIT_REPORT"),

        new("F00", "الوسيط العليم (Omniscient Facilitator)",
            ["C08", "C13"],
            ["OpenAI/Codex/escalation", "xAI/steelman", "Anthropic/deep-research", "Meta/meta-ai"],
            ["shura-intelligence-core.md", "omni-qa-army/orchestrator-law.md", "shura-prompt-governance.md", "council-intelligence-matrix.md"],
            "unified theory of quality · meta-consensus · steelman · council-of-experts synthesis"),
    ];
}
