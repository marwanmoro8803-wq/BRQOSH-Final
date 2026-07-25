namespace ShuraCouncil;

/// <summary>
/// Maps each Shura seat to BRQOSH councils, leak families, and skill modules.
/// Principles extracted from system_prompts_leaks-main via leak-corpus-index.yaml.
/// </summary>
internal static class SeatIntelligenceRegistry
{
    internal sealed record SeatIntel(
        string SeatId,
        string RoleAr,
        IReadOnlyList<string> Councils,
        IReadOnlyList<string> LeakFamilies,
        IReadOnlyList<string> SkillModules,
        string IntelligencePattern);

    private static readonly IReadOnlyList<SeatIntel> All =
    [
        new("S01", "المؤيد", ["C12", "C03"], ["OpenAI/Codex", "Anthropic/batch"], ["brqosh-analysis/requirements.md", "omni-qa-army/orchestrator-law.md"], "delivery optimism · evidence-gated · Codex persistence · «ماذا يعمل اليوم؟»"),
        new("S02", "المعارض", ["C11", "C13"], ["Microsoft/copilot-cli", "Anthropic/Fable"], ["brqosh-analysis/deep-cognition.md", "shura-intelligence-core.md"], "devil's advocate · Fable classify · «أين الدليل؟» · 3b voter"),
        new("S03", "الناقد", ["C11", "C08"], ["Anthropic/Claude Code/verify", "Anthropic/code-review"], ["brqosh-debug", "brqosh-closure", "omni-qa-army/vision-perception.md"], "verify automation · findings-first · coverage gaps · 3b voter"),
        new("S04", "استراتيجي", ["C02", "C12"], ["Google/gemini-cli", "ai-dev-platform"], ["brqosh-analysis/super-dev.md", "ai-dev-platform/sdlc-pipeline.md"], "3–5y platform · GTM risk · Inquiry/Directive split"),
        new("S05", "أمن", ["C07"], ["Anthropic/security-review"], ["brqosh-review/security.md"], "attacker mindset · P0 first · conflict priority #1"),
        new("S06", "مهندس", ["C01", "C08"], ["Cursor", "OpenAI/Codex", "Karpathy"], ["brqosh-engineering", "supreme-programming/karpathy-synthesis.md", "shura-prompt-governance.md"], "parallel tools · think-before-code · prompt harness · minimal diff"),
        new("S07", "سوق", ["C12", "C10"], ["Perplexity", "Anthropic/deep-research"], ["brqosh-analysis/requirements.md"], "sellability · G-items · fan-out synthesis · synthesize don't summarize"),
        new("S08", "محاسب", ["C06", "C08"], ["Anthropic/doctor", "chronicle-unified"], ["brqosh-accounting", "CognitiveQa.Core/PlatformPaths.cs"], "balanced books · platform bridge · SOCPA-ready · conflict priority #2"),
        new("S09", "اقتصادي", ["C12", "C14"], ["OpenAI/Codex/plan_mode"], ["brqosh-analysis"], "ROI · prioritization · Codex tradeoffs · plan_mode gate"),
        new("S10", "CEO", ["C12"], ["OpenAI/Codex/escalation"], ["brqosh-analysis/requirements.md"], "focus · stop scope creep · escalation gate"),
        new("S11", "محصّي", ["C06", "C07", "C13"], ["Anthropic/deep-research", "Karpathy", "ai-dev-platform"], ["brqosh-accounting", "shura-intelligence-core.md"], "ZATCA live vs stub · leak v4 integrity · omniscient synthesis"),
        new("S12", "مدقق خارجي", ["C08", "C06"], ["Anthropic/Fable", "chronicle-unified"], ["brqosh-closure", "omni-qa-army/chronicle-protocol.md"], "4-level closure · chronicle unified · no false ✅ · 3b voter"),
        new("F00", "الوسيط (Facilitator)", ["C08", "C13"], ["OpenAI/Codex/escalation", "xAI/steelman", "Meta/meta-ai"], ["shura-intelligence-core.md", "shura-prompt-governance.md", "council-intelligence-matrix.md"], "synthesis · steelman · meta-consensus · prompt governance tie-break"),
    ];

    public static IReadOnlyList<SeatIntel> GetAll() => All;

    public static SeatIntel? Get(string seatId) =>
        All.FirstOrDefault(s => s.SeatId == seatId);

    public static SeatIntel? GetForWorkspace(WorkspaceKind workspace, string seatId) =>
        workspace == WorkspaceKind.ProgramDeveloper
            ? PromptSeatIntelligenceRegistry.Get(seatId)
            : Get(seatId);
}
