namespace UiAudit;

internal enum AuditOutcome
{
    Pass,
    Fail,
    Skip,
}

internal sealed class UiAuditResult
{
    private readonly List<(string Step, AuditOutcome Outcome, string Detail)> _items = [];

    public void Pass(string step, string detail) => _items.Add((step, AuditOutcome.Pass, detail));
    public void Fail(string step, string detail) => _items.Add((step, AuditOutcome.Fail, detail));
    public void Skip(string step, string detail) => _items.Add((step, AuditOutcome.Skip, detail));

    public void MergeFrom(UiAuditResult other)
    {
        foreach (var item in other.Items)
            _items.Add(item);
    }

    public int Total => _items.Count;
    public int Passed => _items.Count(i => i.Outcome == AuditOutcome.Pass);
    public int Failed => _items.Count(i => i.Outcome == AuditOutcome.Fail);
    public int Skipped => _items.Count(i => i.Outcome == AuditOutcome.Skip);

    public void Print(TextWriter? writer = null)
    {
        writer ??= Console.Out;
        writer.WriteLine();
        writer.WriteLine("--- نتائج التدقيق الشامل ---");
        foreach (var (step, outcome, detail) in _items)
        {
            var tag = outcome switch
            {
                AuditOutcome.Pass => "PASS",
                AuditOutcome.Fail => "FAIL",
                _ => "SKIP",
            };
            writer.WriteLine($"[{tag}] {step}: {detail}");
        }

        writer.WriteLine();
        if (Failed == 0)
            writer.WriteLine($"=== SUCCESS — {Passed} ناجح · {Skipped} متخطى · {_items.Count} فحص ===");
        else
            writer.WriteLine($"=== {Failed} فشل · {Passed} ناجح · {Skipped} متخطى من {_items.Count} فحص ===");
    }

    public void SaveReport(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        writer.WriteLine($"=== BRQOSH Live UI Audit — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        Print(writer);
    }

    public IReadOnlyList<(string Step, AuditOutcome Outcome, string Detail)> Items => _items;

    public int ExitCode => Failed == 0 ? 0 : 1;
}
