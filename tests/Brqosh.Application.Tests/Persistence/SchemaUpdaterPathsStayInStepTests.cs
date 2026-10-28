using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// ‏(مساران متوازيان للمخطط — شريحةٌ أولى آمنة) ‏كلُّ عمودٍ يُضمَن في PostgreSQL يُضمَن في SQLite أيضاً والعكس —
/// ‏فلا تنحرف قاعدتا المحرّكين بصمت. ‏اختبارٌ ساكنٌ يقرأ المحدّث نفسَه، بلا قاعدة بيانات.
/// </summary>
public class SchemaUpdaterPathsStayInStepTests
{
    private static readonly Regex Pg = new(@"EnsurePgColumnAsync\(\s*""(\w+)"",\s*""(\w+)""");
    private static readonly Regex Sqlite = new(@"EnsureSqliteColumn(?:IfTable)?Async\(\s*""(\w+)"",\s*""(\w+)""");

    [Fact]
    public void EveryEnsuredColumn_IsEnsuredOnBothEngines()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Brqosh.Infrastructure", "Persistence", "BrqoshSchemaUpdater.cs"));
        var pg = Pg.Matches(source).Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}").ToHashSet();
        var sqlite = Sqlite.Matches(source)
            .Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}")
            .ToHashSet();

        Assert.True(pg.Count > 100, $"لم يُقرأ إلا {pg.Count} عموداً — المسحُ أعمى.");
        Assert.Empty(pg.Except(sqlite).Order());
        Assert.Empty(sqlite.Except(pg).Order());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Brqosh.sln not found");
    }
}
