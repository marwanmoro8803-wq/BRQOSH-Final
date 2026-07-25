namespace ShuraCouncil;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("ERROR: cannot locate repo root (Brqosh.sln or PROJECT.yaml or ShuraCouncil/).");
            return 2;
        }

        string? source = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--source" or "-s" && i + 1 < args.Length)
                source = args[++i];
        }

        if (source is null)
        {
            var promptCeef = Path.Combine(repoRoot, "dist", "evidence", "prompt-governance", "CEEF-PROMPT-001.md");
            source = File.Exists(promptCeef)
                ? promptCeef
                : Path.Combine(repoRoot, "تقارير", "CEEF-145_Live_UI_Audit_Marwan.md");
        }

        if (!Path.IsPathRooted(source))
            source = Path.GetFullPath(Path.Combine(repoRoot, source));

        Console.WriteLine("=== BRQOSH Shura Council v5 Supreme + Disciplines ===");
        Console.WriteLine($"Repo:   {repoRoot}");
        Console.WriteLine($"Source: {source}");
        Console.WriteLine($"Disciplines: {ProgrammingDisciplineRegistry.Count} seats (D01–D{ProgrammingDisciplineRegistry.Count:D2})");
        Console.WriteLine();

        var evidence = EvidenceLoader.Load(repoRoot, File.Exists(source) ? source : null);
        var seats = SeatEvaluators.EvaluateAll(evidence);
        var result = ConsensusEngine.Run(evidence, seats);
        var disciplines = ProgrammingDisciplineEvaluators.EvaluateAll(evidence);

        var outDir = Path.Combine(repoRoot, "dist", "evidence", "shura-council");
        Directory.CreateDirectory(outDir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var mdPath = Path.Combine(outDir, $"council-{stamp}.md");
        var jsonPath = Path.Combine(outDir, $"council-{stamp}.json");

        var sourceRel = Path.GetRelativePath(repoRoot, source).Replace('\\', '/');
        var markdown = MarkdownReportWriter.WriteMarkdown(result, sourceRel, disciplines);
        File.WriteAllText(mdPath, markdown);
        MarkdownReportWriter.WriteJson(result, jsonPath, disciplines);

        PrintSummary(result, disciplines, mdPath, jsonPath);

        return result.OverallConsensus ? 0 : 1;
    }

    private static void PrintSummary(
        CouncilResult result,
        ProgrammingDisciplineEvaluators.DisciplineCouncilReport disciplines,
        string mdPath,
        string jsonPath)
    {
        Console.WriteLine("--- Evidence ---");
        if (result.Evidence.Workspace == WorkspaceKind.ProgramDeveloper)
        {
            Console.WriteLine($"Workspace: Program Developer (prompt governance)");
            Console.WriteLine($"Prompt file: {(result.Evidence.PromptTxtExists ? "yes" : "no")}");
            Console.WriteLine($"omni-qa:     {result.Evidence.OmniQaSkillModules} modules, {result.Evidence.OmniQaRoleModules} roles");
            Console.WriteLine($"Prompt code: {(result.Evidence.PromptCodeAbsent ? "absent (OK)" : "PRESENT")}");
            Console.WriteLine($"PAE:       engine={(result.Evidence.AuditEngineExists ? "yes" : "no")} strength={result.Evidence.AuditStrengthScore}% pass={result.Evidence.AuditOverallPass}");
            Console.WriteLine($"Leak corpus: {result.Evidence.LeakCorpusFiles} files");
        }
        else
        {
            Console.WriteLine($"Tests:     {result.Evidence.TestsTotal} total, {result.Evidence.TestsFailed} failed");
            Console.WriteLine($"FlaUI:     {result.Evidence.UiAuditPassed}/{result.Evidence.UiAuditTotal} pass");
            Console.WriteLine($"Security:  P0={result.Evidence.SecurityP0} P1={result.Evidence.SecurityP1}");
            Console.WriteLine($"O-01:      {(result.Evidence.O01Open ? "OPEN" : "closed")}");
        }
        Console.WriteLine();

        Console.WriteLine("--- Seats (12) ---");
        foreach (var seat in result.Seats)
        {
            var pass = seat.Findings.Count(f => f.Pass);
            Console.WriteLine($"  {seat.SeatId} {seat.RoleAr}: {pass}/{seat.Findings.Count} checks pass");
        }
        Console.WriteLine();

        Console.WriteLine($"--- Discipline Council ({disciplines.Total}) ---");
        Console.WriteLine($"  {disciplines.SummaryEn}");
        foreach (var group in disciplines.Seats.GroupBy(s =>
        {
            var def = ProgrammingDisciplineRegistry.Get(s.SeatId);
            return def?.Category ?? "Other";
        }).OrderBy(g => g.Key))
        {
            var ready = group.Count(s => s.Findings.All(f => f.Pass) || s.Findings.Count(f => f.Pass) >= s.Findings.Count - 1);
            Console.WriteLine($"  [{group.Key}] {ready}/{group.Count()} ready");
        }
        Console.WriteLine();

        Console.WriteLine("--- Decisions ---");
        foreach (var d in result.Decisions)
        {
            var mark = d.ConsensusMet ? "OK" : "FAIL";
            Console.WriteLine($"  {d.Id} [{mark}] ✅{d.Approve} 🟡{d.Conditional} ❌{d.Reject} — {d.TitleAr}");
        }
        Console.WriteLine();

        Console.WriteLine($"Overall consensus: {(result.OverallConsensus ? "APPROVED" : "NOT MET")}");
        Console.WriteLine($"Supreme v5:        {(result.SupremeGate.AllGatesPass ? "CERTIFIED" : "PARTIAL")} — {result.SupremeGate.CertificationSummary}");
        Console.WriteLine($"Leak synthesis:    {result.LeakSynthesis.IntegrityScore}/100 · {result.LeakSynthesis.FamiliesPresent.Count}/{LeakSynthesisEngine.FamilyCount} families");
        Console.WriteLine($"Facilitator: {result.Facilitator.SummaryAr}");
        Console.WriteLine($"Adversarial: {result.AdversarialVerdicts.Count(a => a.Survived)}/{result.AdversarialVerdicts.Count} claims survived");
        Console.WriteLine($"Conflicts resolved: {result.ConflictResolutions.Count}");
        Console.WriteLine($"Report: {mdPath}");
        Console.WriteLine($"JSON:   {jsonPath}");
        Console.WriteLine();
        Console.WriteLine("Verdict:");
        Console.WriteLine(result.UnifiedVerdictAr);
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Brqosh.sln"))
                || File.Exists(Path.Combine(dir, "ShuraCouncil", "ShuraCouncil.csproj"))
                || File.Exists(Path.Combine(dir, "PROJECT.yaml")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "Brqosh.sln"))
            || File.Exists(Path.Combine(cwd, "ShuraCouncil", "ShuraCouncil.csproj"))
            || File.Exists(Path.Combine(cwd, "PROJECT.yaml")))
            return cwd;

        return null;
    }
}
