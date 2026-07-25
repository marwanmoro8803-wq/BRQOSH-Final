namespace ShuraCouncil;

/// <summary>
/// Assembles full v3 council result: conflicts · facilitator · leak synthesis · supreme gates.
/// </summary>
internal static class CouncilResultAssembler
{
    public static CouncilResult Assemble(
        EvidenceBundle evidence,
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions,
        IReadOnlyList<AdversarialVerdict> adversarial,
        bool overall,
        IReadOnlyList<string> actions,
        Func<bool, FacilitatorSynthesis, string> buildVerdict,
        Func<EvidenceBundle, IReadOnlyList<AdversarialVerdict>, bool>? postAdversarialCheck = null)
    {
        if (postAdversarialCheck?.Invoke(evidence, adversarial) == false)
            overall = false;

        var conflicts = ConflictResolver.Resolve(evidence, seats, decisions);
        var facilitator = FacilitatorEngine.Synthesize(
            evidence, seats, decisions, adversarial, conflicts, overall);
        var leakSynthesis = LeakSynthesisEngine.Analyze(evidence.RepoRoot, evidence);
        var supreme = SupremeGateEngine.Evaluate(
            evidence, decisions, adversarial, facilitator, overall, leakSynthesis);
        var verdict = buildVerdict(overall, facilitator);

        return new CouncilResult(
            evidence,
            seats,
            decisions,
            adversarial,
            conflicts,
            facilitator,
            leakSynthesis,
            supreme,
            overall,
            verdict,
            actions);
    }
}
