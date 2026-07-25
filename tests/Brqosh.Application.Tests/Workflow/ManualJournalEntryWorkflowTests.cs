using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Workflow;

public class ManualJournalEntryWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public ManualJournalEntryWorkflowTests()
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

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة JE-WF", "310123456700012", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    private ManualJournalEntryWorkflowService Svc() =>
        new(_db, new WorkflowEngine(_db));

    [Fact]
    public async Task FullChain_Submit_Accountant_Admin_ThenPost()
    {
        await SetupAsync();

        var cash = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var expense = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.BankCharges);

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 7, 9), "قيد يدوي اختبار");
        entry.AddLine(expense, 100m, 0m);
        entry.AddLine(cash, 0m, 100m);
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentUsername = "preparer";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(entry);
        Assert.NotNull(entry.WorkflowInstanceId);

        SecurityContext.CurrentUsername = "reviewer";
        await Svc().ApproveAsync(entry.Id);

        SecurityContext.CurrentUsername = "admin";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await Svc().ApproveAsync(entry.Id);

        SecurityContext.CurrentRole = UserRole.Administrator;
        await Svc().EnsureWorkflowApprovedAsync(entry);
        await new JournalPostingService(_db).PostAsync(entry);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
    }
}
