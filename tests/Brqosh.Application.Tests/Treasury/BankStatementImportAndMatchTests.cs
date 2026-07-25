using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class BankStatementImportAndMatchTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public BankStatementImportAndMatchTests()
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
    public void CsvParser_ParsesAmountColumn()
    {
        const string csv = """
            Date,Description,Amount,Reference
            2026-07-05,إيداع عميل,5000.00,REF-1
            2026-07-06,رسوم,-50.00,REF-2
            """;

        var lines = BankStatementCsvParser.Parse(csv);
        Assert.Equal(2, lines.Count);
        Assert.Equal(5000m, lines[0].SignedAmount);
        Assert.Equal(-50m, lines[1].SignedAmount);
    }

    [Fact]
    public void CsvParser_ParsesDebitCreditColumns()
    {
        const string csv = """
            Date,Description,Debit,Credit
            2026-07-01,تحويل وارد,1000,0
            2026-07-02,سحب,0,250
            """;

        var lines = BankStatementCsvParser.Parse(csv);
        Assert.Equal(1000m, lines[0].SignedAmount);
        Assert.Equal(-250m, lines[1].SignedAmount);
    }

    [Fact]
    public async Task ImportAndAutoMatch_ReconcilesMatchingBookLine()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة بنك", "310123456700003", BusinessProfile.General);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Domain.Sales.Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var bank = await new BankAccountService(_db).CreateAsync(
            company.Id, "BNK1", "حساب تشغيل", "الأهلي", "123", "SA0000000000000000000001", true);

        var voucherDate = new DateOnly(2026, 7, 5);
        var voucher = ReceiptVoucher.CreateDraft(
            company.Id, customer.Id, voucherDate, 5000m, ReceiptMethod.Bank, "قبض بنكي", bankAccountId: bank.Id);
        var posted = await new ReceiptVoucherPostingService(_db).PostAsync(voucher);
        Assert.NotNull(posted.JournalEntryId);

        const string csv = """
            Date,Description,Amount,Reference
            2026-07-05,إيداع عميل,5000.00,REF-1
            """;

        var statement = await new BankStatementImportService(_db)
            .ImportCsvAsync(company.Id, bank.Id, "july.csv", csv);

        var report = await new BankStatementMatcherService(_db).AutoMatchAsync(company.Id, statement.Id);
        Assert.Equal(1, report.MatchedCount);
        Assert.Equal(0, report.UnmatchedAfter);

        var reloaded = await _db.BankStatements
            .Include(s => s.Lines)
            .SingleAsync(s => s.Id == statement.Id);
        Assert.True(reloaded.Lines.Single().IsMatched);
        Assert.Equal(posted.JournalEntryId, reloaded.Lines.Single().MatchedJournalEntryId);

        var summary = await new BankReconciliationQuery(_db)
            .GetSummaryAsync(company.Id, bank.Id, voucherDate, voucherDate);
        Assert.Equal(1, summary.ReconciledCount);
    }

    [Fact]
    public async Task PostDifference_CreatesBalancedJournalEntry_ForUnmatchedFeeLine()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروق", "310123456700003", BusinessProfile.General);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var bank = await new BankAccountService(_db).CreateAsync(
            company.Id, "BNK3", "حساب", "الإنماء", "789", "SA0000000000000000000003", true);

        const string csv = """
            Date,Description,Amount,Reference
            2026-07-10,رسوم بنكية,-75.00,FEE-1
            """;

        var statement = await new BankStatementImportService(_db)
            .ImportCsvAsync(company.Id, bank.Id, "fees.csv", csv);

        var result = await new BankStatementDifferencePostingService(_db)
            .PostUnmatchedLineAsync(company.Id, statement.Id, lineIndex: 0);

        Assert.False(string.IsNullOrWhiteSpace(result.EntryNumber));

        var entry = await _db.JournalEntries
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == result.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(75m, entry.TotalDebit);
        Assert.Equal(75m, entry.TotalCredit);
        Assert.Contains(entry.Lines, l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.BankCharges && l.Debit == 75m);

        var reloaded = await _db.BankStatements
            .Include(s => s.Lines)
            .SingleAsync(s => s.Id == statement.Id);
        Assert.True(reloaded.Lines.Single().IsMatched);
        Assert.Equal(result.JournalEntryId, reloaded.Lines.Single().MatchedJournalEntryId);

        var summary = await new BankReconciliationQuery(_db)
            .GetSummaryAsync(company.Id, bank.Id, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 10));
        Assert.Equal(1, summary.ReconciledCount);
    }

    [Fact]
    public void DefaultOffsetCode_UsesIncomeForDeposits_AndChargesForWithdrawals()
    {
        Assert.Equal(SaudiChartOfAccounts.SystemCodes.OtherIncome,
            BankStatementDifferencePostingService.DefaultOffsetCode(100m));
        Assert.Equal(SaudiChartOfAccounts.SystemCodes.BankCharges,
            BankStatementDifferencePostingService.DefaultOffsetCode(-50m));
    }

    [Fact]
    public async Task CashMovementReport_SumsInflowsAndOutflows()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة تقرير", "310123456700003", BusinessProfile.General);

        var customer = Domain.Sales.Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var bank = await new BankAccountService(_db).CreateAsync(
            company.Id, "BNK2", "حساب", "الراجحي", "456", "SA0000000000000000000002", true);

        var d = new DateOnly(2026, 7, 8);
        await new ReceiptVoucherPostingService(_db).PostAsync(
            ReceiptVoucher.CreateDraft(company.Id, customer.Id, d, 3000m, ReceiptMethod.Bank, "إيداع", bankAccountId: bank.Id));

        var report = await new CashMovementReportService(_db)
            .GetBankMovementAsync(company.Id, bank.Id, d, d);

        Assert.Equal(3000m, report.TotalInflows);
        Assert.Equal(0m, report.TotalOutflows);
        Assert.Equal(1, report.TransactionCount);
    }
}
