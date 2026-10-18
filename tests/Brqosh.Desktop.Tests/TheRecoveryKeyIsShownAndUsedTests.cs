using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · security-licensing-tenancy#7) <b>مفتاحُ استعادة المدير يبلغ الواجهة</b> — يُعرض بعد التهيئة، ويُستعمل من
/// «نسيت كلمة المرور»، ويُصدَر من جديد للمدير؛ <b>ونافذتُه لا تُغلق قبل تأكيد الحفظ</b>.
///
/// <para>سلوكُ المفتاح (الحرقُ والقفلُ والسياسة) مقيسٌ في <c>TheAdministratorRecoversWithTheRecoveryKeyTests</c>؛ وهنا التوصيلُ بترتيبه.</para>
/// </summary>
public class TheRecoveryKeyIsShownAndUsedTests
{
    private static readonly string Desktop = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Desktop, .. parts]));

    private static string Body(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"لم يُعثر على {signature}");
        var rest = source[start..];
        return rest[..rest.IndexOf("\n    }", StringComparison.Ordinal)];
    }

    [Fact]
    public void Setup_ShowsTheFirstKey_RightAfterTheBooksAreCreated()
    {
        var app = Read("App.xaml.cs");
        var bootstrap = app.IndexOf(".InitializeAsync(setup!.CompanyName", StringComparison.Ordinal);
        var shown = app.IndexOf("new Views.RecoveryKeyWindow(recoveryKey).ShowDialog();", StringComparison.Ordinal);

        Assert.True(bootstrap >= 0 && shown > bootstrap, "النافذةُ تُعرض بعد التهيئة");
        Assert.Contains("result.RecoveryKey is { } recoveryKey", app[bootstrap..shown], StringComparison.Ordinal);
    }

    [Fact]
    public void ForgotPassword_RecoversWithTheKey_AndShowsTheReplacement()
    {
        Assert.Contains("Click=\"OnRecoverWithKey\"", Read("Views", "ForgotPasswordWindow.xaml"), StringComparison.Ordinal);

        var method = Body(Read("Views", "ForgotPasswordWindow.xaml.cs"), "private async void OnRecoverWithKey(");
        var recover = method.IndexOf(".RecoverAsync(", StringComparison.Ordinal);
        var shown = method.IndexOf("new RecoveryKeyWindow(next)", StringComparison.Ordinal);
        Assert.True(recover >= 0 && shown > recover, "المفتاحُ البديلُ يُعرض بعد الاستعادة");
    }

    [Fact]
    public void OnlyAnAdministrator_SeesTheReissueButton()
    {
        Assert.Contains("x:Name=\"ReissueKeyButton\"", Read("Views", "PasswordChangeWindow.xaml"), StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"\n                    Click=\"OnReissueKey\"",
            Read("Views", "PasswordChangeWindow.xaml").Replace("\r\n", "\n"), StringComparison.Ordinal);

        var source = Read("Views", "PasswordChangeWindow.xaml.cs");
        Assert.Contains("if (user.Role == UserRole.Administrator)\n            ReissueKeyButton.Visibility = Visibility.Visible;",
            source.Replace("\r\n", "\n"), StringComparison.Ordinal);
        var method = Body(source, "private async void OnReissueKey(");
        Assert.True(method.IndexOf(".ReissueAsync(", StringComparison.Ordinal)
                    < method.IndexOf("new RecoveryKeyWindow(key)", StringComparison.Ordinal));
    }

    [Fact]
    public void TheKeyWindow_DoesNotCloseUntilTheOwnerConfirmsSaving()
    {
        Assert.Contains("IsEnabled=\"False\" Click=\"OnContinue\"", Read("Views", "RecoveryKeyWindow.xaml"), StringComparison.Ordinal);

        var closing = Body(Read("Views", "RecoveryKeyWindow.xaml.cs"), "protected override void OnClosing(");
        Assert.Contains("if (SavedCheck.IsChecked != true)", closing, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", closing, StringComparison.Ordinal);
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
