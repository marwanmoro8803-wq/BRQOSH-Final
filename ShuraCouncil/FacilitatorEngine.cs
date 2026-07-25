namespace ShuraCouncil;

/// <summary>
/// Meta-cognitive facilitator (F00) — synthesis, steelman, escalation.
/// Patterns: OpenAI Codex escalation · xAI steelman · Fable no false closure.
/// </summary>
internal static class FacilitatorEngine
{
    public static FacilitatorSynthesis Synthesize(
        EvidenceBundle evidence,
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions,
        IReadOnlyList<AdversarialVerdict> adversarial,
        IReadOnlyList<ConflictResolution> conflicts,
        bool overallConsensus)
    {
        var dissents = CollectDissents(seats, decisions);
        var steelman = BuildSteelmanMinority(seats, decisions, dissents);
        var escalation = RequiresEscalation(evidence, decisions, adversarial, dissents);
        var summary = BuildSummary(evidence, overallConsensus, adversarial, escalation);

        return new FacilitatorSynthesis(summary, dissents, steelman, escalation);
    }

    private static IReadOnlyList<DissentRecord> CollectDissents(
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions)
    {
        var records = new List<DissentRecord>();
        foreach (var decision in decisions.Where(d => d.Reject > 0 || !d.ConsensusMet))
        {
            foreach (var seat in seats)
            {
                if (!seat.DecisionVotes.TryGetValue(decision.Id, out var vote)) continue;
                if (vote != VoteKind.Reject && vote != VoteKind.Conditional) continue;
                if (vote == VoteKind.Conditional && decision.ConsensusMet) continue;

                var objection = seat.Findings.FirstOrDefault(f => !f.Pass)?.Detail ?? seat.SummaryAr;
                records.Add(new DissentRecord(
                    seat.SeatId,
                    decision.Id,
                    objection,
                    seat.Findings.FirstOrDefault(f => !f.Pass)?.Code ?? "—"));
            }
        }

        return records;
    }

    private static string BuildSteelmanMinority(
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions,
        IReadOnlyList<DissentRecord> dissents)
    {
        if (dissents.Count == 0)
            return "لا أقلية معارِضة — الإجماع واسع.";

        var topReject = dissents
            .Where(d => seats.First(s => s.SeatId == d.SeatId)
                .DecisionVotes.TryGetValue(d.DecisionId, out var v) && v == VoteKind.Reject)
            .GroupBy(d => d.SeatId)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (topReject is null)
        {
            var conditional = dissents.First();
            return $"أقوى تحفظ مشروط من {conditional.SeatId}: {conditional.ObjectionAr}";
        }

        var seat = seats.First(s => s.SeatId == topReject.Key);
        return $"أقوى اعتراض (steelman) من {topReject.Key} ({seat.RoleAr}): " +
               $"{string.Join("؛ ", topReject.Select(d => d.ObjectionAr).Distinct().Take(2))}";
    }

    private static bool RequiresEscalation(
        EvidenceBundle e,
        IReadOnlyList<DecisionOutcome> decisions,
        IReadOnlyList<AdversarialVerdict> adversarial,
        IReadOnlyList<DissentRecord> dissents)
    {
        if (e.SecurityP0 > 0) return true;
        if (adversarial.Any(a => !a.Survived && a.ClaimId is
            "C-SEC" or "C-GL" or "C-PROMPT-LAW" or "C-PROMPT-CODE" or "C-KARP-SYNTH")) return true;

        var p0Failed = decisions.Where(d => d.IsP0 && !d.ConsensusMet).Any();
        if (p0Failed) return true;

        var criticalDissent = dissents.Any(d =>
            d.SeatId is "S05" or "S08" or "S12" or "S04" or "S11" &&
            decisions.First(dec => dec.Id == d.DecisionId).IsP0);
        return criticalDissent;
    }

    private static string BuildSummary(
        EvidenceBundle e,
        bool consensus,
        IReadOnlyList<AdversarialVerdict> adversarial,
        bool escalation)
    {
        if (!consensus)
            return "F00: لا إجماع — راجع الادعاءات المقتولة في 3b والمعارضات المحفوظة.";

        var killed = adversarial.Count(a => !a.Survived);
        if (killed > 0)
            return $"F00: إجماع مشروط — {killed} ادعاءً حرجاً لم يجتز التحقق المعادِ.";

        if (escalation)
            return "F00: إجماع تقني مع تصعيد — مخاطر P0 أو معارضة من مقاعد حرجة (أمن/محاسبة/مدقق).";

        return "F00: إجماع معتمد — الأدلة والتحقق المعادِ متسقان؛ المعارضة مُوثَّقة للـ MEM.";
    }
}
