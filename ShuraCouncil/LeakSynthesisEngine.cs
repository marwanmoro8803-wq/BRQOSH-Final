namespace ShuraCouncil;

/// <summary>
/// Maps system_prompts_leaks-main families + BRQOSH synthesis layers to Shura intelligence.
/// Principles: Anthropic deep-research · Codex review · Gemini Inquiry · xAI steelman · Karpathy · platform bridge.
/// Grep-only — never bulk-loads leak text.
/// </summary>
internal static class LeakSynthesisEngine
{
    public const int FamilyCount = 14;
    private const int IntegrityFamilyMinimum = 10;

    private sealed record FamilySpec(
        string Id,
        string Label,
        string[] PathMarkers,
        string ShuraApplication,
        Func<string, EvidenceBundle, bool>? RepoProbe = null);

    private static readonly FamilySpec[] Families =
    [
        new("anthropic-deep-research", "Anthropic deep-research",
            ["deep-research/SKILL.md", "deep-research/scripts"],
            "round 3b · 3-vote adversarial · 2/3 refute kills"),
        new("anthropic-code-review", "Anthropic code-review",
            ["code-review/report-findings-tool.md", "code-review/high.md"],
            "findings-first · severity · P0 ordering"),
        new("anthropic-fable", "Anthropic Fable",
            ["anthropic_reminders.md", "Fable"],
            "epistemic humility · no false closure · S12"),
        new("anthropic-doctor", "Anthropic doctor/debug",
            ["bundled-skills/doctor", "claude-code-doctor"],
            "setup health · dead weight · S08 DevOps · doctor diagnostics"),
        new("openai-codex", "OpenAI Codex",
            ["OpenAI/Codex/gpt-5", "OpenAI/Codex/codex"],
            "escalation · plan_mode · multi-agent delegate"),
        new("microsoft-copilot", "Microsoft copilot-cli",
            ["Microsoft/copilot-cli.md"],
            "devil's advocate · verify before success · S02"),
        new("google-gemini", "Google Gemini",
            ["Google/gemini", "guided-learning"],
            "Inquiry vs Directive · facts round 1 only"),
        new("xai-grok", "xAI Grok",
            ["xAI/grok"],
            "steelman minority · verify before close"),
        new("perplexity", "Perplexity",
            ["Perplexity/"],
            "fan-out synthesis · G-items · S07"),
        new("cursor", "Cursor",
            ["Cursor/"],
            "parallel tools · read-before-edit · S06"),
        new("meta-consensus", "Meta consensus",
            ["Meta/meta-ai.md"],
            "state consensus before fringe · facilitator"),
        new("karpathy-think", "Karpathy think-before-code",
            ["andrej-karpathy-skills-main", "karpathy-guidelines"],
            "think before code · minimal diff · S06/S11",
            (root, _) => PromptGovernanceEvidence.KarpathySynthesisComplete(root)),
        new("ai-dev-platform", "AI dev platform orchestration",
            [],
            "multi-agent SDLC · Program Developer · S11",
            (root, _) => File.Exists(Path.Combine(root, ".cursor", "skills", "ai-dev-platform", "SKILL.md"))),
        new("chronicle-unified", "Council chronicle bridge",
            [],
            "Shura + AiCouncil timeline · S08/S12 · System 77",
            (_, e) => e.PlatformBridgeReady && e.CouncilChronicleExists),
    ];

    public static LeakSynthesisReport Analyze(string repoRoot, EvidenceBundle evidence)
    {
        var leakRoot = ResolveLeakRoot(repoRoot);
        var present = new List<LeakFamilyStatus>();
        var missing = new List<string>();

        foreach (var family in Families)
        {
            var found = family.RepoProbe?.Invoke(repoRoot, evidence) == true
                        || (leakRoot is not null && family.PathMarkers.Length > 0 && family.PathMarkers.Any(m =>
                            Directory.Exists(Path.Combine(leakRoot, m)) ||
                            File.Exists(Path.Combine(leakRoot, m)) ||
                            Directory.EnumerateFiles(leakRoot, "*", SearchOption.AllDirectories)
                                .Any(f => f.Contains(m.Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))));

            if (found)
                present.Add(new LeakFamilyStatus(family.Id, family.Label, family.ShuraApplication, true));
            else
                missing.Add(family.Label);
        }

        var indexPath = Path.Combine(repoRoot, ".cursor", "skills", "brqosh-skills-hub", "leak-corpus-index.yaml");
        var indexExists = File.Exists(indexPath);
        var shuraCore = File.Exists(Path.Combine(repoRoot, ".cursor", "skills", "brqosh-analysis", "shura-intelligence-core.md"));

        var familyScore = present.Count * 100 / FamilyCount;
        var corpusBonus = evidence.LeakCorpusFiles switch
        {
            >= 250 => 15,
            >= 100 => 10,
            >= 50 => 5,
            _ => 0,
        };
        var indexBonus = indexExists ? 10 : 0;
        var shuraBonus = shuraCore ? 5 : 0;
        var integrity = Math.Min(100, familyScore + corpusBonus + indexBonus + shuraBonus);

        return new LeakSynthesisReport(
            present,
            missing,
            integrity,
            evidence.LeakCorpusFiles,
            indexExists,
            integrity >= 70 && present.Count >= IntegrityFamilyMinimum);
    }

    private static string? ResolveLeakRoot(string repoRoot)
    {
        var nested = Path.Combine(repoRoot, "system_prompts_leaks-main", "system_prompts_leaks-main");
        if (Directory.Exists(nested)) return nested;
        var flat = Path.Combine(repoRoot, "system_prompts_leaks-main");
        return Directory.Exists(flat) ? flat : null;
    }
}
