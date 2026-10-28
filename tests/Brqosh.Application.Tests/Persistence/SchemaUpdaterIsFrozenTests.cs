using System.Text.RegularExpressions;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// ‏(مساران متوازيان للمخطط — تجميدُ المُرقِّع) ‏<see cref="BrqoshSchemaUpdater"/> مجمَّدٌ عند
/// ‏<see cref="BrqoshSchemaUpdater.FrozenAtMigration"/>: لا جدولَ ولا عمودَ جديداً فيه — فالمخطّطُ الجديدُ مصدرُه
/// ‏هجرةُ EF وحدها (<c>dotnet ef migrations add</c>)، ونموذجُ EF يُنشئه في SQLite بـ<c>EnsureCreated</c>.
/// ‏والحذفُ من المُرقِّع مسموح؛ والإضافةُ تسقط هنا باسمها. ‏اختبارٌ ساكنٌ بلا قاعدة بيانات.
/// </summary>
public class SchemaUpdaterIsFrozenTests
{
    [Fact]
    public void TheUpdaterAddsNoSchemaBeyondItsFrozenBaseline()
    {
        var root = RepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "Brqosh.Infrastructure", "Persistence", "BrqoshSchemaUpdater.cs"));
        var baseline = File.ReadAllLines(Path.Combine(root, "tests", "Brqosh.Application.Tests", "Persistence", "SchemaUpdaterFrozen.baseline.txt"))
            .Where(l => l.Length > 0).ToHashSet(StringComparer.Ordinal);

        var current = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(source, @"EnsurePgColumnAsync\(\s*""(\w+)"",\s*""(\w+)"""))
            current.Add($"column {m.Groups[1].Value}.{m.Groups[2].Value}");
        foreach (Match m in Regex.Matches(source, @"EnsureSqliteColumn(?:IfTable)?Async\(\s*""(\w+)"",\s*""(\w+)"""))
            current.Add($"column {m.Groups[1].Value}.{m.Groups[2].Value}");
        foreach (Match m in Regex.Matches(source, @"CREATE TABLE IF NOT EXISTS\s+""?(\w+)""?"))
            current.Add($"table {m.Groups[1].Value}");
        foreach (Match m in Regex.Matches(source, @"ALTER TABLE\s+""?(\w+)""?\s+ADD COLUMN\s+(?:IF NOT EXISTS\s+)?""?(\w+)""?"))
            current.Add($"alter {m.Groups[1].Value}.{m.Groups[2].Value}");

        Assert.True(current.Count > 150, $"لم يُقرأ إلا {current.Count} عنصراً — المسحُ أعمى.");
        var added = current.Except(baseline).Order(StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0,
            "‏المُرقِّعُ مجمَّد — المخطّطُ الجديدُ يُضاف بهجرة EF لا هنا:" + Environment.NewLine + string.Join(Environment.NewLine, added));
    }

    [Fact]
    public void TheFreezeMarker_NamesAnExistingMigration()
    {
        var migrations = typeof(BrqoshDbContext).Assembly.GetTypes()
            .Select(t => t.GetCustomAttributes(typeof(Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute), false)
                .Cast<Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute>().FirstOrDefault()?.Id)
            .Where(id => id is not null).ToHashSet();
        Assert.Contains(BrqoshSchemaUpdater.FrozenAtMigration, migrations);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Brqosh.sln not found");
    }
}
