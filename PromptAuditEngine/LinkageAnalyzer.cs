namespace PromptAuditEngine;

/// <summary>Interconnection strength — imports, project refs, governance chain.</summary>
internal static class LinkageAnalyzer
{
    public static LinkageReport Analyze(string repoRoot, IReadOnlyList<StackInfo> stacks)
    {
        var findings = new List<LinkageFinding>();
        findings.AddRange(AnalyzeDotNet(repoRoot, stacks));
        findings.AddRange(AnalyzeNode(repoRoot, stacks));
        findings.AddRange(AnalyzePython(repoRoot, stacks));
        findings.AddRange(AnalyzeGovernance(repoRoot));

        var pass = findings.Count(f => f.Pass);
        var score = findings.Count == 0 ? 0 : pass * 100 / findings.Count;
        return new(score, findings);
    }

    private static IEnumerable<LinkageFinding> AnalyzeDotNet(string root, IReadOnlyList<StackInfo> stacks)
    {
        if (!stacks.Any(s => s.Id == "dotnet")) yield break;

        var projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        yield return new("LNK-DOTNET-PROJ", projects.Count > 0, $"csproj count={projects.Count}");

        var brokenRefs = 0;
        foreach (var proj in projects.Take(20))
        {
            var text = File.ReadAllText(proj);
            foreach (var line in text.Split('\n'))
            {
                if (!line.Contains("ProjectReference", StringComparison.Ordinal)) continue;
                var start = line.IndexOf("Include=\"", StringComparison.Ordinal);
                if (start < 0) continue;
                start += 9;
                var end = line.IndexOf('"', start);
                if (end < 0) continue;
                var rel = line[start..end].Replace('\\', Path.DirectorySeparatorChar);
                var abs = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(proj)!, rel));
                if (!File.Exists(abs)) brokenRefs++;
            }
        }
        yield return new("LNK-DOTNET-REF", brokenRefs == 0, brokenRefs == 0 ? "project refs OK" : $"broken refs={brokenRefs}");
    }

    private static IEnumerable<LinkageFinding> AnalyzeNode(string root, IReadOnlyList<StackInfo> stacks)
    {
        if (!stacks.Any(s => s.Id == "node")) yield break;
        var pkg = Path.Combine(root, "package.json");
        if (!File.Exists(pkg)) yield break;
        yield return new("LNK-NODE-PKG", true, "package.json present");
        var hasLock = File.Exists(Path.Combine(root, "package-lock.json"))
                      || File.Exists(Path.Combine(root, "pnpm-lock.yaml"))
                      || File.Exists(Path.Combine(root, "yarn.lock"));
        yield return new("LNK-NODE-LOCK", hasLock, hasLock ? "lockfile present" : "no lockfile");
    }

    private static IEnumerable<LinkageFinding> AnalyzePython(string root, IReadOnlyList<StackInfo> stacks)
    {
        if (!stacks.Any(s => s.Id == "python")) yield break;
        var cognitiveAbsent = !Directory.Exists(Path.Combine(root, "cognitive-qa"));
        yield return new("LNK-PY-NO-COGNITIVE", cognitiveAbsent, cognitiveAbsent ? "cognitive-qa absent (law)" : "cognitive-qa PRESENT");
    }

    private static IEnumerable<LinkageFinding> AnalyzeGovernance(string root)
    {
        var shura = Directory.Exists(Path.Combine(root, "ShuraCouncil"));
        yield return new("LNK-SHURA", shura, shura ? "ShuraCouncil linked" : "no ShuraCouncil");

        var pae = Directory.Exists(Path.Combine(root, "PromptAuditEngine"));
        yield return new("LNK-PAE", pae, pae ? "PromptAuditEngine linked" : "no PAE");

        var skills = Path.Combine(root, ".cursor", "skills", "omni-qa-army");
        yield return new("LNK-SKILLS", Directory.Exists(skills), Directory.Exists(skills) ? "omni-qa-army" : "missing skills");

        var law = Path.Combine(skills, "prompt-source-law.md");
        yield return new("LNK-LAW", File.Exists(law), "prompt-source-law");

        yield return new("LNK-OMNI-MANIFEST", AnalyzeOmniManifest(skills), "omni-qa module manifest");
    }

    private static bool AnalyzeOmniManifest(string skillsDir)
    {
        if (!Directory.Exists(skillsDir)) return false;
        string[] required =
        [
            "chaos-exploration.md", "empathetic-ux.md", "deep-audit.md",
            "forensic-resilience.md", "chronicle-protocol.md",
            "launcher-architecture.md", "orchestrator-law.md",
            "superintelligence-core.md", "vision-perception.md", "report-excellence.md"
        ];
        return required.All(m => File.Exists(Path.Combine(skillsDir, m)));
    }
}
