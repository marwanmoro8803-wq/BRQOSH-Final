using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Intelligence;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Intelligence;

public class FinancialIntelligenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public FinancialIntelligenceTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void SuggestAccountFromDescription_maps_salary_keyword()
    {
        var hint = FinancialIntelligenceService.SuggestAccountFromDescription("صرف رواتب شهر يونيو");
        Assert.NotNull(hint);
        Assert.Contains(SaudiChartOfAccounts.SystemCodes.SalariesExpense, hint);
    }

    [Fact]
    public async Task Analyze_returns_engine_id_and_sales_mtd_insight()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة ذكاء", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var svc = new FinancialIntelligenceService(_db, new DataQualityService(_db));
        var report = await svc.AnalyzeAsync(company.Id, new DateOnly(2026, 7, 31));

        Assert.Equal(FinancialIntelligenceService.EngineId, report.Engine);
        Assert.Contains(report.Insights, i => i.Code == "PERF_SALES_MTD" && i.Metric == invoice.GrandTotal());
    }

    [Fact]
    public async Task Analyze_flags_large_journal_anomaly()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة شذوذ", "310123456700003", BusinessProfile.Retail);
        ManualJournals.LiftApprovalThreshold(_db);   // ‏(accounting-core#6) قيودٌ يدويّةٌ كبيرةٌ لسببٍ غيرِ الاعتماد — منشأةٌ بمستخدمٍ واحد
        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");

        // عدة قيود صغيرة لبناء متوسط
        for (var i = 0; i < 6; i++)
        {
            var small = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 6, 1 + i), $"صغير {i}");
            small.AddLine(cash, 100m, 0m);
            small.AddLine(capital, 0m, 100m);
            await new JournalPostingService(_db).PostAsync(small);
        }

        var huge = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 6, 20), "قيد ضخم");
        huge.AddLine(cash, 50_000m, 0m);
        huge.AddLine(capital, 0m, 50_000m);
        await new JournalPostingService(_db).PostAsync(huge);

        var svc = new FinancialIntelligenceService(_db, new DataQualityService(_db));
        var report = await svc.AnalyzeAsync(company.Id, new DateOnly(2026, 6, 30));

        Assert.Contains(report.Insights, i => i.Code == "ANOM_LARGE_JE");
    }
}
