using Brqosh.Application.Treasury;
using Brqosh.Domain.Treasury;

namespace Brqosh.Application.Tests.Treasury;

public class SamaFxRateProviderTests
{
    [Fact]
    public async Task FetchRates_from_json_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-sama-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
            {"date":"2026-07-01","rates":{"USD_SAR":3.75,"EUR":4.10}}
            """);

        try
        {
            var provider = new Infrastructure.Treasury.SamaFxRateProvider(jsonFilePath: path);
            var quotes = await provider.FetchRatesAsync("SAR", ["USD", "EUR"], new DateOnly(2026, 7, 1));
            Assert.Equal(2, quotes.Count);
            Assert.Contains(quotes, q => q.FromCurrency == "USD" && q.Rate == 3.75m);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FetchRates_without_config_throws()
    {
        var provider = new Infrastructure.Treasury.SamaFxRateProvider();
        await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(() =>
            provider.FetchRatesAsync("SAR", ["USD"], new DateOnly(2026, 7, 1)));
    }
}
