using System.Text.RegularExpressions;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Npgsql;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (م-٦ · تبعيّةُ ن-٢٢) <b>استعلاماتُ كتيّب الفحص تُشغَّل فعلاً — لا تُنشر وتُترك.</b>
///
/// <para><b>ولماذا وُلدت هذه البوّابة:</b> نُشر في
/// <c>docs/ops/PROBE-EXPOSURE-CHECK.md</c> أربعةُ استعلاماتٍ يفحص بها المُشغِّلُ ما تركته
/// أدواتُ التشغيل في دفاتره — <b>وأوّلُ صياغةٍ كتبتُها كانت بجدولٍ لا وجودَ له</b>
/// (<c>users</c> بدل <c>app_users</c>) <b>وبأعمدةٍ بصيغةٍ خاطئة</b> (‏<c>snake_case</c>
/// والمخطَّطُ <c>"PascalCase"</c> مقتبسة). فكان المُشغِّلُ سيفتح الصفحةَ في لحظةِ حاجةٍ
/// <b>فيقابله خطأُ صياغةٍ بدل جواب</b> — <b>واستعلامٌ منشورٌ لا يعمل أسوأُ من لا استعلام</b>،
/// لأنّ الأوّلَ يُستَنَد إليه في التخطيط.</para>
///
/// <para><b>والمصدرُ واحد:</b> لا تُنسخ الاستعلاماتُ هنا — <b>تُقرأ من الصفحة نفسِها</b>
/// وتُنفَّذ على قاعدةِ PostgreSQL اختباريّةٍ بالمخطَّط الكامل. فتغييرُ اسمِ عمودٍ في النموذج
/// <b>يُحمّر البناءَ</b> بدل أن يكتشفه المُشغِّلُ ساعةَ الحادثة. <i>(وهو النمطُ نفسُه الذي
/// فرضته ن-٢١: العددُ يُطبع وقتَ التشغيل ولا يُكتب في نثر.)</i></para>
///
/// <para><b>وحدُّها مكتوبٌ فيها:</b> تُثبت أنّ الاستعلاماتِ <b>تُنفَّذ على المخطَّط</b> —
/// أسماءُ الجداول والأعمدة والصياغةُ سليمة. <b>ولا تُثبت أنّ صفوفاً موجودةٌ في دفاترِ
/// مُشغِّلٍ بعينه</b>: القاعدةُ هنا نظيفةٌ وفارغةٌ من أثر تلك الأدوات، <b>وهذا مقصود</b> —
/// فاختبارٌ يقرأ دفاترَ صاحبه هو العطلُ الذي كُتب <c>PostgresTestDatabase</c> لمنعه.</para>
/// </summary>
public class TheExposureRunbookQueriesActuallyRunTests
{
    private const string Runbook = "docs/ops/PROBE-EXPOSURE-CHECK.md";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    /// <summary>
    /// ‏كتلُ <c>```sql</c> وحدَها — <b>وكتلُ PowerShell تُترك عمداً</b>، فهي ليست SQL
    /// ولا تُنفَّذ هنا.
    /// </summary>
    private static List<string> SqlBlocks()
    {
        var path = Path.Combine(RepoRoot(), Runbook.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"‏كتيّبُ الفحص مفقود: {Runbook}");

        return Regex
            .Matches(File.ReadAllText(path), "```sql\\r?\\n(.*?)```", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(sql => sql.Length > 0)
            .ToList();
    }

    /// <summary>أوّلُ كلمةٍ فعليّةٍ في الكتلة — والتعليقاتُ تُتخطّى.</summary>
    private static string FirstKeyword(string sql) =>
        sql.Split('\n')
           .Select(line => line.Trim())
           .FirstOrDefault(line => line.Length > 0 && !line.StartsWith("--", StringComparison.Ordinal))
           ?.Split(' ', '(')[0]
           .ToUpperInvariant()
        ?? "";

    /// <summary>
    /// ‏الكتلُ التي تُنفَّذ: قراءةٌ محضة.
    ///
    /// <para><b>و<c>ALTER</c> لا تُنفَّذ عمداً</b> — كتلةُ تدوير كلمة المرور
    /// (<c>ALTER ROLE … PASSWORD</c>) <b>لو نُفِّذت على خادم الاختبار لغيّرت كلمةَ دورٍ
    /// حقيقيّ</b>، أو لحمّرت البناءَ لأنّ الدورَ لا وجودَ له. <b>وهذا هو الشكلُ الوحيدُ
    /// المسموحُ بتخطّيه</b>، والاختبار أدناه يجزم بذلك — فكتلةُ <c>DROP</c> أو
    /// <c>UPDATE</c> تُضاف غداً <b>تُسقط البوّابةَ حتى يُعلَن أمرُها</b>، ولا تُتخطّى
    /// صامتةً.</para>
    /// </summary>
    private static readonly string[] ExecutableKeywords = ["SELECT", "SET", "WITH"];

    /// <summary>الشكلُ الوحيدُ المُعلَنُ الذي يُتخطّى — وكلُّ ما عداه يُسقط البوّابة.</summary>
    private static readonly string[] DeclaredNonExecutable = ["ALTER"];

    /// <summary>
    /// <b>شاهدُ ضبطٍ من الصنف نفسِه، ويجري بلا خادم.</b> القارئُ يجد كتلاً حقيقيّةً
    /// وفيها ما يُميّزها. <b>وبلا هذا</b> كان مسارٌ مكسورٌ أو سياجٌ أُعيدت تسميته يجعل
    /// الاختبارَ أدناه يمرّ على <b>قائمةٍ فارغة</b> — <b>وهو ن-٥٣ بعينه: بوّابةٌ تقول PASS
    /// لأنّها لم تفحص شيئاً</b>.
    /// </summary>
    [Fact]
    public void TheRunbookActuallyCarriesQueries()
    {
        var blocks = SqlBlocks();

        Assert.True(blocks.Count >= 4, $"‏وُجدت {blocks.Count} كتلةَ SQL فقط في {Runbook}.");
        Assert.Contains(blocks, sql => sql.Contains("app_users", StringComparison.Ordinal));
        Assert.Contains(blocks, sql => sql.Contains("audit_log", StringComparison.Ordinal));
        Assert.Contains(blocks, sql => sql.Contains("companies", StringComparison.Ordinal));

        // ‏**ولا يُنشر اسمُ جدولٍ لا وجودَ له**: `users` عارياً كان أوّلَ خطئي.
        Assert.DoesNotContain(blocks, sql => Regex.IsMatch(sql, @"\bFROM\s+users\b", RegexOptions.IgnoreCase));
    }

    [PostgresFact]
    public async Task EveryPublishedQueryExecutesAgainstTheRealSchema()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("runbook");

        await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة كتيّب الفحص", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        await using var connection = new NpgsqlConnection(scope.ConnectionString);
        await connection.OpenAsync();

        var blocks = SqlBlocks();
        Assert.True(blocks.Count >= 4, "‏لا كتلَ SQL تُنفَّذ — راجع شاهدَ الضبط أعلاه.");

        // ‏**ولا تخطٍّ صامت**: كلُّ كتلةٍ لا تُنفَّذ يجب أن تكون من الشكل المُعلَن،
        // ‏وإلّا سقطت البوّابةُ حتى يُعلَن أمرُها. **وبوّابةٌ تتخطّى بلا إعلانٍ تُقرأ تغطيةً
        // ‏كاملةً وهي ناقصة** — وهو نمطُ «أخضرُ يعني لم يُفحص» الذي يُدان في هذا المستودع.
        var skipped = blocks.Where(sql => !ExecutableKeywords.Contains(FirstKeyword(sql))).ToList();
        var undeclared = skipped.Where(sql => !DeclaredNonExecutable.Contains(FirstKeyword(sql))).ToList();
        Assert.True(undeclared.Count == 0,
            "‏كتلُ SQL بشكلٍ غيرِ مُعلَن — لا تُنفَّذ ولا يجوز تخطّيها صامتةً:"
            + Environment.NewLine + string.Join(Environment.NewLine, undeclared.Select(FirstKeyword)));

        var executable = blocks.Where(sql => ExecutableKeywords.Contains(FirstKeyword(sql))).ToList();
        Assert.True(executable.Count >= 4,
            $"‏عددُ الكتل القابلة للتنفيذ {executable.Count} — أقلُّ من أربعةٍ، فالفحصُ ناقص.");

        var failures = new List<string>();

        foreach (var sql in executable)
        {
            try
            {
                await using var command = new NpgsqlCommand(sql, connection);
                // ‏`ExecuteNonQuery` يكفي: المقصودُ أن يُحلَّل ويُنفَّذ على المخطَّط،
                // ‏لا أن يُعيد صفّاً — **فالقاعدةُ نظيفةٌ عمداً**.
                await command.ExecuteNonQueryAsync();
            }
            catch (PostgresException exception)
            {
                // ‏**يُجمَع ولا يُرمى من أوّل سقطة**: رسالةٌ واحدةٌ تُخفي بقيّةَ الأخطاء،
                // ‏فيُصلَح استعلامٌ ثمّ يسقط التالي — وهي دورةٌ تُكلّف تشغيلةً لكلّ سطر.
                failures.Add($"{exception.SqlState}: {exception.MessageText}"
                           + Environment.NewLine + "    " + sql.Replace("\n", "\n    "));
            }
        }

        Assert.True(failures.Count == 0,
            $"‏استعلاماتٌ منشورةٌ في {Runbook} لا تعمل على المخطَّط:"
            + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, failures));
    }
}
