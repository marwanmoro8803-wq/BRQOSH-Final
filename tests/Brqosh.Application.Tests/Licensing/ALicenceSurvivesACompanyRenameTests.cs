using Brqosh.Application.Setup;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// (م-٣ · ن-٣٥) <b>تصحيحُ خطأٍ مطبعيٍّ في اسم المنشأة كان يُقفل الإنتاج.</b>
///
/// <para><b>سلسلةُ العطل:</b> <c>App.xaml.cs</c> كان يقيس الترخيصَ على
/// <c>setup.CompanyName</c> — <b>وهو الحقلُ الذي يُعيد الملفُّ نفسُه كتابتَه</b> من هويّة
/// القاعدة بعد قبولِ المالك. و«إعدادات المنشأة» تُتيح تحريرَ الاسم. فمن صحّح حرفاً وقبِل
/// المزامنة، <b>وجد عند الإقلاع التالي «الترخيص لمنشأة أخرى»</b> — والمنشأةُ منشأتُه،
/// والجهازُ جهازُه، والملفُّ موقَّعٌ صحيحاً.</para>
///
/// <para><b>ولم يكن ذلك سهواً بل علاجاً لمشكلةٍ مقابلة:</b> المزامنةُ أُضيفت لأنّ من يُصحّح
/// الاسمَ كان يُتَّهم لاحقاً بأنّ قاعدتَه «تحمل دفاتر منشأة أخرى». <b>فعلاجُ الأولى صار
/// سببَ الثانية</b>، والحلُّ فصلُ الحقلين لا التنازلُ عن أحدهما.</para>
///
/// <para><b>ولم يُفكَّ الرباط — وهذا شرطٌ لا تفصيل.</b> فأسهلُ «إصلاحٍ» أن يُحذف فحصُ الاسم،
/// وعندها يعمل ترخيصُ منشأةٍ لأيّ اسمٍ على الجهاز نفسِه. <b>والاختبارُ الثالث هنا يمنع ذلك
/// صراحةً</b>: اسمٌ آخرُ ما زال يُردّ بـ<c>CompanyMismatch</c>.</para>
/// </summary>
public class ALicenceSurvivesACompanyRenameTests
{
    private const string IssuedTo = "مؤسسة برقش لتقنية المعلومات";
    private const string AfterTypoFix = "مؤسسة برقش لتقنية المعلومات المحدودة";

    private static SetupState Installed(string? licensedName) => new(
        IsCompleted: true,
        Profile: BusinessProfile.General,
        CompanyName: IssuedTo,
        VatRegistrationNumber: "310000000000003",
        CompletedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        LicensedCompanyName: licensedName);

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> يُصحَّح الاسمُ المعروضُ فيبقى الترخيصُ صالحاً.
    ///
    /// <para>ويُحاكى هنا ما يفعله <c>App.xaml.cs</c> حرفيّاً: يُشتقّ الاسمُ المُرخَّص
    /// (<c>LicensedCompanyName ?? CompanyName</c>) ثمّ يُقاس عليه.</para>
    /// </summary>
    [Fact]
    public void RenamingTheCompany_DoesNotInvalidateTheLicence()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var machineId = LicenseService.MachineId();
        var licence = RsaTestKeys.Sign(IssuedTo, machineId);

        // ‏تنصيبٌ ثُبّت فيه الاسمُ المُرخَّص، ثمّ صحّح المالكُ الاسمَ المعروضَ وقبِل المزامنة.
        var setup = Installed(IssuedTo) with { CompanyName = AfterTypoFix };

        var licensedName = setup.LicensedCompanyName ?? setup.CompanyName;
        var verdict = RsaLicenseValidator.Validate(licensedName, machineId, licence, out _);

        Assert.Equal(LicenseVerdict.Valid, verdict);
    }

    /// <summary>
    /// <b>ومزامنةُ الهويّة لا تمسّ الاسمَ المُرخَّص</b> — وهو ما يجعل ما سبق ممكناً.
    ///
    /// <para>والتوكيدُ على <c>with</c> ليس شكليّاً: سطرٌ واحدٌ يُضاف إليه هناك
    /// (<c>LicensedCompanyName = identity.CompanyName</c>) يُعيد العطلَ كاملاً، ويبدو
    /// «تنظيفاً» لمن يقرأ.</para>
    /// </summary>
    [Fact]
    public void TheIdentitySync_RewritesTheDisplayedName_AndLeavesTheLicensedOne()
    {
        var setup = Installed(IssuedTo);

        var synced = setup with { CompanyName = AfterTypoFix, VatRegistrationNumber = "310000000000011" };

        Assert.Equal(AfterTypoFix, synced.CompanyName);
        Assert.Equal(IssuedTo, synced.LicensedCompanyName);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من القياس:</b> الرباطُ لم يُفكّ.
    ///
    /// <para>فترخيصٌ صادرٌ لمنشأةٍ لا يعمل لاسمٍ آخر، ولو على الجهاز نفسِه. <b>وبغير هذا
    /// التوكيد لا يُميَّز الإصلاحُ عن حذفِ الفحص</b> — وكلاهما يجعل الاختبارَ الأوّلَ أخضر.</para>
    /// </summary>
    [Fact]
    public void ALicenceOfOneCompany_StillFailsForAnother()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var machineId = LicenseService.MachineId();
        var licence = RsaTestKeys.Sign(IssuedTo, machineId);

        var verdict = RsaLicenseValidator.Validate(AfterTypoFix, machineId, licence, out _);

        Assert.Equal(LicenseVerdict.CompanyMismatch, verdict);
    }

    /// <summary>
    /// <b>وتنصيبٌ يسبق الحقلَ يعمل كما كان</b> — <c>null</c> ترجع إلى الاسم المعروض،
    /// فلا تنكسر ترقيةٌ فوق تنصيبٍ قائم.
    /// </summary>
    [Fact]
    public void AnInstallationOlderThanTheField_FallsBackToTheDisplayedName()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var machineId = LicenseService.MachineId();
        var licence = RsaTestKeys.Sign(IssuedTo, machineId);

        var setup = Installed(licensedName: null);
        var licensedName = setup.LicensedCompanyName ?? setup.CompanyName;

        Assert.Equal(IssuedTo, licensedName);
        Assert.Equal(LicenseVerdict.Valid,
            RsaLicenseValidator.Validate(licensedName, machineId, licence, out _));
    }

    /// <summary>
    /// <b>إثباتُ أكوادٍ ثابت — وهو الحارسُ الحقيقيّ.</b>
    ///
    /// <para>فالتوكيداتُ أعلاه تقيس <c>SetupState</c> و<c>RsaLicenseValidator</c>، <b>ولا
    /// تُشغّل بوّابةَ الإقلاع</b> — وهي في <c>App.xaml.cs</c> ولا تُختبَر بلا واجهة. فيُشترط
    /// نصّاً أنّ البوّابةَ تقيس على الاسم المُرخَّص، <b>وأنّ كتلةَ المزامنة لا تُسنده</b>.</para>
    ///
    /// <para>ويتبع هذا سابقةَ <c>OrchestratorEventWiringEvidenceTests</c> في هذا المشروع.</para>
    /// </summary>
    [Fact]
    public void TheStartupGate_MeasuresTheLicensedName_AndTheSyncNeverAssignsIt()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "Brqosh.Desktop", "App.xaml.cs"));

        var code = string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.Contains("setup!.LicensedCompanyName ?? setup.CompanyName", code, StringComparison.Ordinal);
        Assert.Contains("LicenseStore.ValidateForCompany(licensedName", code, StringComparison.Ordinal);

        // ‏وإسنادٌ واحدٌ فقط مسموح: التثبيتُ بعد القبول. وأيُّ إسنادٍ ثانٍ يُعيد العطل.
        var assignments = code.Split("LicensedCompanyName =").Length - 1;
        Assert.True(assignments == 1,
            $"‏وُجد {assignments} إسناداً لـLicensedCompanyName في App.xaml.cs — والمسموحُ واحدٌ "
            + "(التثبيتُ بعد قبولِ الترخيص). وإسنادٌ في كتلة مزامنة الهويّة يُعيد قفلَ الإنتاج.");
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
