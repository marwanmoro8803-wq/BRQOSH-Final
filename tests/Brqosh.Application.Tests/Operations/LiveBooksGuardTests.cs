using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (م-٥ · ن-٢٢) <b>أداةٌ تكتب في دفاترَ حيّةٍ تستأذن باسم القاعدة — والرفضُ هو الأصل.</b>
///
/// <para><b>ما كان قبله:</b> <c>tools/FullScenarioAudit</c> تفتح قاعدةَ المُشغِّل من
/// <c>database.json</c> ثمّ <b>تُنشئ حساباً بدور «مدير» بكلمةٍ منشورةٍ في المستودع</b> —
/// بلا سؤالٍ ولا علامة. <b>وهي الخطوةُ ٦ في كتيّب التنصيب</b>، ويستدعيها
/// <c>O-01_ZATCA_PRODUCTION_RUNBOOK.md:14</c>، ويُشغّلها مُشغِّلُ البوّابات يوميّاً.</para>
///
/// <para><b>والاختبارُ سلوكيٌّ لا هجائيّ:</b> يستدعي الحارسَ نفسَه ويشترط أن <b>يرمي</b>
/// بلا إذن. <b>وهذا ما يجعل النسيانَ مستحيلاً</b> دون مطاردةِ هجاء — وقد سقط في هذه
/// الحملة خمسةُ حرّاسٍ يُطاردون هجاءً على كودٍ سليم.</para>
/// </summary>
public class LiveBooksGuardTests : IDisposable
{
    private readonly string? _before =
        Environment.GetEnvironmentVariable(LiveBooksGuard.ConsentEnvironmentVariable);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LiveBooksGuard.ConsentEnvironmentVariable, _before);
        GC.SuppressFinalize(this);
    }

    private static void Consent(string? value) =>
        Environment.SetEnvironmentVariable(LiveBooksGuard.ConsentEnvironmentVariable, value);

    [Fact]
    public void WithNoConsent_ItRefuses()
    {
        Consent(null);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => LiveBooksGuard.RequireConsentFor("localhost/brqosh", "تكتب سنداً"));

        // ‏والرسالةُ تُسمّي الهدفَ وتُعلّم المخرجَين: العزلَ والإذنَ بالاسم.
        Assert.Contains("localhost/brqosh", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(BrqoshDataRoot.OverrideEnvironmentVariable, thrown.Message, StringComparison.Ordinal);
        Assert.Contains(LiveBooksGuard.ConsentEnvironmentVariable, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("تكتب سنداً", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithConsentForTheSameTarget_ItPasses()
    {
        Consent("localhost/brqosh_qa");
        LiveBooksGuard.RequireConsentFor("localhost/brqosh_qa", "تكتب سنداً");
    }

    /// <summary>
    /// <b>والإذنُ لا ينتقل بين الأجهزة</b> — وهو سببُ اشتراط الاسم بدل «نعم».
    /// </summary>
    [Fact]
    public void ConsentForAnotherTarget_DoesNotTransfer()
    {
        Consent("localhost/brqosh_qa");

        var thrown = Assert.Throws<InvalidOperationException>(
            () => LiveBooksGuard.RequireConsentFor("operator-pc/brqosh", "تكتب سنداً"));

        Assert.Contains("operator-pc/brqosh", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("آلةٍ غير التي أذِنتَ لها", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("yes")]
    public void ABooleanIsNotConsent(string value)
    {
        Consent(value);
        Assert.Throws<InvalidOperationException>(
            () => LiveBooksGuard.RequireConsentFor("localhost/brqosh", "تكتب سنداً"));
    }

    /// <summary>
    /// <b>ووصفُ الهدف لا ينسخ السرّ.</b> كلمةُ مرور PostgreSQL حماها CEEF-236 بـDPAPI عمداً،
    /// <b>فرسالةُ رفضٍ تحملها تُبطل تلك الحماية</b> — وتذهب إلى سجلٍّ أو لقطةِ شاشة.
    /// </summary>
    [Fact]
    public void DescribeTarget_NamesTheDatabaseAndNeverThePassword()
    {
        const string secret = "S3cr3t-Pg-Password";
        var described = LiveBooksGuard.DescribeTarget(
            $"Host=operator-pc;Port=5433;Database=brqosh;Username=postgres;Password={secret}", isSqlite: false);

        Assert.Equal("operator-pc/brqosh", described);
        Assert.DoesNotContain(secret, described, StringComparison.Ordinal);
        Assert.DoesNotContain("postgres", described, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeTarget_OnSqlite_NamesTheFile() =>
        Assert.StartsWith("sqlite:", LiveBooksGuard.DescribeTarget(null, isSqlite: true), StringComparison.Ordinal);

    /// <summary>
    /// <b>ولا كلمةَ مرورٍ حرفيّةً في مسارٍ موصولٍ ببوّابةٍ أو كتيّب.</b>
    ///
    /// <para>وحدُّه مكتوب: يفحص <b>الملفّات الثلاثة</b> التي كانت تحملها وتُشغَّل من بوّابةٍ
    /// أو كتيّب. <b>ولا يمسح المستودعَ كلَّه</b> — بقيّتُها في منصّاتِ تدقيقٍ تجريبيّةٍ
    /// تُجرّبها عند الدخول ولا تُنشئ بها حساباً، وهي بندٌ مستقلّ. <b>والماسحُ الأمنيُّ هو
    /// من يحرس البقيّة</b>، وقد صُحّح تعليلُ سماحه الذي كان يقول «لا تُستعمل في إنتاج».</para>
    /// </summary>
    [Theory]
    [InlineData("tools/FullScenarioAudit/Program.cs")]
    [InlineData("tools/UiAudit/Program.cs")]
    [InlineData("scripts/ops/run-ui-audit-nightly.ps1")]
    public void NoWiredPathCarriesALiteralPassword(string relativePath)
    {
        var root = RepoRoot();
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"‏ملفٌّ مفقود: {relativePath}");

        var text = File.ReadAllText(path);

        // ‏شاهدُ ضبط: القارئُ يرى اسمَ المتغيّر في الملفّ — فالنفيُ عن قراءةٍ لا عن عمى.
        Assert.Contains("BRQOSH_TEST_PASSWORD", text, StringComparison.Ordinal);

        Assert.False(text.Contains("UiAudit9", StringComparison.OrdinalIgnoreCase),
            $"‏كلمةُ مرورٍ حرفيّةٌ في مسارٍ تُشغّله بوّابةٌ أو كتيّبُ تنصيب: {relativePath}. "
            + "‏والأداةُ تُنشئ بها حساباً بدور «مدير» — **والإنشاءُ صامتٌ بخلاف إعادة التعيين**.");
    }

    /// <summary>
    /// <b>ويُستأذَن قبل أن يُفتح الاتّصال، لا بعده.</b>
    ///
    /// <para>الاختباراتُ أعلاه تُثبت أنّ الحارسَ يرفض؛ <b>ولا تُثبت أنّ الأداةَ تستدعيه قبل
    /// أن تفتح الدفاتر</b> — وحارسٌ يُستدعى بعد الفتح زينة. ولا يُقاس ذلك بتشغيل الأداة:
    /// <b>تشغيلُها لقياس الحارس يكتب في دفاترَ حقيقيّةٍ إن فشل الحارس</b>، وهو العطلُ
    /// نفسُه. فيُقاس الترتيبُ نصّاً على ملفٍّ واحدٍ برمزَين معروفَين — <b>وحدٌّ ضيّقٌ
    /// مقصود</b>، لا مطاردةَ هجاءٍ في المستودع.</para>
    /// </summary>
    [Fact]
    public void TheAuditToolAsksBeforeItOpensTheBooks()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "tools", "FullScenarioAudit", "Program.cs"));

        var asks = source.IndexOf(nameof(LiveBooksGuard.RequireConsentFor), StringComparison.Ordinal);
        var opens = source.IndexOf("BrqoshDbContextFactory.Create", StringComparison.Ordinal);

        // ‏شاهدا ضبط: الرمزان موجودان فعلاً — فالترتيبُ عن قراءةٍ لا عن غياب.
        Assert.True(asks >= 0, "‏الأداةُ لا تستدعي الحارسَ إطلاقاً.");
        Assert.True(opens >= 0, "‏لم يُعثر على فتح الاتّصال — الفحصُ يقرأ ملفّاً خطأً.");

        Assert.True(asks < opens,
            "‏الأداةُ تفتح الدفاترَ قبل أن تستأذن — والحارسُ بعد الفتح زينة.");
    }

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
}
