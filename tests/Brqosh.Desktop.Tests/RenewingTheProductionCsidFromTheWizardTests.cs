using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦٢ · tax-zatca#10) <b>تجديدُ شهادة الإنتاج يُبلَغ من المعالج.</b>
///
/// <para><b>ما كان:</b> خدمةُ التجديد (ح-١٣٧) مقيسةٌ ولا زرَّ يستدعيها — فالمالكُ الذي تقترب شهادتُه من الانتهاء
/// لا يجد إلّا التسجيلَ من أوّله، وهو يُبدّل المفتاحَ قبل قبول الهيئة.</para>
///
/// <para>سلوكُ الخدمة (المفتاحُ الجانبيّ، والإبدالُ بعد القبول وحدَه) مقيسٌ في
/// <c>RenewingTheCsidKeepsTheOldKeyUntilAcceptedTests</c>؛ وهنا يُقاس <b>التوصيل</b> بترتيبه: الحارسُ قبل توليد المفتاح،
/// والمفتاحُ للتجديد، والإعدادُ لا يُكتب إلّا بعد ردّ الهيئة، والتجديدُ لا يُفعّل إرسالاً أوقفه المالك.</para>
/// </summary>
public class RenewingTheProductionCsidFromTheWizardTests
{
    private static readonly string ViewsDir = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "Views");
    private static readonly string Xaml = File.ReadAllText(Path.Combine(ViewsDir, "ZatcaCsidWizardWindow.xaml"));
    private static readonly string Source = File.ReadAllText(Path.Combine(ViewsDir, "ZatcaCsidWizardWindow.xaml.cs"));

    [Fact]
    public void TheWizard_HasARenewButton_WiredToOnRenew()
    {
        Assert.Contains("Content=\"تجديد شهادة الإنتاج\"", Xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRenew\"", Xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void OnRenew_RefusesWithoutAProductionCertificate_BeforeMakingAKey()
    {
        var method = Body("private async void OnRenew(");

        var guard = IndexOf(method, "settings.Environment != ZatcaEnvironment.Production");
        var refuse = IndexOf(method, "return;");
        var generate = IndexOf(method, ".GenerateKeyPairAndCsr(");

        Assert.True(guard < refuse && refuse < generate, "الحارسُ يرجع قبل توليد مفتاح التجديد");
    }

    [Fact]
    public void OnRenew_UsesTheRenewalPath_AndWritesSettingsOnlyAfterZatcaAnswers()
    {
        var method = Body("private async void OnRenew(");

        var generate = IndexOf(method, ".GenerateKeyPairAndCsr(");
        var renew = IndexOf(method, ".RenewProductionCsidAsync(");
        var applyCsid = IndexOf(method, "ZatcaSettingsSecrets.ApplyProtectedCsid(");
        var applyUpdate = IndexOf(method, "ZatcaSettingsSecrets.ApplyProtectedUpdate(");
        var save = IndexOf(method, "db.SaveChangesAsync()");

        Assert.Contains("forRenewal: true", method[generate..renew], StringComparison.Ordinal);
        Assert.Contains("ZatcaSettingsSecrets.GetApiToken(settings)", method[renew..applyCsid], StringComparison.Ordinal);
        Assert.True(generate < renew && renew < applyCsid && applyCsid < applyUpdate && applyUpdate < save,
            "المفتاح ← ردُّ الهيئة ← الشهادة ← الإعداد ← الحفظ");

        Assert.DoesNotContain(".RequestProductionCsidAsync(", method, StringComparison.Ordinal);
        Assert.DoesNotContain(".RequestComplianceCsidAsync(", method, StringComparison.Ordinal);
    }

    [Fact]
    public void OnRenew_KeepsTheOwnersEnabledChoice()
    {
        var method = Body("private async void OnRenew(");
        var update = method[IndexOf(method, "ZatcaSettingsSecrets.ApplyProtectedUpdate(")..];

        Assert.StartsWith("ZatcaSettingsSecrets.ApplyProtectedUpdate(settings, settings.IsEnabled,", update, StringComparison.Ordinal);
    }

    private static int IndexOf(string text, string needle)
    {
        var i = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(i >= 0, $"لم يُعثر على {needle}");
        return i;
    }

    private static string Body(string signature)
    {
        var start = IndexOf(Source, signature);
        var rest = Source[start..];
        return rest[..rest.IndexOf("\n    }", StringComparison.Ordinal)];
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
