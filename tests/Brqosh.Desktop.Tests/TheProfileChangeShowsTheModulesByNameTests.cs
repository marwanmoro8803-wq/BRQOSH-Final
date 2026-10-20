using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>ui-ux-i18n-printing#7</c>) <b>شاشةُ الإعدادات تغيّر ملفَّ النشاط عبر الخدمة — وتعرض الرفضَ بأسماء الوحدات المعروضة.</b>
///
/// <para>الخدمةُ ترفض ملفّاً يُخفي وحداتٍ ذاتَ بيانات بـ<c>ProfileHidesModulesWithDataException</c> التي تحمل الوحداتِ نفسَها؛ ورسالتُها
/// الخامُ تُسمّيها بأسماء الكود («Hospitality»). <b>فالشاشةُ تلتقط الرفضَ المُنمَّط وتصوغه بـ<c>ModuleLabelAr</c> و<c>ProfileLabel</c></b>،
/// وإلّا رأى المستخدمُ العربيُّ أسماءً إنجليزيّةً في جملةٍ عربيّة. ويُقاس بنيةً (كأخواته): قيادةُ الصفحة حيّةً تستلزم قاعدةً ومديراً.</para>
/// </summary>
public class TheProfileChangeShowsTheModulesByNameTests
{
    private static string PageSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Brqosh.Desktop", "Views", "Pages", "CompanySettingsPage.xaml.cs"));
    }

    [Fact]
    public void TheProfileIsChangedThroughTheGuardedService()
    {
        var text = PageSource();

        Assert.Contains("GetService<BusinessProfileChangeService>().ChangeAsync(company.Id, profileOpt.Profile)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateBusinessProfile(", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusal_IsShownWithTheDisplayedModuleNames()
    {
        var text = PageSource();

        var change = text.IndexOf("GetService<BusinessProfileChangeService>()", StringComparison.Ordinal);
        var catchAt = text.IndexOf("catch (ProfileHidesModulesWithDataException refused)", change, StringComparison.Ordinal);
        Assert.True(change > 0 && catchAt > change, "‏الرفضُ المُنمَّط لا يُلتقط بعد استدعاء الخدمة");

        var handler = text[catchAt..text.IndexOf("ProfileText.Text = ProfileLabel(profileOpt.Profile);", catchAt, StringComparison.Ordinal)];
        Assert.Contains("refused.Modules.Select(ModuleLabelAr)", handler, StringComparison.Ordinal);
        Assert.Contains("ProfileLabel(refused.Profile)", handler, StringComparison.Ordinal);
    }
}
