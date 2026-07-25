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
        new("S01", "المؤيد", ["C12", "C03"], ["OpenAI/Codex"], ["brqosh-analysis/requirements.md"], "delivery optimism · evidence-gated"),
        new("S02", "المعارض", ["C11", "C13"], ["Anthropic/Fable"], ["brqosh-analysis/deep-cognition.md"], "skeptic · no false completeness"),
        new("S03", "الناقد", ["C11", "C08"], ["Anthropic/Claude Code/verify"], ["brqosh-debug", "brqosh-closure"], "verify automation · coverage gaps"),
        new("S04", "استراتيجي", ["C02", "C12"], ["Google/gemini-cli"], ["brqosh-analysis/super-dev.md"], "3–5y platform · GTM risk"),
        new("S05", "أمن", ["C07"], ["Anthropic/security-review"], ["brqosh-review/security.md"], "attacker mindset · P0 first"),
        new("S06", "مهندس", ["C01", "C08"], ["Cursor", "OpenAI/Codex"], ["brqosh-engineering", "supreme-programming"], "tools in repo · maintainability"),
        new("S07", "سوق", ["C12", "C10"], ["Perplexity"], ["brqosh-analysis/requirements.md"], "sellability · G-items"),
        new("S08", "محاسب", ["C06"], ["— domain"], ["brqosh-accounting"], "balanced books · SOCPA-ready"),
        new("S09", "اقتصادي", ["C12", "C14"], ["OpenAI/Codex"], ["brqosh-analysis"], "ROI · prioritization"),
        new("S10", "CEO", ["C12"], ["OpenAI/Codex/plan_mode"], ["brqosh-analysis/requirements.md"], "focus · stop scope creep"),
        new("S11", "محصّي", ["C06", "C07"], ["— domain"], ["brqosh-accounting"], "ZATCA live vs stub"),
        new("S12", "مدقق خارجي", ["C08", "C06"], ["Anthropic/Fable"], ["brqosh-closure"], "4-level closure · no false ✅"),
    ];

    public static IReadOnlyList<SeatIntel> GetAll() => All;

    public static SeatIntel? Get(string seatId) =>
        All.FirstOrDefault(s => s.SeatId == seatId);
}
