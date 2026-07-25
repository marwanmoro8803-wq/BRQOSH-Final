using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class ConsolidatedFinancialStatementsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly ExchangeRateService _rates;

    public ConsolidatedFinancialStatementsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _rates = new ExchangeRateService(_db);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task ConsolidatedIncomeStatement_uses_average_rate()
    {
        var (groupId, companyId) = await SeedUsdGroupCompanyAsync();
        await PostCashSaleAsync(companyId, 100m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 1), 3.70m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 31), 3.80m);

        var result = await new ConsolidatedIncomeStatementQuery(_db, _rates).ExecuteAsync(
            groupId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Equal("SAR", result.ReportingCurrencyCode);
        Assert.Equal(100m * 3.75m, result.TotalRevenue, 2);
    }

    [Fact]
    public async Task ConsolidatedBalanceSheet_uses_closing_rate_and_balances()
    {
        var (groupId, companyId) = await SeedUsdGroupCompanyAsync();
        await PostCashSaleAsync(companyId, 100m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 31), 3.75m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 1), 3.70m);

        var result = await new ConsolidatedBalanceSheetQuery(_db, _rates).ExecuteAsync(
            groupId, new DateOnly(2026, 7, 31));

        var cash = result.Assets.Single(a => a.AccountCode == "1101");
        Assert.Equal(115m * 3.75m, cash.Amount);
        Assert.True(result.IsBalanced, $"A={result.TotalAssets} L+E={result.TotalLiabilitiesAndEquity}");
    }

    [Fact]
    public async Task FxClosingTranslation_posts_cta_for_rate_spread()
    {
        var (groupId, companyId) = await SeedUsdGroupCompanyAsync();
        await PostCashSaleAsync(companyId, 100m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 1), 3.70m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 15), 3.70m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 31), 3.75m);

        var entry = await new FxClosingTranslationService(_db, _rates).TranslatePeriodAsync(
            companyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "admin");

        Assert.NotNull(entry);
        Assert.Equal(JournalEntryStatus.Posted, entry!.Status);
        var avg = await _rates.GetAverageRateAsync(
            companyId, "USD", "SAR", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var closing = await _rates.GetRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 31));
        var expectedCta = FxTranslationCalculator.ComputePeriodCtaAdjustment(100m, avg, closing);
        var cta = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.TranslationReserve);
        var ctaLine = entry.Lines.Single(l => l.AccountId == cta.Id);
        if (expectedCta > 0)
        {
            Assert.Equal(0m, ctaLine.Debit);
            Assert.Equal(expectedCta, ctaLine.Credit);
        }
        else
        {
            Assert.Equal(Math.Abs(expectedCta), ctaLine.Debit);
            Assert.Equal(0m, ctaLine.Credit);
        }
    }

    private async Task<(long GroupId, long CompanyId)> SeedUsdGroupCompanyAsync()
    {
        var group = CompanyGroup.Create("GRP-CFS", "مجموعة تقارير", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة دولار", "310123456700010", BusinessProfile.Retail);
        company.AssignCompanyGroup(group.Id);
        company.SetCurrencies("USD", "USD");
        await _db.SaveChangesAsync();
        return (group.Id, company.Id);
    }

    private async Task PostCashSaleAsync(long companyId, decimal subtotal)
    {
        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "4101");
        var vat = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "2106");
        var vatAmt = Math.Round(subtotal * 0.15m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + vatAmt;

        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 7, 5), "بيع");
        entry.AddLine(cash, total, 0m);
        entry.AddLine(sales, 0m, subtotal);
        if (vatAmt > 0)
            entry.AddLine(vat, 0m, vatAmt);
        entry.Post("JE-CFS");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _connection.Dispose();
    }
}
