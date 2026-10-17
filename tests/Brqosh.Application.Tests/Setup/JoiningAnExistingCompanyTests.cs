using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٠ · <c>installer-deployment-ops#9</c>) <b>جهازٌ ثانٍ ينضمّ إلى المنشأة القائمة بقرار، بهويّتها.</b>
/// </summary>
public class JoiningAnExistingCompanyTests
{
    private static readonly SetupState FromTheWizard = new(
        IsCompleted: true, Profile: BusinessProfile.General, CompanyName: "منشأة كتبها المُعِدّ",
        VatRegistrationNumber: "300000000000999", CompletedAtUtc: new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));

    private static readonly DatabaseIdentity Existing = new(SchemaPresent: true, CompanyId: 7,
        CompanyName: "شركة القائمة", VatNumber: "300000000000150");

    [Fact]
    public void AnEmptyDatabase_AsksNothing_AndADatabaseWithACompanyAsks()
    {
        Assert.False(JoiningAnExistingCompany.MustAsk(new DatabaseIdentity(true, null, null, null)));
        Assert.False(JoiningAnExistingCompany.MustAsk(new DatabaseIdentity(false, null, null, null)));
        Assert.True(JoiningAnExistingCompany.MustAsk(Existing));
    }

    /// <summary>
    /// <b>القياسُ الحاكم: المنضمُّ يأخذ هويّةَ القاعدة لا ما كُتب في المعالج</b> — فيُقاس ترخيصُه عليها ولا يُتَّهم لاحقاً بأنّ
    /// قاعدتَه «تحمل دفاتر منشأة أخرى».
    /// </summary>
    [Fact]
    public void Joining_TakesTheCompanyIdentityFromTheDatabase()
    {
        var joined = JoiningAnExistingCompany.Joined(FromTheWizard, Existing);

        Assert.Equal("شركة القائمة", joined.CompanyName);
        Assert.Equal("300000000000150", joined.VatRegistrationNumber);
        Assert.Equal("شركة القائمة", joined.LicensedCompanyName);
        Assert.True(joined.IsCompleted);
        Assert.True(Existing.Matches(joined.CompanyName, joined.VatRegistrationNumber));
    }

    [Fact]
    public void ALicensedNameCarriedByTheInstall_IsKept()
    {
        var joined = JoiningAnExistingCompany.Joined(FromTheWizard with { LicensedCompanyName = "الاسم المُرخَّص" }, Existing);

        Assert.Equal("الاسم المُرخَّص", joined.LicensedCompanyName);
    }

    /// <summary><b>ورفضُ الانضمام يُبقي الإعدادَ ناقصاً</b> — فيعود المعالجُ ولا يُتبنّى شيءٌ في الإقلاع التالي.</summary>
    [Fact]
    public void Declining_LeavesTheSetupIncomplete()
    {
        Assert.False(JoiningAnExistingCompany.Declined(FromTheWizard).IsCompleted);
    }

    [Fact]
    public void JoiningAnEmptyDatabase_IsAProgrammingError()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JoiningAnExistingCompany.Joined(FromTheWizard, new DatabaseIdentity(true, null, null, null)));
    }
}
