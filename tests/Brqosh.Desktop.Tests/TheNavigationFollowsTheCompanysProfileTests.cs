using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦٦ · sector-modules#10) <b>القوائمُ تُبنى لنظام المنشأة — لا لاختيار معالجِ هذا الجهاز.</b>
///
/// <para><b>ما كان:</b> المُنشئُ يبدأ <c>BindCompaniesAsync</c> بلا انتظارٍ ثمّ يبني القوائمَ فوراً من
/// <c>_setup.Profile</c> — وهو ما اختاره معالجُ الجهاز. و<c>ApplyCompanyContextAsync</c> يُصحّح النظامَ
/// والشارةَ بعد الربط <b>ولا يُعيد بناءَ القوائم</b>؛ لا يُعاد إلّا بتبديل الشركة (والقائمةُ معطَّلةٌ لشركةٍ واحدة)
/// أو اللغة أو الدخول بعد الخمول. <b>فجهازٌ ثانٍ ترك معالجَه على «محاسبة عامة» يفتح منشأةَ مقاولاتٍ بلا شاشاتها</b>،
/// والشارةُ في الأعلى تقول «مقاولات»، ثمّ تظهر الشاشاتُ بعد تبديل اللغة — سلوكٌ يبدو عشوائيّاً للدعم.</para>
///
/// <para><b>والإصلاحُ طبقتان:</b> المُقلِعُ يتبنّى نظامَ المنشأة المخزَّن من <c>CompanyBootstrapResult</c> قبل أن
/// يُنشئ النافذة (فالبناءُ الأوّلُ صحيح)، و<c>ApplyCompanyContextAsync</c> يُعيد البناءَ حين يتغيّر النظام
/// (فتبديلُ منشأةٍ بنظامٍ آخرَ أو أيُّ انحرافٍ لاحقٍ لا يترك القوائمَ خلف الشارة).</para>
///
/// <para><b>ولمَ إثباتٌ في النصّ لا اختبارُ سلوك:</b> <c>App</c> و<c>MainWindow</c> كودٌ خلف WPF يفتح قاعدةَ
/// <c>AppServices</c> الساكنة — ولا اختبارَ يُعيد توجيهها. وعقدُ الخدمة نفسُه مقيسٌ سلوكاً في
/// <c>TheBootstrapReportsTheStoredProfileTests</c>.</para>
/// </summary>
public class TheNavigationFollowsTheCompanysProfileTests
{
    private static string Source(string file) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", file));

    /// <summary>جسمُ دالّةٍ من توقيعها إلى أوّل عضوٍ بعدها.</summary>
    private static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"‏لا «{signature}» في المصدر.");
        var next = new[] { "\n    private ", "\n    internal ", "\n    public ", "\n    protected " }
            .Select(m => source.IndexOf(m, start + signature.Length, StringComparison.Ordinal))
            .Where(i => i > 0)
            .DefaultIfEmpty(source.Length)
            .Min();
        return source[start..next];
    }

    [Fact]
    public void TheStartupAdoptsTheStoredProfile_BeforeTheWindowIsBuilt()
    {
        var app = Source("App.xaml.cs");

        const string adopt = "setup = setup with { Profile = result.Profile };";
        Assert.Contains("if (result.Profile != setup.Profile)", app, StringComparison.Ordinal);
        Assert.Contains(adopt, app, StringComparison.Ordinal);

        var window = app.IndexOf("new MainWindow(", StringComparison.Ordinal);
        Assert.True(window > 0, "‏لا «new MainWindow(» في App.xaml.cs.");
        Assert.True(app.IndexOf(adopt, StringComparison.Ordinal) < window,
            "‏التبنّي يقع بعد إنشاء النافذة — فالبناءُ الأوّلُ للقوائم على اختيار المعالج.");
    }

    [Fact]
    public void ApplyingACompanyRebuildsTheNavigation_WhenItsProfileDiffers()
    {
        var body = MethodBody(Source("MainWindow.xaml.cs"), "private async Task ApplyCompanyContextAsync(");

        Assert.Contains("var profileChanged = _setup.Profile != company.Profile;", body, StringComparison.Ordinal);
        Assert.Contains("if (profileChanged)", body, StringComparison.Ordinal);
        Assert.Contains("BuildNavigation();", body, StringComparison.Ordinal);
        Assert.Contains("SetActiveNav(_activePageKey);", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب</b> — الماسحُ يقرأ الملفَّين الحقيقيَّين، <b>والقوائمُ تُبنى من <c>_setup.Profile</c></b>:
    /// وهي مقدّمةُ المقارنة أعلاه، فإن بُنيت من غيره صارت المقارنةُ بلا معنى.
    /// </summary>
    [Fact]
    public void TheScanner_ReadsTheRealFiles_AndTheNavigationIsBuiltFromTheSetupProfile()
    {
        var window = Source("MainWindow.xaml.cs");
        var app = Source("App.xaml.cs");

        Assert.True(window.Length > 10_000 && app.Length > 10_000, "‏مصدرٌ مقروءٌ قصيرٌ — مشكوكٌ فيه.");
        Assert.Contains("NavigationCatalog.ForProfile(_setup.Profile)", MethodBody(window, "private void BuildNavigation()"),
            StringComparison.Ordinal);
        Assert.Contains("DatabaseBootstrapService", app, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
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
