using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;

namespace Brqosh.Domain.Tests.Companies;

/// <summary>(أسئلةُ المالك 2026-10-24) اسمُ المدقِّق/المراجع القانونيّ للمنشأة.</summary>
public class CompanyAuditorTests
{
    private static Company NewCompany() => Company.Create("منشأة", "310123456700003", BusinessProfile.General);

    [Fact]
    public void TheAuditorName_IsTrimmed_AndEmptyMeansNone()
    {
        var company = NewCompany();
        Assert.Null(company.AuditorName);

        company.SetAuditor("  مكتب الأمانة  ");
        Assert.Equal("مكتب الأمانة", company.AuditorName);

        company.SetAuditor("   ");
        Assert.Null(company.AuditorName);
    }

    [Fact]
    public void ATooLongOrControlCharacterName_IsRefused()
    {
        var company = NewCompany();
        company.SetAuditor("مكتب");

        Assert.Throws<ArgumentException>(() => company.SetAuditor(new string('م', Company.AuditorNameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => company.SetAuditor("مكتب\u0007"));
        Assert.Equal("مكتب", company.AuditorName);

        company.SetAuditor(new string('م', Company.AuditorNameMaxLength));
        Assert.Equal(Company.AuditorNameMaxLength, company.AuditorName!.Length);
    }
}
