using Brqosh.Application.Setup;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٥ · <c>security-licensing-tenancy#0</c> · <c>installer-deployment-ops#7</c>) <b>الثنائيّةُ المشحونة لا
/// تعرف غيرَ الإنتاج.</b>
///
/// <para><b>ما كان:</b> «معطّلٌ قسرياً في الإنتاج» صحيحٌ في <c>LicenseStore</c> و<c>App.TryEnterWithUiTestAuthAsync</c> —
/// <b>والإنتاجُ نفسُه قابلٌ للخفض</b>: المصدرُ الرسميُّ (متغيّرُ الجهاز · ملفُّ العلامة في مجلّد التثبيت) يكتبه مسؤولُ Windows
/// المحلّيّ، وهو في المنشأة الصغيرة صاحبُ الجهاز. فـ«development» هناك ⟵ <see cref="AppEnvironmentConfig.Decide"/> يُرجع تطويراً
/// ⟵ الفترةُ التجريبيّةُ ثلاثين يوماً، و<c>BRQOSH_UITEST=1</c> يدخل بحساب المدير بلا كلمة مرور ويتجاوز الترخيص.</para>
///
/// <para><b>والإصلاح:</b> <c>installer/publish.ps1</c> وحدَه يبني بـ<c>-p:BrqoshShipped=true</c> فيُعرَّف <c>BRQOSH_SHIPPED</c>،
/// و<see cref="AppEnvironmentConfig"/> يُرجع Production بلا قراءة. <b>ولا تشغيلةَ اختبارٍ تبني المشحون</b> — فالفرعُ نفسُه
/// دالةٌ نقيّةٌ يُقاس بكلّ تركيبات المدخلات، والتوصيلُ يُقاس في النصّ.</para>
/// </summary>
public class AShippedBuildKnowsOnlyProductionTests
{
    private static readonly AppEnvironment?[] Authoritative =
        [null, AppEnvironment.Development, AppEnvironment.Test, AppEnvironment.Production];

    private static readonly string?[] ProcessEnv = [null, "", "development", "dev", "test", "production", "Productoin"];

    private static readonly string?[] TestFlag = [null, "0", "1"];

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

    private static string Text(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// في الثنائيّة المشحونة: إنتاجٌ — أيّاً كان ما كتبه المسؤولُ في الجهاز، وما أعلنته العمليّة، وعلَمُ الاختبار.
    /// </summary>
    [Fact]
    public void AShippedBuild_IsProduction_WhateverTheMachineSays()
    {
        var downgraded = new List<string>();
        foreach (var authoritative in Authoritative)
        foreach (var processEnv in ProcessEnv)
        foreach (var testFlag in TestFlag)
        {
            var got = AppEnvironmentConfig.DecideForBuild(true, authoritative, processEnv, testFlag);
            if (got != AppEnvironment.Production)
                downgraded.Add($"({authoritative?.ToString() ?? "∅"}, {processEnv ?? "∅"}, {testFlag ?? "∅"}) ⟵ {got}");
        }

        Assert.True(downgraded.Count == 0, $"‏ثنائيّةٌ مشحونةٌ خُفضت: {string.Join(" · ", downgraded)}");
    }

    /// <summary>
    /// <b>شاهدُ ضبط — والطريقُ الذي أُغلق:</b> في البناء غير المشحون يبقى إعلانُ الجهاز «development» تطويراً، كما كان.
    /// فالإنتاجُ أعلاه من علَم الشحن لا من دالةٍ تُرجع إنتاجاً دائماً.
    /// </summary>
    [Fact]
    public void AnUnshippedBuild_StillHonoursTheMachine_WhichIsThePathTheFlagCloses()
    {
        Assert.Equal(AppEnvironment.Development,
            AppEnvironmentConfig.DecideForBuild(false, AppEnvironment.Development, null, null));
        Assert.Equal(AppEnvironment.Test, AppEnvironmentConfig.DecideForBuild(false, null, null, "1"));
    }

    /// <summary>
    /// وهذه التشغيلةُ ليست مشحونة — وإلّا لكان <c>BRQOSH_TEST</c> بلا أثرٍ وسقطت الاختباراتُ كلُّها قبل أن تقيس شيئاً.
    /// </summary>
    [Fact]
    public void ThisTestRun_IsNotAShippedBuild() =>
        Assert.False(AppEnvironmentConfig.IsShippedBuild);

    /// <summary>
    /// والتوصيل: <c>Resolve()</c> يمرّ بالعلَم، والعلَمُ يُعرَّف من الخاصّيّة، <b>و<c>publish.ps1</c> وحدَه يمرّرها</b> — ولا خطَّ
    /// اختبارٍ ولا CI يبني المشحون.
    /// </summary>
    [Fact]
    public void OnlyThePackagingScript_BuildsAShippedBinary()
    {
        var environment = Text("src/Brqosh.Application/Setup/AppEnvironment.cs");
        Assert.Contains("private static AppEnvironment Resolve() => DecideForBuild(\n        IsShippedBuild,",
            environment.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("#if BRQOSH_SHIPPED", environment, StringComparison.Ordinal);

        var props = Text("Directory.Build.props");
        Assert.Contains("<PropertyGroup Condition=\"'$(BrqoshShipped)' == 'true'\">", props, StringComparison.Ordinal);
        Assert.Contains("<DefineConstants>$(DefineConstants);BRQOSH_SHIPPED</DefineConstants>", props, StringComparison.Ordinal);

        var root = RepoRoot();
        var passers = new[] { (".github", "*.yml"), ("scripts", "*.ps1"), ("installer", "*.ps1"), ("tools", "*.ps1") }
            .Where(x => Directory.Exists(Path.Combine(root, x.Item1)))
            .SelectMany(x => Directory.EnumerateFiles(Path.Combine(root, x.Item1), x.Item2, SearchOption.AllDirectories))
            .Where(p => File.ReadAllText(p).Contains("BrqoshShipped=true", StringComparison.Ordinal))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .ToList();

        Assert.Equal(new[] { "installer/publish.ps1" }, passers);
        var publish = Text("installer/publish.ps1");
        Assert.Contains("-p:BrqoshBuildNumber=$BuildNumber -p:BrqoshShipped=true", publish, StringComparison.Ordinal);
    }
}
