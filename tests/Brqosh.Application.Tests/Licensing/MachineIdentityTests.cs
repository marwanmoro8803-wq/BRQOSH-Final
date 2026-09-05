using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// (CEEF-237 · ت-٥) <b>هويّةُ الجهاز تصمد لتحديث ويندوز وتبديل الحساب — والقديمةُ تبقى مقبولة.</b>
///
/// <para><b>ما كان:</b> <c>SHA256(MachineName|UserName|OSVersion)</c> — وثلاثتُها تتغيّر:
/// <c>OSVersion</c> مع كلّ تحديث ميزاتٍ سنويّ، و<c>UserName</c> حين يفتح موظّفٌ آخرُ الجهازَ
/// بحسابه، و<c>MachineName</c> حين يُعاد تسميةُ الجهاز. <b>فترخيصٌ صحيحٌ يُرفض فجأةً صباحَ
/// يومٍ لم يتغيّر فيه شيءٌ في نظر العميل</b> — وقد صار هذا حمّالاً بعد أن صار التفعيلُ
/// إلزامياً في الإنتاج.</para>
///
/// <para><b>والانتقالُ لا يكسر أحداً:</b> تراخيصُ صدرت بالصيغة القديمة تبقى مقبولةً، وإلا
/// توقّف كلُّ عميلٍ يعمل في اللحظة التي يُحدَّث فيها التطبيق.</para>
/// </summary>
public class MachineIdentityTests
{
    [Fact]
    public void TheIdentityIsStableAcrossCalls()
    {
        Assert.Equal(LicenseService.MachineId(), LicenseService.MachineId());
    }

    [Fact]
    public void TheIdentityKeepsTheShapeTheVendorToolExpects()
    {
        var id = LicenseService.MachineId();

        Assert.Equal(16, id.Length);
        Assert.True(id.All(Uri.IsHexDigit), $"المعرّف ليس سداسيّاً: {id}");
    }

    [Fact]
    public void OnWindows_TheIdentityNoLongerFollowsTheOldVolatileFormula()
    {
        // ‏لو بقيا متطابقَين على ويندوز فالمعرّفُ ما زال يُشتقّ من اسم المستخدم وإصدار النظام،
        // ‏أي أنّ الإصلاح لم يقع. (وعلى نظامٍ آخر يتساويان عمداً، فيُتخطّى الحكم.)
        if (!OperatingSystem.IsWindows()) return;

        Assert.NotEqual(LicenseService.LegacyMachineId(), LicenseService.MachineId());
    }

    [Fact]
    public void BothIdentitiesAreOffered_SoAnAlreadyIssuedLicenceKeepsWorking()
    {
        var accepted = LicenseService.AcceptableMachineIds();

        Assert.Contains(LicenseService.MachineId(), accepted);
        Assert.Contains(LicenseService.LegacyMachineId(), accepted);
    }

    [Fact]
    public void ALicenceBoundToTheOldIdentity_IsStillAccepted()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var licence = RsaTestKeys.Sign("منشأة الانتقال", LicenseService.LegacyMachineId());

        // ‏المسارُ الحقيقيّ يمرّ عبر LicenseStore الذي يمرّر المعرّفَ القديم صراحةً.
        var verdict = RsaLicenseValidator.Validate(
            "منشأة الانتقال", LicenseService.MachineId(), licence, out _,
            alsoAcceptMachineId: LicenseService.LegacyMachineId());

        Assert.Equal(LicenseVerdict.Valid, verdict);
    }

    [Fact]
    public void ALicenceBoundToAnotherMachine_IsStillRefused()
    {
        // ‏قبولُ القديم لا يعني قبولَ أيّ شيء — وإلا صار الانتقالُ ثغرة.
        using var keys = RsaTestKeys.UseForValidation();
        var licence = RsaTestKeys.Sign("منشأة أخرى", "0123456789ABCDEF");

        var verdict = RsaLicenseValidator.Validate(
            "منشأة أخرى", LicenseService.MachineId(), licence, out _);

        Assert.Equal(LicenseVerdict.MachineMismatch, verdict);
    }
}
