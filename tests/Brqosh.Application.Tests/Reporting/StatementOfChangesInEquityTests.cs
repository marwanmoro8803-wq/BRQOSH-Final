using Brqosh.Application.Reporting;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// ‏<b>قائمةُ التغيّرات في حقوق الملكية (IAS 1)</b>: كلُّ مكوِّنٍ يبدأ برصيده ويتحرّك بربح الفترة والدخل الشامل الآخر
/// والمعاملات مع الملّاك والتحويلات — وإجماليُّ أوّلها وآخرها يطابق المركزَ الماليَّ، والتحويلاتُ مجموعُها صفر.
/// </summary>
public class StatementOfChangesInEquityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public StatementOfChangesInEquityTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TheYearOfClosing_MovesProfitIntoRetainedEarnings_AsATransfer()
    {
        await SeedAsync();

        var soce = await new StatementOfChangesInEquityQuery(_db).ExecuteAsync(_companyId, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        Assert.Equal(100_000m, Row(soce, "3101").TransactionsWithOwners);
        Assert.Equal(20_000m, Row(soce, "3103").TransfersWithinEquity);
        var unclosed = Row(soce, StatementOfChangesInEquityQuery.UnclosedProfitCode);
        Assert.Equal(20_000m, unclosed.ProfitOrLoss);
        Assert.Equal(-20_000m, unclosed.TransfersWithinEquity);
        Assert.Equal(0m, unclosed.Closing);
        Assert.Equal(0m, soce.TotalTransfersWithinEquity);
        Assert.Equal(120_000m, soce.TotalClosing);
        Assert.Equal((await new BalanceSheetQuery(_db).ExecuteAsync(_companyId, new DateOnly(2025, 12, 31))).TotalEquity, soce.TotalClosing);
    }

    [Fact]
    public async Task TheNextYear_OpensWherePriorClosed_AndClassifiesEachMovement()
    {
        await SeedAsync();

        var soce = await new StatementOfChangesInEquityQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(100_000m, Row(soce, "3101").Opening);
        Assert.Equal(100_000m, Row(soce, "3101").Closing);
        Assert.Equal(20_000m, Row(soce, "3103").Opening);
        Assert.Equal(-2_000m, Row(soce, "3103").TransfersWithinEquity);
        Assert.Equal(2_000m, Row(soce, "3102").TransfersWithinEquity);
        Assert.Equal(-5_000m, Row(soce, "3105").TransactionsWithOwners);
        Assert.Equal(1_500m, Row(soce, SaudiChartOfAccounts.SystemCodes.TranslationReserve).OtherComprehensiveIncome);
        Assert.Equal(8_000m, Row(soce, StatementOfChangesInEquityQuery.UnclosedProfitCode).ProfitOrLoss);
        Assert.Equal(9_500m, soce.TotalComprehensiveIncome);
        Assert.Equal(0m, soce.TotalTransfersWithinEquity);

        var sheet = new BalanceSheetQuery(_db);
        Assert.Equal((await sheet.ExecuteAsync(_companyId, new DateOnly(2025, 12, 31))).TotalEquity, soce.TotalOpening);
        Assert.Equal((await sheet.ExecuteAsync(_companyId, new DateOnly(2026, 12, 31))).TotalEquity, soce.TotalClosing);
        Assert.Equal(124_500m, soce.TotalClosing);
    }

    [Fact]
    public async Task AnInvertedPeriod_IsRefused_AndTheServiceIsRegistered()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<AccountingException>(() =>
            new StatementOfChangesInEquityQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
        Assert.IsAssignableFrom<IStatementOfChangesInEquityQuery>(new StatementOfChangesInEquityQuery(_db));
    }

    private static EquityComponentMovementDto Row(StatementOfChangesInEquityDto soce, string code) =>
        Assert.Single(soce.Components, c => c.AccountCode == code);

    private async Task SeedAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("منشأة الملكية", "300000000000155", BusinessProfile.General);
        _companyId = company.Id;

        await PostAsync(new DateOnly(2025, 1, 1), "Test:capital", ("1101", 100_000m, 0m), ("3101", 0m, 100_000m));
        await PostAsync(new DateOnly(2025, 3, 1), "Test:sale", ("1101", 30_000m, 0m), ("4101", 0m, 30_000m));
        await PostAsync(new DateOnly(2025, 4, 1), "Test:cost", ("5101", 10_000m, 0m), ("1101", 0m, 10_000m));
        await PostAsync(new DateOnly(2025, 12, 31), ReportingJournalEntries.YearEndClosePrefix + "2025",
            ("4101", 30_000m, 0m), ("5101", 0m, 10_000m), ("3103", 0m, 20_000m));

        await PostAsync(new DateOnly(2026, 2, 1), "Test:reserve", ("3103", 2_000m, 0m), ("3102", 0m, 2_000m));
        await PostAsync(new DateOnly(2026, 3, 1), "Test:drawings", ("3105", 5_000m, 0m), ("1101", 0m, 5_000m));
        await PostAsync(new DateOnly(2026, 4, 1), "Test:sale2", ("1101", 8_000m, 0m), ("4101", 0m, 8_000m));
        await PostAsync(new DateOnly(2026, 6, 30), "Test:cta", ("1101", 1_500m, 0m), (SaudiChartOfAccounts.SystemCodes.TranslationReserve, 0m, 1_500m));
    }

    private async Task PostAsync(DateOnly date, string source, params (string Code, decimal Debit, decimal Credit)[] lines)
    {
        var entry = JournalEntry.CreateDraft(_companyId, date, source, sourceDocument: source);
        foreach (var (code, debit, credit) in lines)
            entry.AddLine(await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code), debit, credit);
        await new JournalPostingService(_db).PostAsync(entry);
    }
}
