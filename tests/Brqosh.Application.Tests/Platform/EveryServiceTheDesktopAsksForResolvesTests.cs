using System.Text.RegularExpressions;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٥) <b>كلُّ خدمةٍ تطلبها الشاشاتُ تُحَلّ من الحاوية التي تبنيها الشاشاتُ نفسُها — وكانت أربعٌ لا تُحَلّ.</b>
///
/// <para><b>ما كان:</b> <c>BankStatementsPage</c> تطلب <c>BankStatementImportService</c> و<c>BankStatementMatcherService</c>،
/// و<c>CashPositionPage</c> تطلب <c>CashMovementReportService</c> — بـ<c>DesktopAccountingFacade.GetService&lt;T&gt;()</c>، أي
/// <c>GetRequiredService</c>. <b>والثلاثُ غيرُ مسجَّلةٍ في <c>AddBrqoshInfrastructure</c></b>: تُبنى بـ<c>new</c> داخل محوِّلات حالات
/// الاستخدام وحدَها. <b>ورابعةٌ وجدها هذا الحارسُ نفسُه</b>: <c>DataMigrationPage</c> تطلب <c>CsvDataMigrationService</c>، وهي مسجَّلةٌ
/// <b>بواجهتها</b> (<c>IDataMigrationService</c>) لا بصنفها — وقراءتي النصّيّةُ للتسجيل عدّتها مسجَّلةً. فزرُّ استيراد كشف البنك
/// وزرُّ المطابقة الآليّة وشاشةُ مركز النقد وشاشةُ ترحيل البيانات <b>كلُّها ترمي «No service for type» وتعرض التحذيرَ وحدَه</b> —
/// والتعليقان في <c>CashPositionPage</c> و<c>DataMigrationPage</c> يقولان «مسجَّلة».</para>
///
/// <para><b>وهو الصنفُ نفسُه الذي أُغلق مرّةً في ط6-٥</b> (<c>EndOfServiceAccrualService</c> بلا تسجيل) — أُصلحت الحالةُ ولم يُحرس
/// الصنف. <b>فالحارسُ هنا يقرأ الطلبَ من مصدر الشاشات ويُحلّه من الحاوية الحقيقيّة</b>: لا قائمةَ يدويّةً تنسى جديداً.</para>
/// </summary>
public class EveryServiceTheDesktopAsksForResolvesTests
{
    private static readonly Regex Request = new(@"GetService<([A-Za-z0-9_.]+)>", RegexOptions.Compiled);

    [Fact]
    public void EveryServiceTheDesktopAsksFor_ResolvesFromTheRealContainer()
    {
        var requested = RequestedByTheDesktop();

        // ‏**شاهدُ ضبطٍ موجب**: الماسحُ يرى الشاشات — ٨٨ نوعاً يوم كُتب؛ وماسحٌ أعمى يُخضِّر الحارسَ بلا شيء.
        Assert.True(requested.Count >= 80, $"‏الماسحُ رأى {requested.Count} نوعاً فقط — هل تغيّر مسارُ المصدر؟");

        var types = typeof(BrqoshDbContext).Assembly.GetTypes()
            .Concat(typeof(Brqosh.Application.ApplicationServiceCollectionExtensions).Assembly.GetTypes())
            .Where(t => t.IsPublic || t.IsNestedPublic)
            .ToList();

        using var provider = BuildInfrastructure();
        using var scope = provider.CreateScope();

        var unresolved = new List<string>();
        foreach (var name in requested.OrderBy(n => n, StringComparer.Ordinal))
        {
            var simple = name[(name.LastIndexOf('.') + 1)..];
            var matches = types.Where(t => t.Name == simple).ToList();
            if (matches.Count != 1)
            {
                unresolved.Add($"{name} — {matches.Count} نوعاً بهذا الاسم");
                continue;
            }

            if (scope.ServiceProvider.GetService(matches[0]) is null)
                unresolved.Add($"{name} — غيرُ مسجَّل: GetRequiredService يرمي والزرُّ لا يعمل");
        }

        Assert.True(unresolved.Count == 0,
            "‏خدماتٌ تطلبها الشاشاتُ ولا تُحَلّ من AddBrqoshInfrastructure:" + Environment.NewLine
            + string.Join(Environment.NewLine, unresolved));
    }

    private static HashSet<string> RequestedByTheDesktop()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");
        Assert.True(Directory.Exists(root), $"‏مصدرُ الشاشات غيرُ موجود: {root}");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains($"{sep}obj{sep}") || file.Contains($"{sep}bin{sep}"))
                continue;
            foreach (Match m in Request.Matches(File.ReadAllText(file)))
                names.Add(m.Groups[1].Value);
        }

        // ‏`GetService<T>` في الواجهة نفسِها ليس طلباً.
        names.Remove("T");
        return names;
    }

    private static ServiceProvider BuildInfrastructure()
    {
        var services = new ServiceCollection();
        services.AddBrqoshInfrastructure(
            $"Data Source={Path.Combine(Path.GetTempPath(), $"brqosh-resolve-{Guid.NewGuid():N}.db")}",
            usePostgres: false);
        return services.BuildServiceProvider();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
