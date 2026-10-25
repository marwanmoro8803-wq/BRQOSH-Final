using Brqosh.Infrastructure.Setup;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// (التنصيبُ الجديد يبدأ فارغاً) <b>تنصيبٌ جديدٌ لا يتخطّى المعالجَ على بياناتٍ قديمةٍ بلا سؤال</b> — وما يُنقل لا يُحذف. المالكُ فتح البرنامجَ فوجد شاشةَ دخولٍ
/// باسمٍ محفوظٍ (<c>demo</c>) بدل معالج الإعداد، لأنّ <c>%APPDATA%\BRQOSH</c> يبقى بعد إعادة التنصيب.
/// </summary>
public class ANewInstallationDoesNotInheritThePreviousOneSilentlyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "brqosh-inst-" + Guid.NewGuid().ToString("N")[..8]);

    public ANewInstallationDoesNotInheritThePreviousOneSilentlyTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private void SeedOldInstallation()
    {
        File.WriteAllText(Path.Combine(_root, "setup.json"), "{\"IsCompleted\":true}");
        File.WriteAllText(Path.Combine(_root, "database.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "login-prefs.json"), "{\"RememberMe\":true,\"Username\":\"demo\"}");
        File.WriteAllText(Path.Combine(_root, "license.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "eula-accepted.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_root, "zatca"));
        File.WriteAllText(Path.Combine(_root, "zatca", "private.key"), "k");
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
    }

    [Fact]
    public void AFolderWithoutASetup_NeedsNoQuestion()
    {
        Assert.Equal(InstallationStamp.Verdict.Continue, InstallationStamp.Judge(_root, "A", "1.0.1"));
    }

    [Fact]
    public void OldDataWithoutAnyStampRecord_IsAskedAbout()
    {
        SeedOldInstallation();
        Assert.Equal(InstallationStamp.Verdict.Ask, InstallationStamp.Judge(_root, "A", "1.0.1"));
    }

    [Fact]
    public void TheSameInstallation_ContinuesSilently()
    {
        SeedOldInstallation();
        InstallationStamp.Remember(_root, "A", "1.0.1");
        Assert.Equal(InstallationStamp.Verdict.Continue, InstallationStamp.Judge(_root, "A", "1.0.1"));
    }

    [Fact]
    public void ANewerVersion_IsAnOrdinaryUpgrade_NotAQuestion()
    {
        SeedOldInstallation();
        InstallationStamp.Remember(_root, "A", "1.0.1");
        Assert.Equal(InstallationStamp.Verdict.Upgrade, InstallationStamp.Judge(_root, "B", "1.0.2"));
    }

    [Theory]
    [InlineData("1.0.1")]   // إعادةُ تنصيبٍ لنفس الإصدار
    [InlineData("1.0.0")]   // تخفيض
    public void AReinstallOfTheSameOrAnOlderVersion_IsAsked(string version)
    {
        SeedOldInstallation();
        InstallationStamp.Remember(_root, "A", "1.0.1");
        Assert.Equal(InstallationStamp.Verdict.Ask, InstallationStamp.Judge(_root, "B", version));
    }

    [Fact]
    public void ACorruptRecord_IsAsked_NotGuessed()
    {
        SeedOldInstallation();
        File.WriteAllText(Path.Combine(_root, "installation.json"), "{ليس json");
        Assert.Equal(InstallationStamp.Verdict.Ask, InstallationStamp.Judge(_root, "A", "1.0.1"));
    }

    [Fact]
    public void StartingFresh_MovesTheCompanyData_ButNeverDeletesIt_AndKeepsTheLicence()
    {
        SeedOldInstallation();

        var archive = InstallationStamp.StartFresh(_root, new DateTime(2026, 10, 25, 7, 0, 0));

        // ‏بياناتُ المنشأة والحساباتُ ومفاتيحُ الختم انتقلت إلى الأرشيف — لا حذف.
        Assert.True(File.Exists(Path.Combine(archive, "setup.json")));
        Assert.True(File.Exists(Path.Combine(archive, "login-prefs.json")));
        Assert.True(File.Exists(Path.Combine(archive, "database.json")));
        Assert.True(File.Exists(Path.Combine(archive, "zatca", "private.key")));

        // ‏والجذرُ صار بلا إعدادٍ ولا اسمٍ محفوظ ⟵ المعالجُ هو ما يظهر أوّلاً.
        Assert.False(File.Exists(Path.Combine(_root, "setup.json")));
        Assert.False(File.Exists(Path.Combine(_root, "login-prefs.json")));
        Assert.False(File.Exists(Path.Combine(_root, "database.json")));

        // ‏والترخيصُ وقبولُ الاتفاقية والسجلّاتُ ليست بياناتِ منشأة فتبقى.
        Assert.True(File.Exists(Path.Combine(_root, "license.json")));
        Assert.True(File.Exists(Path.Combine(_root, "eula-accepted.json")));
        Assert.True(Directory.Exists(Path.Combine(_root, "logs")));

        Assert.Equal(InstallationStamp.Verdict.Continue, InstallationStamp.Judge(_root, "A", "1.0.1"));
    }

    [Fact]
    public void TheStamp_ChangesWhenTheExecutableOrVersionChanges()
    {
        var exe = Path.Combine(_root, "app.exe");
        File.WriteAllText(exe, "x");
        var one = InstallationStamp.Current(exe, "1.0.1");

        Assert.Equal(one, InstallationStamp.Current(exe, "1.0.1"));
        Assert.NotEqual(one, InstallationStamp.Current(exe, "1.0.2"));

        File.SetLastWriteTimeUtc(exe, DateTime.UtcNow.AddHours(-5));
        Assert.NotEqual(one, InstallationStamp.Current(exe, "1.0.1"));
    }
}
