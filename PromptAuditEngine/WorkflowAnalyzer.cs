namespace PromptAuditEngine;

/// <summary>Workflow & governance strength — prompt + Shura + skills chain.</summary>
internal static class WorkflowAnalyzer
{
    private static readonly string[] P2Modules =
    [
        "chaos-exploration.md", "empathetic-ux.md", "deep-audit.md",
        "forensic-resilience.md", "chronicle-protocol.md"
    ];

    private static readonly string[] P3Modules =
    [
        "launcher-architecture.md", "orchestrator-law.md"
    ];

    public static WorkflowReport Analyze(string repoRoot)
    {
        var paths = new List<string>();
        var projectYaml = Path.Combine(repoRoot, "PROJECT.yaml");
        var shura = Directory.Exists(Path.Combine(repoRoot, "ShuraCouncil"));
        var pae = Directory.Exists(Path.Combine(repoRoot, "PromptAuditEngine"));
        var omniDir = Path.Combine(repoRoot, ".cursor", "skills", "omni-qa-army");
        var rolesDir = Path.Combine(omniDir, "roles");
        var roleCount = Directory.Exists(rolesDir) ? Directory.GetFiles(rolesDir, "role-*.md").Length : 0;
        var skillCount = Directory.Exists(omniDir)
            ? Directory.GetFiles(omniDir, "*.md").Count(f => !f.Contains($"{Path.DirectorySeparatorChar}roles{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            : 0;
        var p2Complete = P2Modules.All(m => File.Exists(Path.Combine(omniDir, m)));
        var p3Complete = P3Modules.All(m => File.Exists(Path.Combine(omniDir, m)));
        var skillsComplete = skillCount >= 13 && roleCount >= 10 && p2Complete && p3Complete;
        var law = Path.Combine(omniDir, "prompt-source-law.md");
        var leakIndex = Path.Combine(repoRoot, ".cursor", "skills", "brqosh-skills-hub", "leak-corpus-index.yaml");

        if (File.Exists(projectYaml)) paths.Add(projectYaml);
        if (shura) paths.Add(Path.Combine(repoRoot, "ShuraCouncil"));
        if (pae) paths.Add(Path.Combine(repoRoot, "PromptAuditEngine"));
        if (Directory.Exists(omniDir)) paths.Add(omniDir);

        var ci = Directory.Exists(Path.Combine(repoRoot, ".github", "workflows"))
                 || File.Exists(Path.Combine(repoRoot, "azure-pipelines.yml"))
                 || Directory.Exists(Path.Combine(repoRoot, "scripts", "ops"));

        var checks = new[] { File.Exists(projectYaml), shura, pae, skillsComplete, p2Complete, p3Complete, File.Exists(law), File.Exists(leakIndex), ci };
        var score = checks.Count(c => c) * 100 / checks.Length;

        return new(score, File.Exists(projectYaml), shura, pae, skillsComplete, p2Complete, p3Complete, File.Exists(law), File.Exists(leakIndex), ci, paths);
    }
}
