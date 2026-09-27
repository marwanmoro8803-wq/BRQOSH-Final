using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٠ · <c>sector-modules#3</c>) <b>إيرادُ العقد يتبع نسبةَ الإنجاز تراكميّاً — وكان المؤجَّلُ لا يُطلَق، وما سبقت
/// فيه التكاليفُ الفوترةَ لا يُعترف به.</b>
///
/// <para><b>عقدٌ واحدٌ بأرقامٍ تُفرّق بين الطرق:</b> قيمتُه ١٬٠٠٠٬٠٠٠ وتكلفتُه المقدَّرة ٨٠٠٬٠٠٠. تكاليفُ ٢٠٠٬٠٠٠ ⟵ ٢٥٪ ⟵ ٢٥٠٬٠٠٠ مكتسبة؛
/// ثمّ مستخلصٌ بـ٣٠٠٬٠٠٠ ⟵ ٥٠٬٠٠٠ التزامُ عقد؛ ثمّ تكاليفُ ١٠٠٬٠٠٠ أخرى ⟵ ٣٧٫٥٪ ⟵ ٣٧٥٬٠٠٠ مكتسبة: يُطلَق الخمسون ويصير ٧٥٬٠٠٠ أصلَ
/// عقد. <b>وقبل الإصلاح</b>: الإيرادُ صفرٌ قبل المستخلص، و٢٥٠٬٠٠٠ بعده إلى الأبد، والخمسون مؤجَّلةٌ إلى الأبد.</para>
/// </summary>
public class ContractRevenueFollowsProgressCumulativelyTests : IDisposable
{
    private const string Revenue = SaudiChartOfAccounts.SystemCodes.ContractRevenue;
    private const string Deferred = SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers;
    private const string Asset = SaudiChartOfAccounts.SystemCodes.WorkInProgress;

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _projectId;

    public ContractRevenueFollowsProgressCumulativelyTests()
    {
        TestActor.Accountant();
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

    /// <summary><b>القياسُ الأوّل:</b> تكاليفُ سبقت الفوترةَ ⟵ ٢٥٠٬٠٠٠ إيراداً وأصلَ عقد — وكان صفراً.</summary>
    [Fact]
    public async Task CostsRunningAheadOfBilling_AreRecognisedAsAContractAsset()
    {
        await ProjectAsync(estimatedTotalCost: 800_000m);

        await CostAsync(200_000m, new(2026, 1, 15));

        Assert.Equal(250_000m, await CreditAsync(Revenue));
        Assert.Equal(-250_000m, await CreditAsync(Asset));
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> مستخلصٌ فوق المكتسب يؤجّل خمسين ألفاً، <b>ثمّ تلحق التكاليفُ فتُطلَق</b> ويصير الزائدُ أصلَ عقد.
    /// </summary>
    [Fact]
    public async Task ADeferralIsReleased_WhenLaterCostsCatchUp()
    {
        await ProjectAsync(estimatedTotalCost: 800_000m);
        await CostAsync(200_000m, new(2026, 1, 15));
        await BillAsync(300_000m, new(2026, 2, 1));

        Assert.Equal(250_000m, await CreditAsync(Revenue));
        Assert.Equal(50_000m, await CreditAsync(Deferred));
        Assert.Equal(0m, await CreditAsync(Asset));

        await CostAsync(100_000m, new(2026, 3, 1));

        Assert.Equal(375_000m, await CreditAsync(Revenue));
        Assert.Equal(0m, await CreditAsync(Deferred));
        Assert.Equal(-75_000m, await CreditAsync(Asset));
    }

    /// <summary><b>وإلغاءُ المستخلص يُعيد العقدَ إلى ما اكتُسب</b> — ٢٥٠٬٠٠٠ إيراداً وأصلَ عقد، ولا التزام.</summary>
    [Fact]
    public async Task CancellingABilling_RebalancesTheContract()
    {
        await ProjectAsync(estimatedTotalCost: 800_000m);
        await CostAsync(200_000m, new(2026, 1, 15));
        var billingId = await BillAsync(300_000m, new(2026, 2, 1));

        await new ProgressBillingCancellationService(_db).CancelAsync(_companyId, billingId, new(2026, 2, 10), "admin");

        Assert.Equal(250_000m, await CreditAsync(Revenue));
        Assert.Equal(0m, await CreditAsync(Deferred));
        Assert.Equal(-250_000m, await CreditAsync(Asset));
    }

    /// <summary><b>ومراجعةُ التقدير لحاقٌ تراكميّ</b> — التكلفةُ المقدَّرة مليونٌ ⟵ ٢٠٪ ⟵ ٢٠٠٬٠٠٠ لا ٢٥٠٬٠٠٠.</summary>
    [Fact]
    public async Task ARevisedEstimate_CatchesUpCumulatively()
    {
        await ProjectAsync(estimatedTotalCost: 800_000m);
        await CostAsync(200_000m, new(2026, 1, 15));

        var project = await _db.ConstructionProjects.SingleAsync(p => p.Id == _projectId);
        await new ConstructionProjectService(_db).UpdateAsync(_companyId, _projectId, project.NameAr, project.CustomerId,
            project.StartDate, project.EndDate, project.ContractValue, project.RetentionPercent, project.Location,
            project.Status, estimatedTotalCost: 1_000_000m);

        Assert.Equal(200_000m, await CreditAsync(Revenue));
        Assert.Equal(-200_000m, await CreditAsync(Asset));
    }

    /// <summary><b>شاهدُ ضبط:</b> مشروعٌ بلا تكلفةٍ مقدَّرة يُعترف فيه بما فُوتر — فلا تسويةَ تمسّه.</summary>
    [Fact]
    public async Task ABillingBasedProject_IsNotTrueUpped()
    {
        await ProjectAsync(estimatedTotalCost: 0m);
        await CostAsync(200_000m, new(2026, 1, 15));
        await BillAsync(100_000m, new(2026, 2, 1));

        Assert.Equal(100_000m, await CreditAsync(Revenue));
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == ContractRevenueTrueUp.Key(_projectId)));
    }

    /// <summary>
    /// <b>والتوزيعُ</b>: الموجبُ يُطلق المؤجَّلَ ثمّ يرفع الأصل؛ والسالبُ يُنقص الأصلَ ثمّ يؤجّل؛ وما بقي منهما معاً يُقاصّ.
    /// </summary>
    [Fact]
    public void TheAllocation_ReleasesFirst_ReducesFirst_AndOffsetsWhatRemains()
    {
        Assert.Equal(new ContractRevenueTrueUp.Movement(50m, 75m, 0m, 0m, 0m),
            ContractRevenueTrueUp.Allocate(125m, deferred: 50m, asset: 0m));
        Assert.Equal(new ContractRevenueTrueUp.Movement(0m, 0m, 250m, 0m, 0m),
            ContractRevenueTrueUp.Allocate(-250m, deferred: 50m, asset: 250m));
        Assert.Equal(new ContractRevenueTrueUp.Movement(0m, 0m, 0m, 0m, 250m),
            ContractRevenueTrueUp.Allocate(0m, deferred: 300m, asset: 250m));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task ProjectAsync(decimal estimatedTotalCost)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مقاولات", "300000000000080", BusinessProfile.Construction);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل المقاولة");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var project = ConstructionProject.Create(_companyId, "PRJ-80", "مشروع النسبة", _customerId,
            contractValue: 1_000_000m, retentionPercent: 10m, estimatedTotalCost: estimatedTotalCost);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();
        _projectId = project.Id;
    }

    private Task CostAsync(decimal amount, DateOnly on) =>
        new ProjectCostService(_db).RecordAsync(_companyId, _projectId, on, "تكاليف مرحلة", amount, "admin",
            postJournal: true);

    private async Task<long> BillAsync(decimal amount, DateOnly on)
    {
        var billing = ProgressBilling.CreateDraft(_companyId, _projectId, _customerId, on, "مستخلص", amount, 10m);
        await new ProgressBillingPostingService(_db).PostAsync(billing);
        return billing.Id;
    }

    /// <summary>الرصيدُ الدائنُ للحساب في دفتر المنشأة — والمدينُ سالب.</summary>
    private async Task<decimal> CreditAsync(string accountCode) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == accountCode)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Credit - l.Debit);
}
