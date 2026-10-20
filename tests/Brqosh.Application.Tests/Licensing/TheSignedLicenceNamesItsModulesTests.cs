using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٦٤ · security-licensing-tenancy#2) <b>الوحداتُ تُقرأ من الترخيص الموقَّع — ووسومُ الأمس لا تُقفل أحداً.</b>
///
/// <para>‏كلُّ ترخيصٍ صدر قبل هذا يحمل <c>["core","zatca","construction"]</c>. ولو قُرئت قائمةَ وحداتٍ لفقد عميلُ التجزئة
/// ‏نقاطَ بيعه صباحَ التحديث — <b>فالحصرُ بمدخلاتٍ صريحة <c>module:</c> وحدَها</b>، وما خلا منها لا يحصر.</para>
/// </summary>
[Collection("SequentialEnv")]
public class TheSignedLicenceNamesItsModulesTests
{
    private const string Company = "منشأة الوحدات";
    private const string Machine = "MACHINE-MODULES";

    [Theory]
    [InlineData("core,zatca,construction")]
    [InlineData("all")]
    [InlineData("core")]
    [InlineData("")]
    public void YesterdaysLicences_WithoutModuleEntries_RestrictNothing(string features)
    {
        Assert.Null(LicensedModules.From(features.Split(',', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void NoFeaturesAtAll_RestrictsNothing() => Assert.Null(LicensedModules.From(null));

    [Fact]
    public void ModuleEntries_AreRead_CaseInsensitively_AndLegacyTagsBesideThemAreIgnored()
    {
        var granted = LicensedModules.From(["core", "module:Sales", "MODULE:inventory"]);

        Assert.NotNull(granted);
        Assert.Equal(new[] { ModuleKey.Sales, ModuleKey.Inventory }.OrderBy(m => m), granted!.OrderBy(m => m));
    }

    /// <summary>‏وحدةٌ لا يعرفها هذا الإصدار تُتجاهَل <b>والحصرُ قائم</b> — فترخيصٌ من إصدارٍ أحدث لا يصير مفتوحاً هنا.</summary>
    [Fact]
    public void AnUnknownModule_IsIgnored_ButTheLicenceStillRestricts()
    {
        var granted = LicensedModules.From(["module:Teleportation", "module:99"]);

        Assert.NotNull(granted);
        Assert.Empty(granted!);
    }

    [Fact]
    public void ToFeatures_RoundTrips()
    {
        var modules = new[] { ModuleKey.Sales, ModuleKey.Payroll };

        Assert.Equal(modules.OrderBy(m => m), LicensedModules.From(LicensedModules.ToFeatures(modules))!.OrderBy(m => m));
    }

    /// <summary>‏الوحداتُ تحت التوقيع: ترخيصٌ موقَّعٌ يُقرأ بوحداته، <b>وإضافةُ وحدةٍ إلى نصّه تُبطل توقيعَه</b>.</summary>
    [Fact]
    public void TheModulesAreUnderTheSignature()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var json = RsaLicenseValidator.CreateSignedLicenseJson(
            new LicensePayload(Company, Machine, "standard", DateTime.UtcNow.AddDays(30),
                ["core", .. LicensedModules.ToFeatures([ModuleKey.Sales])]),
            RsaTestKeys.PrivatePem);

        Assert.True(RsaLicenseValidator.TryReadGenuine(Company, Machine, json, out var payload));
        Assert.Equal(new[] { ModuleKey.Sales }, LicensedModules.From(payload!.Features));

        var widened = json.Replace("\"module:Sales\"", "\"module:Sales\",\"module:Payroll\"", StringComparison.Ordinal);
        Assert.NotEqual(json, widened);
        Assert.False(RsaLicenseValidator.TryReadGenuine(Company, Machine, widened, out _));
    }

    /// <summary>‏والترخيصُ الأصيلُ المنتهي يُقرأ بوحداته — فوضعُ القراءة لا يفتح ما لم يُمنح.</summary>
    [Fact]
    public void AGenuineButExpiredLicence_StillNamesItsModules()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var json = RsaLicenseValidator.CreateSignedLicenseJson(
            new LicensePayload(Company, Machine, "standard", DateTime.UtcNow.AddDays(-3),
                LicensedModules.ToFeatures([ModuleKey.Treasury])),
            RsaTestKeys.PrivatePem);

        Assert.False(RsaLicenseValidator.TryValidate(Company, Machine, json, out _));
        Assert.True(RsaLicenseValidator.TryReadGenuine(Company, Machine, json, out var payload));
        Assert.Equal(new[] { ModuleKey.Treasury }, LicensedModules.From(payload!.Features));
        Assert.False(RsaLicenseValidator.TryReadGenuine("منشأةٌ أخرى", Machine, json, out _));
    }
}
