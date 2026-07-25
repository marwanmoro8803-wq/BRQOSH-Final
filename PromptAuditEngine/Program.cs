namespace PromptAuditEngine;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repoRoot = FindRepoRoot() ?? Directory.GetCurrentDirectory();
        var skipBuild = args.Contains("--skip-build", StringComparer.OrdinalIgnoreCase);
        var watch = args.Contains("--watch", StringComparer.OrdinalIgnoreCase);
        var interval = ParseInterval(args);
        var targetApp = ParseTargetApp(args);

        if (!watch)
            return RunOnce(repoRoot, skipBuild, targetApp);

        Console.WriteLine($"Prompt Audit Engine — watch mode every {interval}s (Ctrl+C to stop)");
        while (true)
        {
            var code = RunOnce(repoRoot, skipBuild, targetApp);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] next run in {interval}s (last exit={code})");
            await Task.Delay(TimeSpan.FromSeconds(interval));
        }
    }

    private static int RunOnce(string repoRoot, bool skipBuild, string? targetApp)
    {
        Console.WriteLine("=== Prompt Audit Engine (universal) ===");
        Console.WriteLine($"Repo: {repoRoot}");
        if (!string.IsNullOrWhiteSpace(targetApp))
            Console.WriteLine($"Target app (stub): {targetApp}");

        var result = AuditEngineOrchestrator.Run(repoRoot, skipBuild, targetApp);
        var outDir = Path.Combine(repoRoot, "dist", "evidence", "prompt-audit");
        AuditReportWriter.Write(result, outDir);

        Console.WriteLine($"Stacks:   {string.Join(", ", result.Stacks.Select(s => s.Label))}");
        Console.WriteLine($"Linkage:  {result.Linkage.Score}/100");
        Console.WriteLine($"Workflow: {result.Workflow.Score}/100");
        Console.WriteLine($"Strength: {result.StrengthScore}/100");
        Console.WriteLine($"Result:   {(result.OverallPass ? "PASS" : "PARTIAL")}");
        Console.WriteLine($"Report:   {Path.Combine(outDir, "latest.md")}");

        return result.OverallPass ? 0 : 1;
    }

    private static int ParseInterval(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--interval" or "-i" && int.TryParse(args[i + 1], out var sec) && sec >= 30)
                return sec;
        }
        return 300;
    }

    private static string? ParseTargetApp(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--target-app" or "-t")
                return args[i + 1];
        }
        return null;
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "PROJECT.yaml"))
                || Directory.Exists(Path.Combine(dir, "PromptAuditEngine")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "PROJECT.yaml"))) return cwd;
        return null;
    }
}
