using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٤٦ · <c>sector-modules#9</c>) <b>أجرُ موظّف المشروع تكلفةُ مشروعه.</b>
///
/// <para><b>ما كان:</b> الرواتبُ كلُّها مصروفُ رواتبٍ عامّ — فعاملُ الموقع لا يظهر في تكلفة مشروعه، ونسبةُ إنجاز
/// العقد (التكلفة إلى التكلفة) تُحسب بلا أكبر بنودها في المقاولات.</para>
/// </summary>
public class ProjectCostFromPayrollTests : IDisposable
{
    private const string ProjectCosts = "5105";

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _projectId;
    private long _siteWorkerId;

    public ProjectCostFromPayrollTests()
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

    /// <summary>
    /// <b>القياسُ الحاكم: أجرُ عامل الموقع وحصّةُ صاحب العمل عنه على المشروع، وأجرُ المحاسب مصروفُ رواتب —
    /// والقيدُ متوازنٌ والدائنُ لا يتغيّر.</b>
    /// </summary>
    [Fact]
    public async Task ASiteWorkersPay_IsAProjectCost_AndTheOfficeStaffStaysAnExpense()
    {
        await SetupAsync();

        var run = await PostedRunAsync();

        var worker = run.Lines.Single(l => l.EmployeeId == _siteWorkerId);
        var workerCost = worker.GrossPay + worker.GosiEmployer;
        var officeGross = run.Lines.Where(l => l.EmployeeId != _siteWorkerId).Sum(l => l.GrossPay);

        Assert.Equal(workerCost, await DebitAsync(ProjectCosts));
        // ‏الأساسيُّ على الرواتب والبدلاتُ على 5202 — لموظّف المكتب وحدَه.
        Assert.Equal(officeGross,
            await DebitAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense) + await DebitAsync("5202"));
        Assert.Equal(workerCost, (await _db.ProjectCostEntries.SingleAsync()).Amount);
        Assert.Equal(run.JournalEntryId, (await _db.ProjectCostEntries.SingleAsync()).JournalEntryId);

        var entry = await _db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == run.JournalEntryId);
        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
    }

    /// <summary><b>والعكسُ يُسقط تكلفةَ المشروع</b> كما يُسقط القيد.</summary>
    [Fact]
    public async Task ReversingTheRun_ReversesTheProjectCost()
    {
        await SetupAsync();
        var run = await PostedRunAsync();

        await new PayrollReversalService(_db).ReverseAsync(_companyId, run.Id, new DateOnly(2026, 5, 2), "admin");

        Assert.True((await _db.ProjectCostEntries.SingleAsync()).IsReversed);
        Assert.Equal(0m, await new ProjectCostService(_db).IncurredCostAsync(_companyId, _projectId));
        Assert.Equal(0m, await DebitAsync(ProjectCosts));
    }

    /// <summary>
    /// <b>وموظّفٌ على مشروعٍ مكتملٍ يوقف الترحيلَ باسمه</b> — لا يُحمَّل بصمتٍ على مصروف الرواتب.
    /// </summary>
    [Fact]
    public async Task AnEmployeeOnACompletedProject_StopsThePosting_ByName()
    {
        await SetupAsync();
        var project = await _db.ConstructionProjects.SingleAsync(p => p.Id == _projectId);
        await new ConstructionProjectService(_db).UpdateAsync(_companyId, _projectId, project.NameAr, project.CustomerId,
            project.StartDate, project.EndDate, project.ContractValue, project.RetentionPercent, project.Location,
            ConstructionProjectStatus.Completed, estimatedTotalCost: project.EstimatedTotalCost);

        var ex = await Assert.ThrowsAsync<AccountingException>(PostedRunAsync);

        Assert.Contains("عامل الموقع", ex.Message, StringComparison.Ordinal);
        Assert.False(await _db.ProjectCostEntries.AnyAsync());
    }

    [Fact]
    public void ARunWithoutProjects_IsUnchanged()
    {
        var run = PayrollRun.CreateDraft(1, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddLine("موظّف", 5_000m);

        Assert.Empty(run.ProjectCosts(new Dictionary<long, long>()));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مقاولات", "310123456700146", BusinessProfile.Construction);
        _companyId = company.Id;

        var project = ConstructionProject.Create(_companyId, "PRJ-146", "مشروع الموقع");
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();
        _projectId = project.Id;

        var office = Employee.Create(_companyId, "E-001", "محاسب المكتب", "1099999999",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        office.SetPay(basic: 8_000m, housing: 2_000m, transport: 0m);
        var worker = Employee.Create(_companyId, "E-002", "عامل الموقع", "1088888888",
            EmployeeNationality.Saudi, "فنّي", new DateOnly(2024, 1, 1));
        worker.SetPay(basic: 6_000m, housing: 1_500m, transport: 500m);
        worker.AssignProject(_projectId);
        _db.Employees.AddRange(office, worker);
        await _db.SaveChangesAsync();
        _siteWorkerId = worker.Id;
    }

    private async Task<PayrollRun> PostedRunAsync()
    {
        var run = await new PayrollRunBuilder(_db).BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");
        return run;
    }

    private async Task<decimal> DebitAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
