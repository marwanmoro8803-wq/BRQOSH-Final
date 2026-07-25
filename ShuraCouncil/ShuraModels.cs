namespace ShuraCouncil;

internal enum WorkspaceKind
{
    Brqosh,
    ProgramDeveloper,
}

internal enum VoteKind
{
    Approve,
    Conditional,
    Reject,
}

internal enum ConfidenceLevel
{
    High,
    Medium,
    Low,
}

internal enum ClaimTier
{
    Confirmed,
    Plausible,
    Unverified,
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
    WorkspaceKind Workspace,
    bool PromptTxtExists,
    bool PromptAnalysisExists,
    bool PromptSourceLawExists,
    int OmniQaSkillModules,
    int OmniQaRoleModules,
    bool PromptCodeAbsent,
    bool LeakCorpusExists,
    int LeakCorpusFiles,
    bool AuditEngineExists,
    bool AuditReportFresh,
    int AuditStrengthScore,
    bool AuditOverallPass,
    bool AiCouncilIntegrated,
    bool CouncilChronicleExists,
    bool PlatformBridgeReady,
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

internal sealed record AdversarialVerdict(
    string ClaimId,
    string ClaimText,
    int VotesRefuted,
    int VotesConfirmed,
    bool Survived,
    ConfidenceLevel Confidence,
    ClaimTier Tier,
    IReadOnlyList<string> VoterReasons);

internal sealed record LeakFamilyStatus(
    string FamilyId,
    string Label,
    string ShuraApplication,
    bool Present);

internal sealed record LeakSynthesisReport(
    IReadOnlyList<LeakFamilyStatus> FamiliesPresent,
    IReadOnlyList<string> FamiliesMissing,
    int IntegrityScore,
    int CorpusFileCount,
    bool IndexExists,
    bool IntegrityMet);

internal sealed record SupremeGateStatus(
    string Id,
    string Label,
    bool Passed,
    string Detail);

internal sealed record SupremeGateReport(
    IReadOnlyList<SupremeGateStatus> Gates,
    bool AllGatesPass,
    string CertificationSummary);

internal sealed record DissentRecord(
    string SeatId,
    string DecisionId,
    string ObjectionAr,
    string EvidenceId);

internal sealed record ConflictResolution(
    string DecisionId,
    string DecisionTitleAr,
    VoteKind ResolvedVote,
    string DissentingSeats,
    string ReasonAr);

internal sealed record FacilitatorSynthesis(
    string SummaryAr,
    IReadOnlyList<DissentRecord> Dissents,
    string SteelmanMinorityAr,
    bool EscalationRequired);

internal sealed record CouncilResult(
    EvidenceBundle Evidence,
    IReadOnlyList<SeatVerdict> Seats,
    IReadOnlyList<DecisionOutcome> Decisions,
    IReadOnlyList<AdversarialVerdict> AdversarialVerdicts,
    IReadOnlyList<ConflictResolution> ConflictResolutions,
    FacilitatorSynthesis Facilitator,
    LeakSynthesisReport LeakSynthesis,
    SupremeGateReport SupremeGate,
    bool OverallConsensus,
    string UnifiedVerdictAr,
    IReadOnlyList<string> ActionP0);
