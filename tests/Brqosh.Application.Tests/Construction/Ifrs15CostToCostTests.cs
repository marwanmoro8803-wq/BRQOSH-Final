using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

public class Ifrs15CostToCostTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public Ifrs15CostToCostTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task CostToCost_RecognizesRevenueByIncurredCostRatio()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة مقاولات", "300000000000010", BusinessProfile.Construction);

        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);

        // عقد 1,000,000 · تكلفة مقدّرة 800,000 · تكاليف مسجّلة 200,000 → 25%
        var project = ConstructionProject.Create(company.Id, "PRJ-IFRS", "مشروع تكلفة",
            customer.Id, contractValue: 1_000_000m, retentionPercent: 10m,
            estimatedTotalCost: 800_000m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        await new ProjectCostService(_db).RecordAsync(
            company.Id, project.Id, new DateOnly(2026, 1, 15),
            "تكاليف مرحلة أولى", 200_000m, "admin", postJournal: true);

        // مستخلص يطالب بـ 300,000 أعمال — لكن الإيراد المعترف = 25% × 1M = 250,000
        var billing = ProgressBilling.CreateDraft(
            company.Id, project.Id, customer.Id, new DateOnly(2026, 2, 1),
            "مستخلص نسبة تكلفة", 300_000m, 10m);

        var (recognized, percent, method) = await ProgressBillingIfrs15.ComputeAsync(_db, billing);
        Assert.Equal("cost-to-cost", method);
        Assert.Equal(25m, percent);
        Assert.Equal(250_000m, recognized);

        await new ProgressBillingPostingService(_db).PostAsync(billing);

        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == billing.JournalEntryId);
        Assert.True(entry.IsBalanced);

        var revenue = entry.Lines.Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.ContractRevenue)
            .Sum(l => l.Credit - l.Debit);
        Assert.Equal(250_000m, revenue);

        // الفرق 50,000 → إيراد مؤجل 2105
        var deferred = entry.Lines.Where(l => l.AccountCode == "2105").Sum(l => l.Credit - l.Debit);
        Assert.Equal(50_000m, deferred);
    }

    [Fact]
    public async Task WithoutEstimatedCost_FallsBackToBillingBased()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة مقاولات", "300000000000011", BusinessProfile.Construction);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);

        var project = ConstructionProject.Create(company.Id, "PRJ-BILL", "مشروع فوترة",
            customer.Id, contractValue: 1_000_000m, retentionPercent: 10m,
            estimatedTotalCost: 0m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);

        var billing = ProgressBilling.CreateDraft(
            company.Id, project.Id, customer.Id, new DateOnly(2026, 3, 1),
            "مستخلص فوترة", 100_000m, 10m);

        var (recognized, _, method) = await ProgressBillingIfrs15.ComputeAsync(_db, billing);
        Assert.Equal("billing-based", method);
        Assert.Equal(100_000m, recognized);
    }
}
