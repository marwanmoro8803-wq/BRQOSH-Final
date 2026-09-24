namespace Brqosh.Application.Tests.Performance;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٩ · <c>data-upgrade-performance#3</c>) <b>«وضعُ المليون» يُشغّل مليوناً — وكان يُشغّل ألفين.</b>
///
/// <para>‏<b>ما كان</b>: <c>run-stress-benchmark.ps1 -FullMillion</c> يضبط المتغيّراتِ نفسَها في فرعَيه — <b>لا يضبط
/// <c>BRQOSH_LOAD_ENTRIES</c> أصلاً</b> فيبقى الاختبارُ على ألفين؛ <b>ولو ضُبط لقُصّ عند مئة ألف</b>
/// (<c>Math.Min(envCount, 100_000)</c>). <b>والوثيقةُ</b> (<c>PERFORMANCE_BUDGETS.md</c>) تقول «يُشغِّل نفسَ الحارس
/// بحمل 1,000,000 قيد». <b>ولا طريقَ إلى PostgreSQL</b> — SQLite بالذاكرة دائماً.</para>
///
/// <para>‏<b>والقياسُ على العدد والمزوّد لا على الحمل نفسِه</b> — مليونُ قيدٍ ساعاتٌ على جهازٍ مخصّص؛ <b>والسكربتُ يُقرأ
/// نصّاً</b> لأنّه ما يُشغّله المُشغِّل، <b>ويُربط بثوابت الاختبار</b> فلا يفترقان باسمٍ مكتوبٍ مرّتين.</para>
/// </summary>
public class TheLoadHarnessRunsWhatItSaysTests
{
    [Theory]
    [InlineData(null, 2_000)]
    [InlineData("100", 2_000)]            // ‏ضجيجٌ لا قياس
    [InlineData("5000", 5_000)]
    [InlineData("1000000", 1_000_000)]    // ‏‏وكان يُقصّ إلى مئة ألف
    [InlineData("2000000", 1_000_000)]
    public void TheRequestedCount_IsTheCountRun(string? value, int expected) =>
        Assert.Equal(expected, JournalStressLoadTests.EntriesFrom(value));

    [Theory]
    [InlineData("postgres", true)]
    [InlineData("PostgreSQL", false)]
    [InlineData("Postgres", true)]
    [InlineData(null, false)]
    [InlineData("sqlite", false)]
    public void PostgreSql_IsChosenByName(string? value, bool expected) =>
        Assert.Equal(expected, JournalStressLoadTests.UsesPostgres(value));

    /// <summary>‏<b>القياسُ الحاكم:</b> <c>-FullMillion</c> يضبط المليون — والافتراضيُّ لا.</summary>
    [Fact]
    public void FullMillion_SetsAMillion_AndTheDefaultDoesNot()
    {
        var (full, otherwise) = Branches(Script(), "if ($FullMillion) {");
        var assignment = $"$env:{JournalStressLoadTests.EntriesVariable} = \"1000000\"";

        Assert.Contains(assignment, full, StringComparison.Ordinal);
        Assert.DoesNotContain(assignment, otherwise, StringComparison.Ordinal);
    }

    /// <summary>‏<b>و<c>-Postgres</c> يضبط المزوّدَ ويشترط الخادم</b> — فلا يمرّ «وضعُ PostgreSQL» على SQLite بصمت.</summary>
    [Fact]
    public void Postgres_SetsTheProvider_AndRequiresTheServer()
    {
        var (postgres, otherwise) = Branches(Script(), "if ($Postgres) {");
        var provider = $"$env:{JournalStressLoadTests.ProviderVariable} = \"postgres\"";

        Assert.Contains(provider, postgres, StringComparison.Ordinal);
        Assert.Contains("$env:BRQOSH_REQUIRE_PG = \"1\"", postgres, StringComparison.Ordinal);
        Assert.DoesNotContain(provider, otherwise, StringComparison.Ordinal);
    }

    private static (string Then, string Else) Branches(string script, string opening)
    {
        var start = script.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"«{opening}» غير موجود في السكربت — القياسُ أعمى.");
        var elseAt = script.IndexOf("} else {", start, StringComparison.Ordinal);
        Assert.True(elseAt > start, $"فرعُ «else» بعد «{opening}» غير موجود.");
        var end = script.IndexOf("\n}", elseAt + 1, StringComparison.Ordinal);
        Assert.True(end > elseAt, $"نهايةُ «else» بعد «{opening}» غير موجودة.");
        return (script[start..elseAt], script[elseAt..end]);
    }

    private static string Script()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.True(dir is not null, "جذرُ المستودع غير موجود.");
        return File.ReadAllText(Path.Combine(dir!.FullName, "scripts", "ops", "run-stress-benchmark.ps1"))
            .Replace("\r\n", "\n");
    }
}
