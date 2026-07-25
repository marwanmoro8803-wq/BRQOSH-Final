namespace ShuraCouncil;

/// <summary>
/// Resolves seat disagreements using BRQOSH conflict priority chain.
/// Security > Accounting > Verify > Architecture > Product > UX > Aesthetics
/// </summary>
internal static class ConflictResolver
{
    private static readonly Dictionary<string, int> BrqoshSeatPriority = new(StringComparer.Ordinal)
    {
        ["S05"] = 100,
        ["S08"] = 90,
        ["S11"] = 85,
        ["S12"] = 80,
        ["S03"] = 70,
        ["S02"] = 65,
        ["S04"] = 50,
        ["S06"] = 45,
        ["S09"] = 40,
        ["S10"] = 35,
        ["S07"] = 30,
        ["S01"] = 20,
    };

    /// <summary>البرومبيت.txt QA Army conflict chain: Pen > Overseer > Report > Perf > i18n > …</summary>
    private static readonly Dictionary<string, int> PromptQaSeatPriority = new(StringComparer.Ordinal)
    {
        ["S04"] = 100, // Pen Tester
        ["S11"] = 90,  // Superintelligence Overseer
        ["S12"] = 85,  // Report Auditor
        ["S06"] = 75,  // Perf
        ["S07"] = 70,  // L10n/Arabic
        ["S02"] = 65,  // Exploratory skeptic
        ["S03"] = 60,  // UX/Accessibility
        ["S01"] = 50,  // Automation
        ["S08"] = 45,  // DevOps
        ["S09"] = 42,  // Compatibility
        ["S05"] = 40,  // Beta
        ["S10"] = 35,  // Copywriter
    };

    public static IReadOnlyList<ConflictResolution> Resolve(
        EvidenceBundle evidence,
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions)
    {
        var priority = evidence.Workspace == WorkspaceKind.ProgramDeveloper
            ? PromptQaSeatPriority
            : BrqoshSeatPriority;
        return ResolveInternal(seats, decisions, priority);
    }

    [Obsolete("Use Resolve(evidence, seats, decisions)")]
    public static IReadOnlyList<ConflictResolution> Resolve(
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions) =>
        ResolveInternal(seats, decisions, BrqoshSeatPriority);

    private static IReadOnlyList<ConflictResolution> ResolveInternal(
        IReadOnlyList<SeatVerdict> seats,
        IReadOnlyList<DecisionOutcome> decisions,
        Dictionary<string, int> seatPriority)
    {
        var results = new List<ConflictResolution>();
        foreach (var decision in decisions)
        {
            var votes = CollectVotes(seats, decision.Id);
            if (votes.Count < 2) continue;

            var groups = votes.GroupBy(v => v.Vote).ToList();
            if (groups.Count <= 1) continue;

            var winner = groups
                .OrderByDescending(g => g.Max(v => seatPriority.GetValueOrDefault(v.SeatId, 0)))
                .ThenByDescending(g => g.Count())
                .First();

            var dissenters = votes.Where(v => v.Vote != winner.Key).ToList();
            if (dissenters.Count == 0) continue;

            results.Add(new ConflictResolution(
                decision.Id,
                decision.TitleAr,
                winner.Key,
                string.Join(", ", dissenters.Select(d => d.SeatId)),
                BuildResolutionReason(winner.Key, dissenters, seatPriority)));
        }

        return results;
    }

    private static List<(string SeatId, VoteKind Vote)> CollectVotes(
        IReadOnlyList<SeatVerdict> seats,
        string decisionId)
    {
        var list = new List<(string, VoteKind)>();
        foreach (var seat in seats)
        {
            if (seat.DecisionVotes.TryGetValue(decisionId, out var vote))
                list.Add((seat.SeatId, vote));
        }

        return list;
    }

    private static string BuildResolutionReason(
        VoteKind winner,
        IReadOnlyList<(string SeatId, VoteKind Vote)> dissenters,
        Dictionary<string, int> seatPriority)
    {
        var topDissent = dissenters
            .OrderByDescending(d => seatPriority.GetValueOrDefault(d.SeatId, 0))
            .First();

        return winner switch
        {
            VoteKind.Reject => $"أولوية {topDissent.SeatId} — رفض يحسم",
            VoteKind.Conditional => $"تعارض — حسم مشروط (أولوية {topDissent.SeatId})",
            _ => dissenters.Any(d => seatPriority.GetValueOrDefault(d.SeatId, 0) >= 80)
                ? $"موافقة مع تحفظ من {topDissent.SeatId}"
                : "إجماع بعد أولوية المجال",
        };
    }
}
