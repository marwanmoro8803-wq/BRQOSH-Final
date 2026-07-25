namespace ShuraCouncil;

internal enum VoteKind
{
    Approve,
    Conditional,
    Reject,
}

internal sealed record EvidenceBundle(
    string RepoRoot,
    string? SourceCeefPath,
    int TestsTotal,
    int TestsFailed,
    int SecurityP0,
    int SecurityP1,
    int MarketZeroCode,
    bool O01Open,
    bool G31Zero,
    bool G32Zero,
    int UiAuditTotal,
    int UiAuditPassed,
    int UiAuditFailed,
    bool UiAuditLogFound,
    bool FullScenarioPass,
    bool NightlyUiAuditScript,
    bool ShuraToolExists,
    int I18nArabicXamlLines,
    IReadOnlyList<string> EvidencePaths);

internal sealed record SeatFinding(string Code, bool Pass, string Detail);

internal sealed record SeatVerdict(
    string SeatId,
    string RoleAr,
    IReadOnlyList<SeatFinding> Findings,
    IReadOnlyDictionary<string, VoteKind> DecisionVotes,
    string SummaryAr);

internal sealed record DecisionSpec(
    string Id,
    string TitleAr,
    bool IsP0,
    Func<EvidenceBundle, VoteKind> AggregateRule);

internal sealed record DecisionOutcome(
    string Id,
    string TitleAr,
    bool IsP0,
    int Approve,
    int Conditional,
    int Reject,
    VoteKind Result,
    bool ConsensusMet);

internal sealed record CouncilResult(
    EvidenceBundle Evidence,
    IReadOnlyList<SeatVerdict> Seats,
    IReadOnlyList<DecisionOutcome> Decisions,
    bool OverallConsensus,
    string UnifiedVerdictAr,
    IReadOnlyList<string> ActionP0);
