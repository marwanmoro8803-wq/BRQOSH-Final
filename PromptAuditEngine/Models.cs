namespace PromptAuditEngine;

internal sealed record StackInfo(
    string Id,
    string Label,
    string? ManifestPath,
    bool Detected);

internal sealed record VerifyResult(
    string StackId,
    string Command,
    int ExitCode,
    bool Passed,
    string OutputTail);

internal sealed record LinkageFinding(string Code, bool Pass, string Detail);

internal sealed record LinkageReport(
    int Score,
    IReadOnlyList<LinkageFinding> Findings);

internal sealed record WorkflowReport(
    int Score,
    bool ProjectYaml,
    bool ShuraTool,
    bool AuditEngineSelf,
    bool SkillsComplete,
    bool P2SkillsComplete,
    bool P3SkillsComplete,
    bool PromptLaw,
    bool LeakIndex,
    bool CiWorkflow,
    IReadOnlyList<string> GovernancePaths);

internal sealed record AuditEngineResult(
    string RepoRoot,
    DateTime GeneratedUtc,
    IReadOnlyList<StackInfo> Stacks,
    IReadOnlyList<VerifyResult> Verifications,
    LinkageReport Linkage,
    WorkflowReport Workflow,
    int StrengthScore,
    bool OverallPass,
    string Summary,
    string? TargetAppPath = null);
