namespace ShuraCouncil;

/// <summary>
/// Six-gate supreme certification mapped from Anthropic deep-research phases
/// + BRQOSH closure law. Supreme ≠ theatrical — all gates need evidence.
/// </summary>
internal static class SupremeGateEngine
{
    public static SupremeGateReport Evaluate(
        EvidenceBundle evidence,
        IReadOnlyList<DecisionOutcome> decisions,
        IReadOnlyList<AdversarialVerdict> adversarial,
        FacilitatorSynthesis facilitator,
        bool overallConsensus,
        LeakSynthesisReport leakSynthesis)
    {
        var gates = new List<SupremeGateStatus>
        {
            EvaluateScope(evidence),
            EvaluateEvidence(evidence),
            EvaluateAdversarial(adversarial, evidence),
            EvaluateConsensus(overallConsensus, decisions),
            EvaluateFacilitator(facilitator, overallConsensus),
            EvaluateLeakIntegrity(leakSynthesis),
        };

        var allPass = gates.All(g => g.Passed);
        return new SupremeGateReport(gates, allPass, BuildCertificationSummary(gates, allPass, leakSynthesis));
    }

    private static SupremeGateStatus EvaluateScope(EvidenceBundle e) =>
        new("G-SCOPE", "Scope (deep-research phase 1)",
            e.SourceCeefPath is not null && File.Exists(e.SourceCeefPath),
            e.SourceCeefPath ?? "no CEEF SAVE");

    private static SupremeGateStatus EvaluateEvidence(EvidenceBundle e) =>
        new("G-EVIDENCE", "Evidence paths loaded",
            e.EvidencePaths.Count >= 3,
            $"{e.EvidencePaths.Count} paths");

    private static SupremeGateStatus EvaluateAdversarial(
        IReadOnlyList<AdversarialVerdict> adversarial,
        EvidenceBundle e)
    {
        if (adversarial.Count == 0)
            return new("G-VERIFY", "Adversarial 3b (deep-research phase 4)", false, "no claims");

        var p0Claims = e.Workspace == WorkspaceKind.ProgramDeveloper
            ? new[] { "C-PROMPT-LAW", "C-PROMPT-SKILLS", "C-PROMPT-CODE" }
            : new[] { "C-SEC", "C-TEST", "C-GL" };

        var p0Killed = adversarial.Any(a => !a.Survived && p0Claims.Contains(a.ClaimId));
        var survived = adversarial.Count(a => a.Survived);
        return new("G-VERIFY", "Adversarial 3b (deep-research phase 4)",
            !p0Killed && survived == adversarial.Count,
            $"{survived}/{adversarial.Count} survived · P0 killed={p0Killed}");
    }

    private static SupremeGateStatus EvaluateConsensus(bool overall, IReadOnlyList<DecisionOutcome> decisions)
    {
        var p0Met = decisions.Where(d => d.IsP0).All(d => d.ConsensusMet);
        return new("G-CONSENSUS", "Council vote consensus",
            overall && p0Met,
            $"overall={overall} · P0 decisions met={p0Met}");
    }

    private static SupremeGateStatus EvaluateFacilitator(FacilitatorSynthesis f, bool overall)
    {
        var pass = overall && (!f.EscalationRequired || f.Dissents.Count > 0);
        return new("G-FACILITATOR", "F00 synthesis · steelman · escalation",
            pass,
            f.EscalationRequired ? "escalation documented" : "clean facilitator");
    }

    private static SupremeGateStatus EvaluateLeakIntegrity(LeakSynthesisReport leak) =>
        new("G-LEAK", "279-leak corpus synthesis integrity v4",
            leak.IntegrityMet,
            $"score={leak.IntegrityScore}/100 · families={leak.FamiliesPresent.Count}/{LeakSynthesisEngine.FamilyCount}");

    private static string BuildCertificationSummary(
        IReadOnlyList<SupremeGateStatus> gates,
        bool allPass,
        LeakSynthesisReport leak)
    {
        if (allPass)
            return $"SUPREME v4 certified — {leak.FamiliesPresent.Count}/{LeakSynthesisEngine.FamilyCount} leak families active · integrity {leak.IntegrityScore}%";

        var failed = string.Join(", ", gates.Where(g => !g.Passed).Select(g => g.Id));
        return $"Supreme gate incomplete — failed: {failed}";
    }
}
