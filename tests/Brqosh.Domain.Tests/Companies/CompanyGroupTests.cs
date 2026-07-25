using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Tests.Companies;

public class CompanyGroupTests
{
    [Fact]
    public void Create_NormalizesCodeAndCurrency()
    {
        var group = CompanyGroup.Create("grp1", "مجموعة القابضة", "sar");
        Assert.Equal("GRP1", group.Code);
        Assert.Equal("SAR", group.ReportingCurrencyCode);
    }

    [Fact]
    public void Create_RejectsInvalidCurrency()
    {
        Assert.Throws<AccountingException>(() =>
            CompanyGroup.Create("X", "اسم", "SA"));
    }
}
