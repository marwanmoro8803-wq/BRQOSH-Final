namespace ShuraCouncil;

internal static class PromptGovernanceEvidence
{
    public static void Enrich(string repoRoot, List<string> paths, ref WorkspaceKind workspace,
        ref bool promptTxt, ref bool promptAnalysis, ref bool promptLaw,
        ref int omniQaSkills, ref int omniQaRoles, ref bool promptCodeAbsent,
        ref bool leakExists, ref int leakFiles,
        ref bool auditEngine, ref bool auditFresh, ref int auditStrength, ref bool auditPass,
        ref bool aiCouncilIntegrated, ref bool councilChronicleExists, ref bool platformBridgeReady)
    {
        var projectYaml = Path.Combine(repoRoot, "PROJECT.yaml");
        if (File.Exists(projectYaml))
        {
            workspace = WorkspaceKind.ProgramDeveloper;
            paths.Add(projectYaml);
        }

        var promptPath = Directory.GetFiles(repoRoot, "*.txt")
            .FirstOrDefault(f => Path.GetFileName(f).Contains("برومب", StringComparison.OrdinalIgnoreCase)
                                 || Path.GetFileName(f).Equals("prompt.txt", StringComparison.OrdinalIgnoreCase));
        if (promptPath is not null && File.Exists(promptPath))
        {
            promptTxt = true;
            paths.Add(promptPath);
        }

        var analysis = Path.Combine(repoRoot, ".cursor", "skills", "ai-dev-platform", "omni-qa-prompt-analysis.md");
        if (File.Exists(analysis))
        {
            promptAnalysis = true;
            paths.Add(analysis);
        }

        var law = Path.Combine(repoRoot, ".cursor", "skills", "omni-qa-army", "prompt-source-law.md");
        if (File.Exists(law))
        {
            promptLaw = true;
            paths.Add(law);
        }

        var omniDir = Path.Combine(repoRoot, ".cursor", "skills", "omni-qa-army");
        if (Directory.Exists(omniDir))
        {
            omniQaSkills = Directory.GetFiles(omniDir, "*.md")
                .Count(f => !f.Contains($"{Path.DirectorySeparatorChar}roles{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
            var rolesDir = Path.Combine(omniDir, "roles");
            omniQaRoles = Directory.Exists(rolesDir)
                ? Directory.GetFiles(rolesDir, "role-*.md").Length
                : 0;
            paths.Add(omniDir);
        }

        promptCodeAbsent = !CognitiveQaFrameworkReady(repoRoot);

        var leakRoot = Path.Combine(repoRoot, "system_prompts_leaks-main", "system_prompts_leaks-main");
        if (!Directory.Exists(leakRoot))
            leakRoot = Path.Combine(repoRoot, "system_prompts_leaks-main");
        if (Directory.Exists(leakRoot))
        {
            leakExists = true;
            leakFiles = Directory.EnumerateFiles(leakRoot, "*", SearchOption.AllDirectories).Count();
            paths.Add(leakRoot);
        }

        var leakIndex = Path.Combine(repoRoot, ".cursor", "skills", "brqosh-skills-hub", "leak-corpus-index.yaml");
        if (File.Exists(leakIndex))
            paths.Add(leakIndex);

        auditEngine = Directory.Exists(Path.Combine(repoRoot, "PromptAuditEngine"));
        var auditJson = Path.Combine(repoRoot, "dist", "evidence", "prompt-audit", "latest.json");
        auditFresh = false;
        auditStrength = 0;
        auditPass = false;
        if (File.Exists(auditJson))
        {
            paths.Add(auditJson);
            auditFresh = File.GetLastWriteTimeUtc(auditJson) > DateTime.UtcNow.AddHours(-24);
            TryReadAuditSummary(auditJson, ref auditStrength, ref auditPass);
        }
        if (auditEngine)
            paths.Add(Path.Combine(repoRoot, "PromptAuditEngine"));

        aiCouncilIntegrated = AiCouncilIntegrated(repoRoot);
        councilChronicleExists = CouncilChronicleExists(repoRoot);
        platformBridgeReady = PlatformBridgeReady(repoRoot);
        if (aiCouncilIntegrated)
            paths.Add(Path.Combine(repoRoot, "AiCouncil", "council", "state", "agent-logs.md"));
        if (councilChronicleExists)
            paths.Add(Path.Combine(repoRoot, "dist", "evidence", "council-chronicle", "CHRONICLE.md"));
    }

    public static bool AiCouncilIntegrated(string repoRoot) =>
        Directory.Exists(Path.Combine(repoRoot, "AiCouncil", "brqosh"))
        && File.Exists(Path.Combine(repoRoot, "AiCouncil", "council", "state", "agent-logs.md"));

    public static bool CouncilChronicleExists(string repoRoot) =>
        File.Exists(Path.Combine(repoRoot, "dist", "evidence", "council-chronicle", "CHRONICLE.md"));

    public static bool PlatformBridgeReady(string repoRoot) =>
        File.Exists(Path.Combine(repoRoot, "CognitiveQa", "src", "CognitiveQa.Core", "PlatformPaths.cs"))
        && File.Exists(Path.Combine(repoRoot, "CognitiveQa", "src", "CognitiveQa.Core", "CouncilChronicleMerger.cs"))
        && File.Exists(Path.Combine(repoRoot, "scripts", "ops", "merge-council-chronicle.ps1"));

    private static void TryReadAuditSummary(string jsonPath, ref int strength, ref bool pass)
    {
        try
        {
            var text = File.ReadAllText(jsonPath);
            var strengthMatch = System.Text.RegularExpressions.Regex.Match(text, @"""StrengthScore""\s*:\s*(\d+)");
            if (strengthMatch.Success)
                strength = int.Parse(strengthMatch.Groups[1].Value);
            pass = text.Contains("\"OverallPass\": true", StringComparison.Ordinal);
        }
        catch
        {
            /* ignore parse errors */
        }
    }

    public static bool SkillsFoundationComplete(int skillModules, int roleModules) =>
        skillModules >= 13 && roleModules >= 10;

    public static bool P3SkillsComplete(string repoRoot)
    {
        var omniDir = Path.Combine(repoRoot, ".cursor", "skills", "omni-qa-army");
        return File.Exists(Path.Combine(omniDir, "launcher-architecture.md"))
               && File.Exists(Path.Combine(omniDir, "orchestrator-law.md"));
    }

    public static bool P2SkillsComplete(string repoRoot)
    {
        var omniDir = Path.Combine(repoRoot, ".cursor", "skills", "omni-qa-army");
        string[] p2 =
        [
            "chaos-exploration.md", "empathetic-ux.md", "deep-audit.md",
            "forensic-resilience.md", "chronicle-protocol.md"
        ];
        return p2.All(f => File.Exists(Path.Combine(omniDir, f)));
    }

    public static bool IsKarpathyCeef(EvidenceBundle e) =>
        e.SourceCeefPath?.Contains("KARP", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsImplementCeef(EvidenceBundle e) =>
        e.SourceCeefPath?.Contains("IMPLEMENT", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsReplatformCeef(EvidenceBundle e) =>
        e.SourceCeefPath?.Contains("REPLATFORM", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsPromptCodeWaveCeef(EvidenceBundle e) =>
        IsImplementCeef(e) || IsReplatformCeef(e);

    public static bool CognitiveQaDotNetReady(string repoRoot)
    {
        var basePath = Path.Combine(repoRoot, "CognitiveQa", "src");
        var required =
            new[]
            {
                Path.Combine(repoRoot, "CognitiveQa", "CognitiveQa.sln"),
                Path.Combine(basePath, "CognitiveQa.Orchestrator", "AuditOrchestrator.cs"),
                Path.Combine(basePath, "CognitiveQa.Cli", "Program.cs"),
                Path.Combine(basePath, "CognitiveQa.SourceIntel", "XamlIndex.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "RoleAuditors.cs"),
                Path.Combine(basePath, "CognitiveQa.Meta", "WeaknessSynthesizer.cs"),
                Path.Combine(basePath, "CognitiveQa.Repair", "RepairEngine.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "HumanNavigator.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "ForensicHandler.cs"),
                Path.Combine(basePath, "CognitiveQa.Core", "TargetResolver.cs"),
                Path.Combine(basePath, "CognitiveQa.Repair", "PatchApplicator.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "NetworkAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.SourceIntel", "ErpConstitutionRules.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "UiAccessibilitySnapshot.cs"),
                Path.Combine(basePath, "CognitiveQa.Meta", "CrossCorrelationWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "AuditManifestWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "MicrocopyAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "MultilingualReportBuilder.cs"),
                Path.Combine(basePath, "CognitiveQa.SourceIntel", "XamlQualityAnalyzer.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "L10nAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "CoverageGapWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "ArabicPayloads.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "BetaTesterAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "PentestSourceAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "EvidenceIndexWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "QaAutoAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "EvidencePackZipWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "ParityReportWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "OcrFallbackAdvisor.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "AuditSessionResultWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "PhaseVerifyLogger.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "PackageVulnerabilityAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Core", "SolutionLocator.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "ReportQualityWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "AuditHtmlDashboardWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "EvidenceArchiveWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "ReportAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "ChronicleIntelligenceAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "EmpatheticUxAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Meta", "CognitiveCouncilWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Meta", "IntelligenceBriefWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "AuditManifestComparer.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "AuditTrendAggregator.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "PerceptionFusionEngine.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "CognitivePerceptionAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Meta", "CognitivePerceptionWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Automation", "OcrTextReader.cs"),
                Path.Combine(basePath, "CognitiveQa.Meta", "HolisticClosureWriter.cs"),
                Path.Combine(basePath, "CognitiveQa.Auditors", "HolisticQualityAuditor.cs"),
                Path.Combine(basePath, "CognitiveQa.Reports", "NetworkCaptureManifestWriter.cs"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-supreme-gate.ps1"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-perception.ps1"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-full-gate.ps1"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-multi-target.ps1"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-compare.ps1"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-intelligence.ps1"),
                Path.Combine(repoRoot, "scripts", "ops", "sync-cognitive-status.ps1"),
                Path.Combine(repoRoot, ".github", "workflows", "cognitive-qa-dotnet.yml"),
                Path.Combine(repoRoot, "scripts", "ops", "run-cognitive-brqosh-audit.ps1"),
                Path.Combine(repoRoot, "CognitiveQa", "STATUS.yaml"),
                Path.Combine(basePath, "CognitiveQa.Desktop", "MainWindow.xaml"),
            };
        return required.All(File.Exists);
    }

    public static bool CognitiveQaPythonLegacyReady(string repoRoot)
    {
        var root = Path.Combine(repoRoot, "cognitive-qa");
        if (!Directory.Exists(root)) return false;
        var orch = Path.Combine(root, "cognitive_qa", "audit_orchestrator.py");
        var req = Path.Combine(root, "requirements.txt");
        return File.Exists(orch) && File.Exists(req);
    }

    public static bool CognitiveQaFrameworkReady(string repoRoot) =>
        CognitiveQaDotNetReady(repoRoot) || CognitiveQaPythonLegacyReady(repoRoot);

    public static bool PromptImplementationReleased(string repoRoot)
    {
        var law = Path.Combine(repoRoot, ".cursor", "skills", "omni-qa-army", "prompt-source-law.md");
        if (!File.Exists(law)) return false;
        var text = File.ReadAllText(law);
        return text.Contains("PS01-RELEASED", StringComparison.Ordinal)
               || text.Contains("implementation: active", StringComparison.OrdinalIgnoreCase);
    }

    public static bool KarpathySynthesisComplete(string repoRoot)
    {
        var synthesis = Path.Combine(repoRoot, ".cursor", "skills", "supreme-programming-mastery", "karpathy-synthesis.md");
        var index = Path.Combine(repoRoot, ".cursor", "skills", "brqosh-skills-hub", "leak-corpus-index.yaml");
        var dupRule = Path.Combine(repoRoot, ".cursor", "rules", "karpathy-guidelines.mdc");
        if (!File.Exists(synthesis) || !File.Exists(index)) return false;
        var indexText = File.ReadAllText(index);
        return indexText.Contains("Karpathy:", StringComparison.Ordinal) && !File.Exists(dupRule);
    }

    public static bool LeakSynthesisV4Ready(string repoRoot) =>
        File.Exists(Path.Combine(repoRoot, ".cursor", "skills", "brqosh-analysis", "shura-intelligence-core.md"))
        && KarpathySynthesisComplete(repoRoot)
        && File.Exists(Path.Combine(repoRoot, ".cursor", "skills", "ai-dev-platform", "SKILL.md"));

    public static bool PaeGovernanceReady(EvidenceBundle e) =>
        e.AuditEngineExists
        && PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot)
        && (e.AuditOverallPass || e.AuditStrengthScore >= 70);
}
