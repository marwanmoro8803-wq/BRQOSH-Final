namespace ShuraCouncil;

/// <summary>Evaluates 28 programming-discipline seats against repo evidence (Shura v5).</summary>
internal static class ProgrammingDisciplineEvaluators
{
    internal sealed record DisciplineCouncilReport(
        int Total,
        int Ready,
        int Percent,
        IReadOnlyList<SeatVerdict> Seats,
        string SummaryAr,
        string SummaryEn);

    public static DisciplineCouncilReport EvaluateAll(EvidenceBundle e)
    {
        var seats = ProgrammingDisciplineRegistry.All
            .Select(d => EvaluateDiscipline(e, d))
            .ToList();

        var ready = seats.Count(s => s.Findings.All(f => f.Pass) || s.Findings.Count(f => f.Pass) >= s.Findings.Count - 1);
        var pct = seats.Count > 0 ? (int)Math.Round(100.0 * ready / seats.Count) : 0;
        var summaryAr = $"{ready}/{seats.Count} تخصصات جاهزة ({pct}%) — مجلس التخصصات البرمجية v5";
        var summaryEn = $"{ready}/{seats.Count} disciplines ready ({pct}%) — Programming Discipline Council v5";

        return new(seats.Count, ready, pct, seats, summaryAr, summaryEn);
    }

    private static SeatVerdict EvaluateDiscipline(EvidenceBundle e, ProgrammingDisciplineRegistry.DisciplineDef def)
    {
        var findings = new List<SeatFinding>();

        var filesFound = CountMatchingFiles(e.RepoRoot, def.FileGlobs, maxDepth: 6);
        findings.Add(new($"{def.SeatId}-FILES", filesFound > 0 || def.FileGlobs.Length == 0,
            filesFound > 0 ? $"{filesFound} ملف مطابق" : "لا ملفات في المستودع (غير مطلوب)"));

        var markersFound = CountPathMarkers(e.RepoRoot, def.PathMarkers);
        findings.Add(new($"{def.SeatId}-MARKERS", markersFound > 0 || def.PathMarkers.Length == 0,
            markersFound > 0 ? $"{markersFound} مسار/علامة" : "غير مطبّق في هذا المستودع"));

        var skillsFound = CountExistingSkills(e.RepoRoot, def.SkillModules);
        findings.Add(new($"{def.SeatId}-SKILLS", skillsFound > 0,
            $"{skillsFound}/{def.SkillModules.Length} skill modules"));

        if (def.SeatId is "D01" or "D17")
            findings.Add(new($"{def.SeatId}-TESTS", e.TestsFailed == 0 && e.TestsTotal > 0,
                $"اختبارات {e.TestsTotal - e.TestsFailed}/{e.TestsTotal}"));

        if (def.SeatId is "D02")
            findings.Add(new($"{def.SeatId}-PYTHON", Directory.Exists(Path.Combine(e.RepoRoot, "cognitive-qa")),
                "cognitive-qa/ present"));

        if (def.SeatId is "D11")
            findings.Add(new($"{def.SeatId}-CI", Directory.Exists(Path.Combine(e.RepoRoot, ".github", "workflows")),
                "GitHub workflows"));

        if (def.SeatId is "D16")
            findings.Add(new($"{def.SeatId}-UIAUDIT", e.UiAuditTotal > 0 && e.UiAuditFailed == 0,
                $"FlaUI {e.UiAuditPassed}/{e.UiAuditTotal}"));

        if (def.SeatId is "D13" or "D28")
            findings.Add(new($"{def.SeatId}-SHURA", e.ShuraToolExists,
                "ShuraCouncil harness"));

        if (def.SeatId is "D19" or "D28")
            findings.Add(new($"{def.SeatId}-LEAKS", e.LeakCorpusExists,
                $"leak corpus {e.LeakCorpusFiles} files"));

        var passCount = findings.Count(f => f.Pass);
        var summary = passCount == findings.Count
            ? $"✓ {def.RoleEn} — discipline ready"
            : $"◐ {def.RoleEn} — {passCount}/{findings.Count} checks";

        return new SeatVerdict(
            def.SeatId,
            def.RoleAr,
            findings,
            new Dictionary<string, VoteKind>(),
            summary);
    }

    private static int CountMatchingFiles(string repoRoot, string[] globs, int maxDepth)
    {
        if (globs.Length == 0)
            return 0;

        var count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(repoRoot, "*.*", SearchOption.AllDirectories))
            {
                if (ShouldSkipDir(file, repoRoot, maxDepth))
                    continue;

                var name = Path.GetFileName(file);
                var rel = file.Replace('\\', '/');
                foreach (var glob in globs)
                {
                    if (glob.StartsWith('*') && name.EndsWith(glob.TrimStart('*'), StringComparison.OrdinalIgnoreCase))
                    { count++; break; }
                    if (glob.Contains('*') && name.Contains(glob.Trim('*'), StringComparison.OrdinalIgnoreCase))
                    { count++; break; }
                    if (rel.Contains(glob.Trim('*'), StringComparison.OrdinalIgnoreCase))
                    { count++; break; }
                }
                if (count > 50)
                    break;
            }
        }
        catch
        {
            // ignore permission errors
        }

        return count;
    }

    private static int CountPathMarkers(string repoRoot, string[] markers)
    {
        if (markers.Length == 0)
            return 0;

        var found = 0;
        foreach (var marker in markers)
        {
            if (string.IsNullOrWhiteSpace(marker))
                continue;

            try
            {
                if (Directory.Exists(Path.Combine(repoRoot, marker.TrimEnd('/'))))
                { found++; continue; }

                var hits = Directory.EnumerateFiles(repoRoot, "*.*", SearchOption.AllDirectories)
                    .Take(5000)
                    .Any(p => p.Replace('\\', '/').Contains(marker, StringComparison.OrdinalIgnoreCase));
                if (hits)
                    found++;
            }
            catch
            {
                // skip
            }
        }

        return found;
    }

    private static int CountExistingSkills(string repoRoot, string[] skillModules)
    {
        var skillsRoot = Path.Combine(repoRoot, ".cursor", "skills");
        if (!Directory.Exists(skillsRoot))
            return 0;

        var found = 0;
        foreach (var mod in skillModules)
        {
            var normalized = mod.Replace('\\', '/');
            var skillName = normalized.Split('/')[0];
            var skillPath = Path.Combine(skillsRoot, skillName, "SKILL.md");
            if (File.Exists(skillPath))
                found++;
            else
            {
                var alt = Path.Combine(skillsRoot, normalized);
                if (File.Exists(alt) || Directory.Exists(Path.GetDirectoryName(alt) ?? ""))
                    found++;
            }
        }

        return found;
    }

    private static bool ShouldSkipDir(string filePath, string repoRoot, int maxDepth)
    {
        var rel = Path.GetRelativePath(repoRoot, filePath);
        if (rel.Contains("node_modules", StringComparison.OrdinalIgnoreCase)
            || rel.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
            || rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || rel.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            || rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || rel.Contains(".venv", StringComparison.OrdinalIgnoreCase))
            return true;

        var depth = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
        return depth > maxDepth + 3;
    }
}
