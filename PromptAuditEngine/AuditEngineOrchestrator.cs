namespace PromptAuditEngine;

internal static class AuditEngineOrchestrator
{
    public static AuditEngineResult Run(string repoRoot, bool skipBuild, string? targetAppPath = null)
    {
        var stacks = StackDetector.Detect(repoRoot);
        if (stacks.Count == 0)
            stacks = [new StackInfo("generic", "Generic (skills-only)", null, true)];

        var verifications = VerifyRunner.Run(repoRoot, stacks, skipBuild);
        var linkage = LinkageAnalyzer.Analyze(repoRoot, stacks);
        var workflow = WorkflowAnalyzer.Analyze(repoRoot);

        var verifyPass = verifications.Count == 0 || verifications.All(v => v.Passed);
        var strength = (linkage.Score + workflow.Score) / 2;
        if (verifications.Count > 0)
            strength = (strength + (verifyPass ? 100 : 30)) / 2;

        var overall = verifyPass && linkage.Score >= 60 && workflow.Score >= 50;
        var stackLabels = string.Join(", ", stacks.Select(s => s.Label));
        var summary = overall
            ? $"PASS — stacks=[{stackLabels}] · strength={strength}% · governance chain OK"
            : $"PARTIAL — stacks=[{stackLabels}] · strength={strength}% · review verifications";

        return new(
            repoRoot,
            DateTime.UtcNow,
            stacks,
            verifications,
            linkage,
            workflow,
            strength,
            overall,
            summary,
            targetAppPath);
    }
}
