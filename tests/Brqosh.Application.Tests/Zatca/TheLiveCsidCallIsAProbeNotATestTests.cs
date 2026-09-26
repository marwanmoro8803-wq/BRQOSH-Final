namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#15</c>) <b>طلبُ CSID الحيُّ مسبارٌ يُشغَّل، لا اختبارٌ يُعدّ.</b>
///
/// <para><b>ما كان:</b> <c>ZatcaLiveCsidTests</c> في هذه الحزمة يقرأ رمزَ الهيئة من البيئة و<b>يرجع</b> بلا
/// رمز — فيُسجَّل <c>Passed</c> في كلّ تشغيلٍ (0.0008s في <c>trx</c> 2026-09-15) ولم يُرسل شيئاً قطّ، <b>ويُحسب في
/// «صفرٌ متخطًّى»</b>. ودليلُ التشغيل O-01 يُحيل إليه، و<c>ZATCA_LIVE_CSID.md</c> يقول «يُتخطَّى بنجاح».</para>
///
/// <para><b>ولماذا لا «تخطٍّ مُعلَن»</b> كما صُنع لبوّابات الأداء (<c>StressFactAttribute</c>): الرمزُ لا يكون في CI
/// أبداً، فالتخطّي دائمٌ فيه — <b>وبوّابةُ CI تشترط صفراً</b>. فإمّا نجاحٌ كاذبٌ وإمّا بوّابةٌ حمراءُ دائماً.
/// فصار أداةً (<c>tools/ZatcaLiveCsidProbe</c>) يُشغّلها مَن يملك الرمز وتخرج بغير الصفر حين لا تُعطاه.</para>
///
/// <para><b>والحارسان هنا:</b> لا اختبارَ في الحزمة يقرأ رمزَ الهيئة الحيّ — فلا يعود النمطُ باسمٍ آخر؛
/// <b>والمسبارُ يُغيّر جذرَ البيانات قبل أن يكتب مفتاحاً</b> ويرفض قبل أيّ طلب — فلا يستبدل مفتاحَ ختم
/// المُشغِّل ولا يُسجّل وحدةَ إصدارٍ في الإنتاج. <b>والمسبارُ لا يُشغَّل في الحزمة</b> (شبكةٌ ورمزٌ حيّ)، فيُقاس
/// نصُّه بترتيبٍ لا بوجودٍ فقط.</para>
/// </summary>
public class TheLiveCsidCallIsAProbeNotATestTests
{
    /// <summary>مبنيٌّ بالوصل حتى لا يجده المسحُ في هذا الملفّ نفسِه.</summary>
    private static readonly string LiveOtp = "BRQOSH_ZATCA_" + "OTP";

    private const string Probe = "tools/ZatcaLiveCsidProbe/Program.cs";

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

    /// <summary>ملفّاتُ المصدر التي تذكر <paramref name="needle"/> — <b>و<c>bin</c> و<c>obj</c> مستثنيان</b>.</summary>
    private static List<string> SourcesMentioning(string directory, string needle) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains(needle, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(RepoRoot(), f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void NoTestInTheSuite_ReadsTheLiveZatcaOtp()
    {
        var readers = SourcesMentioning(Path.Combine(RepoRoot(), "tests"), LiveOtp);

        Assert.True(readers.Count == 0,
            $"‏اختبارٌ يقرأ {LiveOtp} يرجع بلا رمزٍ في كلّ تشغيل ويُعدّ ناجحاً — والطلبُ الحيُّ مكانُه {Probe}: "
            + string.Join(" · ", readers));
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ من الصنف نفسِه:</b> المسحُ نفسُه يجد الرمزَ حيث يعيش الآن. <b>وبلا هذا</b> يمرّ الأوّلُ على
    /// إبرةٍ مكتوبةٍ خطأً أو مجلّدٍ لا يُقرأ.
    /// </summary>
    [Fact]
    public void TheSameScan_FindsTheOtp_InTheProbe()
    {
        var readers = SourcesMentioning(Path.Combine(RepoRoot(), "tools", "ZatcaLiveCsidProbe"), LiveOtp);

        Assert.Equal(new[] { Probe }, readers);
    }

    private static string ProbeText() => File.ReadAllText(Path.Combine(RepoRoot(), Probe));

    private static int At(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"‏«{marker}» غائبٌ عن {Probe}");
        return index;
    }

    /// <summary>
    /// <c>GenerateKeyPairAndCsr</c> يكتب <c>private.key</c> تحت جذر البيانات — <b>وجذرُ المُشغِّل فيه مفتاحُ ختمه</b>.
    /// فالجذرُ يُنقل قبل المفتاح، لا بعده.
    /// </summary>
    [Fact]
    public void TheProbe_MovesTheDataRoot_BeforeItWritesAKey()
    {
        var text = ProbeText();

        Assert.True(
            At(text, "SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable")
            < At(text, "GenerateKeyPairAndCsr("),
            "‏المسبارُ يولّد المفتاحَ قبل نقل جذر البيانات — فيكتب فوق مفتاح ختم المُشغِّل.");
    }

    /// <summary>
    /// <b>بلا رمزٍ لا طلب</b> (خروجٌ 2 لا نجاح)، <b>وعنوانُ الإنتاج مرفوض</b> (خروجٌ 3) — وكلاهما قبل المفتاح والطلب.
    /// </summary>
    [Fact]
    public void TheProbe_RefusesWithoutAnOtp_AndOffProduction_BeforeAnything()
    {
        var text = ProbeText();
        var firstAction = At(text, "GenerateKeyPairAndCsr(");

        Assert.True(At(text, "return 2;") < firstAction, "‏الرفضُ بلا رمزٍ بعد أوّل فعل.");
        Assert.True(At(text, "return 3;") < firstAction, "‏رفضُ الإنتاج بعد أوّل فعل.");
        Assert.True(firstAction < At(text, "RequestComplianceCsidAsync("), "‏الطلبُ قبل المفتاح؟");
    }
}
