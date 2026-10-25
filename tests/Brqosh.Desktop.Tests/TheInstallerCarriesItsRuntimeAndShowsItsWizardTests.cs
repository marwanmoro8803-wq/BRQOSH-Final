using System.IO;
using System.Xml.Linq;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (التنصيبُ يحمل كلَّ ما يلزمه ويُرحّب) <b>المُنصِّبُ لا يطلب من العميل تنزيلَ شيء</b>: وقتُ تشغيل .NET مضمَّنٌ في الحزمة (نشرٌ self-contained) فلا
/// شرطَ مسبقاً ولا إنترنت — وهذا يحلّ محلَّ <c>TheInstallerRequiresTheRuntimeItRunsOnTests</c> الذي كان يحرس شرطَ «.NET 10 مُثبَّتٌ سلفاً».
/// وللمُنصِّب <b>واجهةُ ترحيبٍ</b> (ترحيب ← الاتفاقيّة ← المجلّد ← تأكيد ← إنهاء)، وفي الإزالةِ حوارٌ <b>يسأل عن حذف البيانات</b> وافتراضُه الإبقاء.
/// </summary>
public class TheInstallerCarriesItsRuntimeAndShowsItsWizardTests
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }

    private static XDocument Doc(string name) => XDocument.Load(Path.Combine(Root(), "installer", name));

    [Fact]
    public void ThePublishIsSelfContained_SoNoRuntimeIsDownloaded()
    {
        var publish = File.ReadAllText(Path.Combine(Root(), "installer", "publish.ps1"));
        Assert.Contains("--self-contained true", publish, StringComparison.Ordinal);
        Assert.DoesNotContain("--self-contained false", publish, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInstallerNoLongerRefusesAMachineWithoutADotNetRuntime()
    {
        var launches = Doc("brqosh.wxs").Descendants(Wix + "Launch")
            .Select(l => (string?)l.Attribute("Message") ?? "").ToList();
        Assert.DoesNotContain(launches, m => m.Contains(".NET", StringComparison.Ordinal));
    }

    [Fact]
    public void TheWizardHasWelcomeLicenseAndFolderPages_FromTheSharedEulaText()
    {
        var wxs = File.ReadAllText(Path.Combine(Root(), "installer", "brqosh.wxs"));
        Assert.Contains("UIRef Id=\"BrqoshInstallDir\"", wxs, StringComparison.Ordinal);
        Assert.Contains("WixUILicenseRtf", wxs, StringComparison.Ordinal);
        Assert.Contains("WixUIBannerBmp", wxs, StringComparison.Ordinal);

        // ‏والاتفاقيّةُ في المُنصِّب هي eula.txt نفسُه — لا نسخةٌ ثانية تنحرف.
        var assets = File.ReadAllText(Path.Combine(Root(), "installer", "make-ui-assets.ps1"));
        Assert.Contains(@"assets\legal\eula.txt", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLicenceRtfEscapesArabicLettersWithTheirNumber()
    {
        // ‏`"\u$signed?"` يُقرأ في PowerShell 7 متغيّراً اسمُه «signed?» فيخرج `\u` بلا رقمٍ وتضيع الحروفُ العربيّة من الاتفاقيّة.
        var assets = File.ReadAllText(Path.Combine(Root(), "installer", "make-ui-assets.ps1"));
        Assert.Contains("\\u${signed}?", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u$signed?", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEnglishPackageAndTheLaunchCheckboxAreBuilt_WithoutAnUnsignedLauncherExe()
    {
        // ‏خيارُ اللغة = ملفٌّ إنجليزيٌّ بجوار العربيّ؛ مُشغِّلٌ غيرُ موقَّع كشفه Defender فحذفه فلا يُبنى.
        var build = File.ReadAllText(Path.Combine(Root(), "installer", "build-msi.ps1"));
        Assert.Contains("BRQOSH-Setup-en.msi", build, StringComparison.Ordinal);
        Assert.DoesNotContain("csc.exe", build, StringComparison.Ordinal);

        var ui = File.ReadAllText(Path.Combine(Root(), "installer", "ui.wxs"));
        Assert.Contains("WIXUI_EXITDIALOGOPTIONALCHECKBOX", ui, StringComparison.Ordinal);
        Assert.Contains("LaunchApplication", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAppDeclaresItselfToWindows_ManifestDpiAndDisplayName()
    {
        var manifest = File.ReadAllText(Path.Combine(Root(), "src", "Brqosh.Desktop", "app.manifest"));
        Assert.Contains("asInvoker", manifest, StringComparison.Ordinal);   // ‏لا صلاحيّاتِ مسؤولٍ للتشغيل
        Assert.Contains("PerMonitorV2", manifest, StringComparison.Ordinal);
        Assert.Contains("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a", manifest, StringComparison.Ordinal);   // ‏Windows 10/11

        var project = File.ReadAllText(Path.Combine(Root(), "src", "Brqosh.Desktop", "Brqosh.Desktop.csproj"));
        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", project, StringComparison.Ordinal);
        Assert.Contains("<AssemblyTitle>BRQOSH ERP</AssemblyTitle>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLogoAnimatesWhereItIsTheHero_LoginAndSetupWizard()
    {
        var root = Path.Combine(Root(), "src", "Brqosh.Desktop", "Views");
        Assert.Contains("Animate=\"True\"", File.ReadAllText(Path.Combine(root, "LoginWindow.xaml")), StringComparison.Ordinal);
        Assert.Contains("Animate=\"True\"", File.ReadAllText(Path.Combine(root, "SetupWizardWindow.xaml")), StringComparison.Ordinal);
        // ‏وتُحترم «تقليل الحركة» في ويندوز.
        Assert.Contains("SystemParameters.ClientAreaAnimation", File.ReadAllText(Path.Combine(root, "Controls", "BrandLogo.xaml.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRemoveFlowAsksAboutDataWithKeepAsTheDefault()
    {
        var ui = Doc("ui.wxs");

        // ‏المربّعُ يربط خاصّيّةَ REMOVEDATA التي لا قيمةَ افتراضيّةَ لها ⟵ غيرُ مؤشَّر ⟵ البياناتُ تبقى.
        var property = ui.Descendants(Wix + "Property").Single(p => (string?)p.Attribute("Id") == "REMOVEDATA");
        Assert.Null(property.Attribute("Value"));

        var dialog = ui.Descendants(Wix + "Dialog").Single(d => (string?)d.Attribute("Id") == "UninstallDataDlg");
        var check = dialog.Descendants(Wix + "Control").Single(c => (string?)c.Attribute("Id") == "RemoveDataCheck");
        Assert.Equal("REMOVEDATA", (string?)check.Attribute("Property"));

        // ‏والحذفُ مشروطٌ بالإزالة الكاملة وبـREMOVEDATA=1 معاً — وبلا انتحال (يصل إلى مجلّد المستخدم).
        var condition = ui.Descendants(Wix + "Custom").Single(c => (string?)c.Attribute("Action") == "RemoveUserDataCmd");
        var text = (string)condition.Attribute("Condition")!;
        Assert.Contains("REMOVE=\"ALL\"", text, StringComparison.Ordinal);
        Assert.Contains("REMOVEDATA=\"1\"", text, StringComparison.Ordinal);

        // ‏ولا يمسّ إلا مجلّدَ البيانات: الأمرُ حذفٌ لمسارٍ واحد.
        var command = ui.Descendants(Wix + "SetProperty").Single(s => (string?)s.Attribute("Id") == "RemoveUserDataCmd");
        Assert.Contains("rmdir /s /q", (string)command.Attribute("Value")!, StringComparison.Ordinal);
        Assert.Contains("[USERDATAPATH]", (string)command.Attribute("Value")!, StringComparison.Ordinal);
    }
}
