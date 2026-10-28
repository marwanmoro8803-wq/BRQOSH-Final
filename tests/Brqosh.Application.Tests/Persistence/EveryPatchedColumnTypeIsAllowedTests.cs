using System.Text.RegularExpressions;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (أسئلةُ المالك · قياسُ PostgreSQL) <b>كلُّ نوعِ عمودٍ يرقّعه <c>BrqoshSchemaUpdater</c> مسموحٌ في <c>SchemaSqlGuard</c>.</b>
///
/// <para><b>ما حدث:</b> أضفتُ عمودَ «اسم المدقق» بنوع <c>character varying(120) NULL</c> — والحارسُ قائمةٌ بيضاءُ بأنواعٍ حرفيّة، وليس فيها ١٢٠.
/// <b>فسقط كلُّ تشغيلٍ على PostgreSQL بـ«Unsupported PostgreSQL column type»</b> ولم يُظهره شيءٌ، لأنّ الاختبارات كلَّها على SQLite الذي لا يمرّ بهذا
/// المسار — وكشفه قياسُ الخمس سنوات على PostgreSQL فقط. والحارسُ يقرأ مصدرَ المُرقِّع فلا يُنسى نوعٌ جديد.</para>
/// </summary>
public class EveryPatchedColumnTypeIsAllowedTests
{
    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return SchemaUpdaterSourceFiles.ReadAll(dir!.FullName);
    }

    private static List<string> Types(string method)
    {
        var matches = Regex.Matches(Source(), method + @"\(\s*""[^""]+""\s*,\s*""[^""]+""\s*,\s*""([^""]+)""");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    [Fact]
    public void EveryPostgresColumnTheUpdaterAdds_PassesTheGuard()
    {
        var types = Types("EnsurePgColumnAsync");
        Assert.True(types.Count > 20, $"القارئُ وجد {types.Count} نوعاً فقط — أعمى.");

        var rejected = new List<string>();
        foreach (var type in types)
            try { SchemaSqlGuard.ValidatePgType(type); }
            catch (ArgumentException) { rejected.Add(type); }

        Assert.True(rejected.Count == 0, "أنواعٌ يرقّعها المُرقِّع ويرفضها الحارسُ على PostgreSQL:\n  " + string.Join("\n  ", rejected));
    }

    [Fact]
    public void EverySqliteColumnTheUpdaterAdds_PassesTheGuard()
    {
        var types = Types("EnsureSqliteColumnAsync");
        Assert.True(types.Count > 10, $"القارئُ وجد {types.Count} نوعاً فقط — أعمى.");

        var rejected = new List<string>();
        foreach (var type in types)
            try { SchemaSqlGuard.ValidateSqliteType(type); }
            catch (ArgumentException) { rejected.Add(type); }

        Assert.True(rejected.Count == 0, "أنواعٌ يرقّعها المُرقِّع ويرفضها الحارسُ على SQLite:\n  " + string.Join("\n  ", rejected));
    }
}
