using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class DataQualityAndSnapshotTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public DataQualityAndSnapshotTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CaptureTrialBalance_CreatesSnapshot_WithNonEmptyHash()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "", BusinessProfile.General);

        var cash = await _db.Accounts.SingleAsync(a => a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.Code == "4101");
        var vat = await _db.Accounts.SingleAsync(a => a.Code == "2106");

        var entry = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 7, 1), "بيع");
        entry.AddLine(cash, 115m, 0m);
        entry.AddLine(sales, 0m, 100m);
        entry.AddLine(vat, 0m, 15m);
        entry.Post("JV-2026-000001");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        var service = new ReportSnapshotService(_db);
        var dto = await service.CaptureTrialBalanceAsync(
            company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var snapshots = await service.ListAsync(company.Id, "TrialBalance");
        Assert.Single(snapshots);
        Assert.Equal(dto.Id, snapshots[0].Id);
        Assert.Equal("TrialBalance", dto.ReportType);
        Assert.Equal(3, dto.RowCount);
        Assert.False(string.IsNullOrWhiteSpace(dto.Hash));
        Assert.Equal(64, dto.Hash.Length);

        var stored = await _db.ReportSnapshots.SingleAsync();
        Assert.Equal(dto.Hash, stored.Hash);
        Assert.Contains("1101", stored.PayloadJson);
    }

    [Fact]
    public async Task DataQuality_RunsWithoutThrow_AndHqBranchPresent()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "", BusinessProfile.General);

        var service = new DataQualityService(_db);
        var report = await service.RunAsync(company.Id);

        Assert.Equal(company.Id, report.CompanyId);
        Assert.True(report.CheckedAtUtc <= DateTime.UtcNow);
        Assert.NotEmpty(report.Findings);

        Assert.DoesNotContain(report.Findings, f => f.Code == "BRANCH_HQ_MISSING");
        Assert.Contains(report.Findings, f => f.Code == "DOMAIN_EVENTS_24H" && f.Severity == "Info");
    }
}
