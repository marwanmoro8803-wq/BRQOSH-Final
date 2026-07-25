using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Tests.Companies;

public class IntercompanyAccountMapTests
{
    [Fact]
    public void Create_accepts_valid_code()
    {
        var map = IntercompanyAccountMap.Create(1, "1104");
        Assert.Equal("1104", map.AccountCode);
    }

    [Fact]
    public void Create_rejects_invalid_group()
    {
        Assert.Throws<AccountingException>(() => IntercompanyAccountMap.Create(0, "1104"));
    }

    [Fact]
    public void Create_rejects_non_numeric_code()
    {
        Assert.Throws<AccountingException>(() => IntercompanyAccountMap.Create(1, "ABC"));
    }
}
