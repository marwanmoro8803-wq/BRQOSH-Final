using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class MonthEndCloseOrchestratorTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public MonthEndCloseOrchestratorTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task ClosePeriod_WhenTrialBalanceBalanced_ClosesFiscalPeriod()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة إقفال", "310123456700003", Domain.Modules.BusinessProfile.General);

        var period = FiscalPeriod.Create(company.Id, 2026, 7);
        _db.FiscalPeriods.Add(period);
        await _db.SaveChangesAsync();

        var trialBalance = new TrialBalanceQueryAdapter(_db);
        var result = await new MonthEndCloseOrchestrator(_db, trialBalance)
            .ClosePeriodAsync(company.Id, period.Id, "admin");

        Assert.True(result.TrialBalanceBalanced);
        Assert.True(result.PeriodClosed);
        Assert.Equal("2026-07", result.PeriodCode);

        var reloaded = await _db.FiscalPeriods.SingleAsync(p => p.Id == period.Id);
        Assert.Equal(FiscalPeriodStatus.Closed, reloaded.Status);
    }

    private sealed class TrialBalanceQueryAdapter(BrqoshDbContext db) : Application.Reporting.ITrialBalanceQuery
    {
        public async Task<IReadOnlyList<Application.Reporting.TrialBalanceDto>> ExecuteAsync(
            long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
        {
            var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, fromDate, toDate);
            return rows.Select(r => new Application.Reporting.TrialBalanceDto(
                r.AccountCode, r.AccountNameAr, r.Type, r.TotalDebit, r.TotalCredit)).ToList();
        }
    }
}
