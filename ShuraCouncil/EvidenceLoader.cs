using System.Globalization;
using System.Text.RegularExpressions;

namespace ShuraCouncil;

internal static class EvidenceLoader
{
    public static EvidenceBundle Load(string repoRoot, string? sourceCeefPath)
    {
        var paths = new List<string>();
        if (sourceCeefPath is not null && File.Exists(sourceCeefPath))
            paths.Add(sourceCeefPath);

        var statusPath = Path.Combine(repoRoot, ".cursor", "skills", "brqosh-skills-hub", "STATUS.yaml");
        var (testsTotal, testsFailed) = ParseStatusTests(statusPath);
        if (File.Exists(statusPath)) paths.Add(statusPath);

        var secPath = Path.Combine(repoRoot, "dist", "evidence", "CEEF-138-supreme-audit", "security-findings.csv");
        var (p0, p1) = ParseSecurityCsv(secPath);
        if (File.Exists(secPath)) paths.Add(secPath);

        var marketPath = Path.Combine(repoRoot, "dist", "evidence", "CEEF-138-supreme-audit", "market-gap-verified.csv");
        var zeroCode = ParseMarketZero(marketPath);
        var (g31, g32) = ParseG31G32(marketPath);
        if (File.Exists(marketPath)) paths.Add(marketPath);

        var openItems = Path.Combine(repoRoot, "docs", "OPEN_ITEMS.md");
        var o01 = File.Exists(openItems) && File.ReadAllText(openItems).Contains("O-01", StringComparison.Ordinal);
        if (File.Exists(openItems)) paths.Add(openItems);

        var uiLog = FindLatestUiAuditLog(repoRoot);
        var (uiTotal, uiPass, uiFail, uiFound) = ParseUiAuditLog(uiLog);
        if (uiFound && uiLog is not null) paths.Add(uiLog);

        var scenarioLog = Path.Combine(repoRoot, "dist", "evidence", "CEEF-138-supreme-audit", "full-scenario-audit.log");
        var scenarioPass = File.Exists(scenarioLog) &&
                           File.ReadAllText(scenarioLog).Contains("SUCCESS", StringComparison.OrdinalIgnoreCase);
        if (File.Exists(scenarioLog)) paths.Add(scenarioLog);

        var nightly = File.Exists(Path.Combine(repoRoot, "scripts", "ops", "run-ui-audit-nightly.ps1"));
        var shuraExists = Directory.Exists(Path.Combine(repoRoot, "tools", "ShuraCouncil"))
                          || Directory.Exists(Path.Combine(repoRoot, "ShuraCouncil"));

        var i18n = CountI18nArabicLines(repoRoot);

        var workspace = WorkspaceKind.Brqosh;
        var promptTxt = false;
        var promptAnalysis = false;
        var promptLaw = false;
        var omniQaSkills = 0;
        var omniQaRoles = 0;
        var promptCodeAbsent = true;
        var leakExists = false;
        var leakFiles = 0;
        var auditEngine = false;
        var auditFresh = false;
        var auditStrength = 0;
        var auditPass = false;
        var aiCouncil = false;
        var chronicle = false;
        var platformBridge = false;
        PromptGovernanceEvidence.Enrich(repoRoot, paths, ref workspace, ref promptTxt, ref promptAnalysis,
            ref promptLaw, ref omniQaSkills, ref omniQaRoles, ref promptCodeAbsent, ref leakExists, ref leakFiles,
            ref auditEngine, ref auditFresh, ref auditStrength, ref auditPass,
            ref aiCouncil, ref chronicle, ref platformBridge);

        return new EvidenceBundle(
            repoRoot,
            sourceCeefPath,
            testsTotal,
            testsFailed,
            p0,
            p1,
            zeroCode,
            o01,
            g31,
            g32,
            uiTotal,
            uiPass,
            uiFail,
            uiFound,
            scenarioPass,
            nightly,
            shuraExists,
            i18n,
            workspace,
            promptTxt,
            promptAnalysis,
            promptLaw,
            omniQaSkills,
            omniQaRoles,
            promptCodeAbsent,
            leakExists,
            leakFiles,
            auditEngine,
            auditFresh,
            auditStrength,
            auditPass,
            aiCouncil,
            chronicle,
            platformBridge,
            paths);
    }

    private static (int total, int failed) ParseStatusTests(string path)
    {
        if (!File.Exists(path)) return (0, -1);
        var text = File.ReadAllText(path);
        var total = Regex.Match(text, @"tests:\s*\r?\n\s*total:\s*(\d+)", RegexOptions.Multiline);
        var failed = Regex.Match(text, @"failed:\s*(\d+)", RegexOptions.Multiline);
        return (
            total.Success ? int.Parse(total.Groups[1].Value) : 0,
            failed.Success ? int.Parse(failed.Groups[1].Value) : -1);
    }

    private static (int p0, int p1) ParseSecurityCsv(string path)
    {
        if (!File.Exists(path)) return (0, 0);
        var p0 = 0;
        var p1 = 0;
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("AUDIT-SEC-", StringComparison.Ordinal)) continue;
            if (line.Contains("Code+Test", StringComparison.Ordinal)
                || line.Contains("✅ CLOSED", StringComparison.Ordinal))
                continue;
            if (line.Contains(",P0,", StringComparison.Ordinal)) p0++;
            else if (line.Contains(",P1,", StringComparison.Ordinal)) p1++;
        }

        return (p0, p1);
    }

    private static int ParseMarketZero(string path)
    {
        if (!File.Exists(path)) return 0;
        return File.ReadAllLines(path).Count(l => l.Contains(",ZERO,", StringComparison.OrdinalIgnoreCase) ||
                                                 l.Contains(",❌ ZERO", StringComparison.Ordinal));
    }

    private static (bool g31, bool g32) ParseG31G32(string path)
    {
        if (!File.Exists(path)) return (true, true);
        var g31Zero = true;
        var g32Zero = true;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("G-31,", StringComparison.Ordinal))
                g31Zero = line.Contains("ZERO", StringComparison.OrdinalIgnoreCase)
                          && !line.Contains("MVP", StringComparison.OrdinalIgnoreCase);
            if (line.StartsWith("G-32,", StringComparison.Ordinal))
                g32Zero = line.Contains("ZERO", StringComparison.OrdinalIgnoreCase)
                          && !line.Contains("MVP", StringComparison.OrdinalIgnoreCase);
        }

        return (g31Zero, g32Zero);
    }

    private static string? FindLatestUiAuditLog(string repoRoot)
    {
        var dir = Path.Combine(repoRoot, "dist", "evidence", "live-ui-audit");
        if (!Directory.Exists(dir)) return null;

        var candidates = Directory.GetFiles(dir, "*.txt")
            .Where(p =>
            {
                var name = Path.GetFileName(p);
                return name.StartsWith("audit-", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("nightly-", StringComparison.OrdinalIgnoreCase);
            })
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.Name.Contains("system83", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.Name.Contains("combined", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => ParseUiAuditTotal(f.FullName))
            .ThenByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        foreach (var file in candidates)
        {
            var text = TryReadAllTextShared(file.FullName);
            if (text is null) continue;
            if (HasUiAuditSummary(text) || file.Name.StartsWith("audit-", StringComparison.OrdinalIgnoreCase))
                return file.FullName;
        }

        return candidates.FirstOrDefault()?.FullName;
    }

    private static bool HasUiAuditSummary(string text) =>
        Regex.IsMatch(text, @"===\s*\d+\s*فشل\s*من\s*\d+\s*فحص\s*===");

    private static (int total, int pass, int fail, bool found) ParseUiAuditLog(string? path)
    {
        if (path is null || !File.Exists(path))
            return (0, 0, 0, false);

        var text = TryReadAllTextShared(path);
        if (text is null)
            return (0, 0, 0, false);
        var summary = Regex.Match(text, @"===\s*(\d+)\s*فشل\s*من\s*(\d+)\s*فحص\s*===");
        if (summary.Success)
        {
            var fail = int.Parse(summary.Groups[1].Value);
            var total = int.Parse(summary.Groups[2].Value);
            return (total, total - fail, fail, true);
        }

        var pass = Regex.Matches(text, @"\[PASS\]").Count;
        var failCount = Regex.Matches(text, @"\[FAIL\]").Count;
        var totalCount = pass + failCount;
        return totalCount > 0 ? (totalCount, pass, failCount, true) : (0, 0, 0, true);
    }

    private static int ParseUiAuditTotal(string path)
    {
        var text = TryReadAllTextShared(path);
        if (text is null)
            return 0;
        var summary = Regex.Match(text, @"===\s*\d+\s*فشل\s*من\s*(\d+)\s*فحص\s*===");
        if (summary.Success && int.TryParse(summary.Groups[1].Value, out var total))
            return total;
        return Regex.Matches(text, @"\[(PASS|FAIL)\]").Count;
    }

    private static string? TryReadAllTextShared(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static int CountI18nArabicLines(string repoRoot)
    {
        var desktop = Path.Combine(repoRoot, "src", "Brqosh.Desktop");
        if (!Directory.Exists(desktop)) return 0;
        var arabic = new Regex(@"[\u0600-\u06FF]");
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(desktop, "*.xaml", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (arabic.IsMatch(line) && !line.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
                    count++;
            }
        }

        return count;
    }
}
