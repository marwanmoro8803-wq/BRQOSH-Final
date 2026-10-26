using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس» · قالبُ صفحات الإعدادات) أربعُ صفحاتٍ كانت بأعمدةٍ مختلفة (٧٢٠ · ٧٦٠ · ٨٨٠ مرّةً يميناً ومرّةً في الوسط) — قِيس بتصويرها.
/// صارت كلُّها على عمودٍ واحدٍ في الوسط من الثيم (<c>SettingsColumn</c> / <c>SettingsColumnWidth</c>)؛ فلا عرضٌ مكتوبٌ بيدٍ في صفحةِ إعدادات.
/// </summary>
public class SettingsPagesShareOneColumnTests
{
    [Theory]
    [InlineData("PreferencesPage.xaml")]
    [InlineData("ZatcaSettingsPage.xaml")]
    [InlineData("BackupPage.xaml")]
    [InlineData("CompanySettingsPage.xaml")]
    public void ASettingsPage_UsesTheSharedColumn(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var xaml = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Brqosh.Desktop", "Views", "Pages", file));

        Assert.Contains("SettingsColumn", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"720\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"760\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"880\"", xaml, StringComparison.Ordinal);
    }
}
