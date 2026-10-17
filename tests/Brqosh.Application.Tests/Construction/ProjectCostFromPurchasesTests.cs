using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٤٤ · <c>sector-modules#9</c>) <b>تكلفةُ المشروع تأتي من فاتورة المورّد.</b>
///
/// <para><b>ما كان:</b> مصدرُ تكلفة المشروع الوحيدُ حوارٌ يدويٌّ يُدين الصندوقَ دائماً — فمشترياتُ
/// الموقع بفاتورةٍ آجلةٍ وضريبةٍ قابلةٍ للخصم إمّا لا تبلغ المشروعَ، وإمّا تُسجَّل مرّتين: مرّةً
/// مشترياتٍ على المورّد ومرّةً تكلفةَ مشروعٍ من الصندوق.</para>
/// </summary>
public class ProjectCostFromPurchasesTests : IDisposable
{
    private const string ProjectCosts = "5105";
    private const string Purchases = "5102";

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _projectId;
    private long _supplierId;

    public ProjectCostFromPurchasesTests()
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

    /// <summary>
    /// <b>القياسُ الحاكم: فاتورةُ مشروعٍ تُدين تكاليفَ المشاريع بوعائها، والضريبةُ مدخلاتٌ، والمورّدُ دائن —
    /// وتكلفةُ المشروع من القيد نفسِه بلا قيدٍ ثانٍ.</b>
    /// </summary>
    [Fact]
    public async Task AProjectInvoice_DebitsProjectCosts_AndRecordsTheCostOnce()
    {
        await SetupAsync();

        var invoice = await PostAsync(10_000m, vatDeductible: true);

        Assert.Equal(10_000m, await DebitAsync(ProjectCosts));
        Assert.Equal(0m, await DebitAsync(Purchases));
        Assert.Equal(1_500m, await DebitAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
        Assert.Equal(-11_500m, await DebitAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));

        var cost = await _db.ProjectCostEntries.SingleAsync();
        Assert.Equal(10_000m, cost.Amount);
        Assert.Equal(_projectId, cost.ProjectId);
        Assert.Equal(invoice.JournalEntryId, cost.JournalEntryId);
        Assert.Equal(10_000m, await new ProjectCostService(_db).IncurredCostAsync(_companyId, _projectId));
        Assert.Equal(1, await _db.JournalEntries.CountAsync(e => e.SourceDocument!.StartsWith("PurchaseInvoice")));
    }

    /// <summary><b>ومورّدٌ غيرُ مسجَّلٍ: الضريبةُ تكلفةٌ على المشروع</b> لا مدخلاتٌ قابلةٌ للخصم.</summary>
    [Fact]
    public async Task ANonDeductibleVat_IsPartOfTheProjectCost()
    {
        await SetupAsync();

        await PostAsync(10_000m, vatDeductible: false);

        Assert.Equal(11_500m, (await _db.ProjectCostEntries.SingleAsync()).Amount);
        Assert.Equal(0m, await DebitAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
    }

    /// <summary>
    /// <b>والإنجازُ يتبع التكلفة</b>: عقدٌ بمليونٍ وتكلفةٍ مقدَّرةٍ بثمانمئة ألف — فاتورةُ مئتي ألفٍ
    /// تُعترف بربع الإيراد كما لو سُجّلت التكلفةُ من الحوار.
    /// </summary>
    [Fact]
    public async Task TheContractRevenue_FollowsTheInvoicedCost()
    {
        await SetupAsync(estimatedTotalCost: 800_000m);

        await PostAsync(200_000m, vatDeductible: true);

        Assert.Equal(250_000m, -await DebitAsync(SaudiChartOfAccounts.SystemCodes.ContractRevenue));
    }

    /// <summary>
    /// <b>وإلغاءُ الفاتورة يُسقط تكلفتَها والإيرادَ الذي بُني عليها</b> — كان الإيرادُ يبقى لأنّ التسويةَ
    /// تخرج مبكّراً حين تصير التكلفةُ صفراً.
    /// </summary>
    [Fact]
    public async Task CancellingTheInvoice_ReversesTheCost_AndTheRevenueBuiltOnIt()
    {
        await SetupAsync(estimatedTotalCost: 800_000m);
        var invoice = await PostAsync(200_000m, vatDeductible: true);

        await new PurchaseInvoiceCancellationService(_db).CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 5, 20), "admin");

        Assert.True((await _db.ProjectCostEntries.SingleAsync()).IsReversed);
        Assert.Equal(0m, await new ProjectCostService(_db).IncurredCostAsync(_companyId, _projectId));
        Assert.Equal(0m, await DebitAsync(SaudiChartOfAccounts.SystemCodes.ContractRevenue));
        Assert.Equal(0m, await DebitAsync(ProjectCosts));
    }

    [Fact]
    public async Task ACancelledProject_TakesNoNewCost()
    {
        await SetupAsync();
        var project = await _db.ConstructionProjects.SingleAsync(p => p.Id == _projectId);
        await new ConstructionProjectService(_db).UpdateAsync(_companyId, _projectId, project.NameAr, project.CustomerId,
            project.StartDate, project.EndDate, project.ContractValue, project.RetentionPercent, project.Location,
            ConstructionProjectStatus.Cancelled, estimatedTotalCost: project.EstimatedTotalCost);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => PostAsync(1_000m, vatDeductible: true));

        Assert.Contains("لا تُحمَّل عليه", ex.Message, StringComparison.Ordinal);
        Assert.False(await _db.ProjectCostEntries.AnyAsync());
    }

    /// <summary><b>وبلا مشروعٍ لا يتغيّر شيء</b> — المشترياتُ مشترياتٌ ولا تكلفةَ مشروع.</summary>
    [Fact]
    public async Task WithoutAProject_ThePurchaseIsAPurchase()
    {
        await SetupAsync();

        await PostAsync(1_000m, vatDeductible: true, withProject: false);

        Assert.Equal(1_000m, await DebitAsync(Purchases));
        Assert.Equal(0m, await DebitAsync(ProjectCosts));
        Assert.False(await _db.ProjectCostEntries.AnyAsync());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync(decimal estimatedTotalCost = 0m)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مقاولات", "300000000000144", BusinessProfile.Construction);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل المقاولة");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(_companyId, "مورّد مواد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;

        var project = ConstructionProject.Create(_companyId, "PRJ-144", "مشروع الموقع", customer.Id,
            contractValue: 1_000_000m, retentionPercent: 10m, estimatedTotalCost: estimatedTotalCost);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();
        _projectId = project.Id;
    }

    private async Task<PurchaseInvoice> PostAsync(decimal amount, bool vatDeductible, bool withProject = true)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 5, 10), $"SUP-{Guid.NewGuid():N}"[..12], vatDeductible: vatDeductible);
        invoice.AddLine("حديد تسليح للموقع", 1m, amount);
        if (withProject)
            invoice.AssignProject(_projectId);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<decimal> DebitAsync(string accountCode) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == accountCode)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
