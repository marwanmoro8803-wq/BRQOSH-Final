namespace ShuraCouncil;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("ERROR: cannot locate BRQOSH repo root (Brqosh.sln).");
            return 2;
        }

        string? source = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--source" or "-s" && i + 1 < args.Length)
                source = args[++i];
        }

        if (source is null)
            source = Path.Combine(repoRoot, "تقارير", "CEEF-145_Live_UI_Audit_Marwan.md");

        if (!Path.IsPathRooted(source))
            source = Path.GetFullPath(Path.Combine(repoRoot, source));

        Console.WriteLine("=== BRQOSH Shura Council (executable) ===");
        Console.WriteLine($"Repo:   {repoRoot}");
        Console.WriteLine($"Source: {source}");
        Console.WriteLine();

        var evidence = EvidenceLoader.Load(repoRoot, File.Exists(source) ? source : null);
        var seats = SeatEvaluators.EvaluateAll(evidence);
        var result = ConsensusEngine.Run(evidence, seats);

        var outDir = Path.Combine(repoRoot, "dist", "evidence", "shura-council");
        Directory.CreateDirectory(outDir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var mdPath = Path.Combine(outDir, $"council-{stamp}.md");
        var jsonPath = Path.Combine(outDir, $"council-{stamp}.json");

        var sourceRel = Path.GetRelativePath(repoRoot, source).Replace('\\', '/');
        var markdown = MarkdownReportWriter.WriteMarkdown(result, sourceRel);
        File.WriteAllText(mdPath, markdown);
        MarkdownReportWriter.WriteJson(result, jsonPath);

        PrintSummary(result, mdPath, jsonPath);

        return result.OverallConsensus ? 0 : 1;
    }

    private static void PrintSummary(CouncilResult result, string mdPath, string jsonPath)
    {
        Console.WriteLine("--- Evidence ---");
        Console.WriteLine($"Tests:     {result.Evidence.TestsTotal} total, {result.Evidence.TestsFailed} failed");
        Console.WriteLine($"FlaUI:     {result.Evidence.UiAuditPassed}/{result.Evidence.UiAuditTotal} pass");
        Console.WriteLine($"Security:  P0={result.Evidence.SecurityP0} P1={result.Evidence.SecurityP1}");
        Console.WriteLine($"O-01:      {(result.Evidence.O01Open ? "OPEN" : "closed")}");
        Console.WriteLine();

        Console.WriteLine("--- Seats (12) ---");
        foreach (var seat in result.Seats)
        {
            var pass = seat.Findings.Count(f => f.Pass);
            Console.WriteLine($"  {seat.SeatId} {seat.RoleAr}: {pass}/{seat.Findings.Count} checks pass");
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
            if (File.Exists(Path.Combine(dir, "Brqosh.sln")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "Brqosh.sln")))
            return cwd;

        return null;
    }
}
