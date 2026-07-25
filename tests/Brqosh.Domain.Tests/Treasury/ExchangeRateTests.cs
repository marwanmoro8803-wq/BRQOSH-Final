using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;

namespace Brqosh.Domain.Tests.Treasury;

public class ExchangeRateTests
{
    [Fact]
    public void Create_normalizes_currency_codes()
    {
        var rate = ExchangeRate.Create(1, "usd", "sar", new DateOnly(2026, 7, 1), 3.75m, "manual");
        Assert.Equal("USD", rate.FromCurrencyCode);
        Assert.Equal("SAR", rate.ToCurrencyCode);
        Assert.Equal(3.75m, rate.Rate);
    }

    [Fact]
    public void Create_rejects_identical_currencies()
    {
        Assert.Throws<AccountingException>(() =>
            ExchangeRate.Create(1, "SAR", "SAR", new DateOnly(2026, 7, 1), 1m));
    }
}
