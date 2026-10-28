using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// ‏رموزُ الحسابات في خدمات الترحيل تأتي من <c>SaudiChartOfAccounts</c> (SystemCodes/PostingRoleCodes) — لا حرفيّاً،
/// ‏فتعديلُ الدليل يُغيَّر في موضعٍ واحد. ‏اختبارٌ ساكنٌ يتجاهل التعليقات.
/// </summary>
public class NoLiteralAccountCodesInPostingTests
{
    private static readonly Regex Literal = new(@"""[1-5]\d{3}""", RegexOptions.Compiled);

    [Fact]
    public void PostingServices_UseNamedAccountCodes()
    {
        var infra = Path.Combine(RepoRoot(), "src", "Brqosh.Infrastructure");
        var files = Directory.EnumerateFiles(infra, "*PostingService.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(infra, "Payroll"), "EndOfService*.cs"))
            .Concat(Directory.EnumerateFiles(Path.Combine(infra, "Manufacturing"), "*.cs"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.True(files.Count >= 15, $"لم يُقرأ إلا {files.Count} ملفاً — المسحُ أعمى.");

        var hits = files.SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (line, i))
                .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                            && Literal.IsMatch(x.line.Split("//")[0]))
                .Select(x => $"{Path.GetFileName(f)}:{x.i + 1}: {x.line.Trim()}"))
            .ToList();
        Assert.True(hits.Count == 0, "‏رموزُ حساباتٍ حرفيّة — استعمل ثوابت SaudiChartOfAccounts:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Brqosh.sln not found");
    }
}
