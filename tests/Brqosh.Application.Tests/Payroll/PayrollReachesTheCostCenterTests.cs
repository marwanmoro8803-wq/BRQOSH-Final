using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (CEEF-232 · م-١) <b>الأجرُ لا يبلغ مركزَ التكلفة — وهو عادةً أكبرُ بنوده.</b>
///
/// <para><b>ما كان:</b> <c>Employee.CostCenterId</c> عمودٌ في المخطَّط،
/// <b>و<c>AssignCostCenter</c> بلا مستدعٍ واحدٍ في المستودع كلِّه</b> — لا شاشةَ تكتبه.
/// وقيدُ الرواتب سطرٌ مُجمَّعٌ بلا بُعد: <c>PayrollPostingService</c> لا يستدعي
/// <c>StampCostCenter</c>، ولا تحمل سطورُه <c>CostCenterId</c>.</para>
///
/// <para><b>والميزانُ سليمٌ تماماً</b> — ولذلك لا يشكو أحد. <b>وكلُّ قائمةِ مركزِ تكلفةٍ
/// تُسقط أكبرَ بنودها</b>: <c>CostCenterStatementQuery</c> يُرشّح على <c>CostCenterId</c>
/// في السطر، <b>فمصنعٌ يوزّع مصروفاته على خطوطِ إنتاجه يرى موادَّه وكهرباءَه ولا يرى
/// أجورَه</b> — ويقيس ربحيّةَ خطٍّ بنصف تكلفته.</para>
///
/// <para><b>وهويّةُ م-١:</b> مجموعُ مصروفِ الأجر في قائمة المراكز = مصروفُ الأجر في
/// قائمة الدخل — <b>موزَّعاً لا منقوصاً</b>.</para>
/// </summary>
public class PayrollReachesTheCostCenterTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private static readonly DateOnly PayDate = new(2026, 4, 30);

    public PayrollReachesTheCostCenterTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: أجرُ كلِّ موظّفٍ يظهر في مركزه.</b>
    ///
    /// <para>موظّفان بعشرة آلافٍ لكلٍّ في مركزين مختلفين ⟵ <b>كلُّ مركزٍ يحمل عشرةَ
    /// آلاف</b>. <b>وكانت القائمةُ تُخرج صفراً للمركزين معاً</b>.</para>
    /// </summary>
    [Fact]
    public async Task EachEmployeesWage_LandsOnHisOwnCostCenter()
    {
        await SetupAsync();
        var north = await CenterAsync("CC-N", "خط الشمال");
        var south = await CenterAsync("CC-S", "خط الجنوب");

        await EmployeeAsync("E-1", "1011111111", 10_000m, north.Id);
        await EmployeeAsync("E-2", "1022222222", 10_000m, south.Id);

        await RunPayrollAsync();

        Assert.Equal(10_000m, await CenterWagesAsync(north.Id));
        Assert.Equal(10_000m, await CenterWagesAsync(south.Id));
    }

    /// <summary>
    /// <b>ومَن بلا مركزٍ يبقى بلا بُعد — ولا يُنسب بالتخمين.</b>
    ///
    /// <para><b>وإسنادُه إلى أوّل مركزٍ أو إلى «مركزٍ افتراضيّ» أسوأُ من تركه</b>: يُنتج
    /// رقماً يبدو مصنَّفاً وليس كذلك، <b>فيُبنى عليه قرارٌ</b>. والفراغُ يُرى فيُملأ.</para>
    /// </summary>
    [Fact]
    public async Task AnEmployeeWithoutACenter_StaysUndimensioned()
    {
        await SetupAsync();
        var north = await CenterAsync("CC-N", "خط الشمال");

        await EmployeeAsync("E-1", "1011111111", 10_000m, north.Id);
        await EmployeeAsync("E-2", "1022222222", 4_000m, costCenterId: null);

        await RunPayrollAsync();

        Assert.Equal(10_000m, await CenterWagesAsync(north.Id));
        Assert.Equal(4_000m, await CenterWagesAsync(null));
    }

    /// <summary>
    /// <b>والمجموعُ لا يتغيّر بريال — فالتفكيكُ توزيعٌ لا إضافة.</b>
    ///
    /// <para><b>وهذا ما يجعل التغييرَ آمناً على القوائم المنشورة:</b> قائمةُ الدخل
    /// وصافي الربح <b>كما كانا حرفاً</b>، والقيدُ متوازن. <b>والخطرُ الوحيدُ كان فرقَ
    /// التقريب</b> بين مجموع الأوعية والمجموع — <b>وهللةٌ واحدةٌ تُسقط القيدَ كلَّه</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheTotalWageExpense_IsUnchangedByTheSplit()
    {
        await SetupAsync();
        var a = await CenterAsync("CC-A", "أ");
        var b = await CenterAsync("CC-B", "ب");

        // ‏مبالغُ تُنتج كسورَ هللةٍ عند القسمة — **وهي مقصودة**.
        await EmployeeAsync("E-1", "1011111111", 3_333.33m, a.Id);
        await EmployeeAsync("E-2", "1022222222", 3_333.34m, b.Id);
        await EmployeeAsync("E-3", "1033333333", 3_333.33m, null);

        var entry = await RunPayrollAsync();

        Assert.True(entry.IsBalanced, "انكسر توازنُ قيد الرواتب بعد التفكيك على المراكز.");

        var wages = await AccountTotalAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense);
        var byCenter = await CenterWagesAsync(a.Id) + await CenterWagesAsync(b.Id)
                       + await CenterWagesAsync(null);

        Assert.Equal(10_000m, Math.Round(wages, 2));
        Assert.Equal(wages, byCenter);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<CostCenter> CenterAsync(string code, string name)
    {
        var center = CostCenter.Create(_companyId, code, name);
        _db.CostCenters.Add(center);
        await _db.SaveChangesAsync();
        return center;
    }

    private async Task<Employee> EmployeeAsync(string code, string nationalId, decimal wage, long? costCenterId)
    {
        var employee = Employee.Create(_companyId, code, $"موظّف {code}", nationalId,
            EmployeeNationality.Saudi, "فنّيّ", new DateOnly(2024, 1, 1));
        employee.SetPay(basic: wage, housing: 0m, transport: 0m);
        employee.AssignCostCenter(costCenterId);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    private async Task<JournalEntry> RunPayrollAsync()
    {
        var run = PayrollRun.CreateDraft(_companyId, PayDate.Year, PayDate.Month, PayDate);
        var policy = GosiPolicy.CreateDefault(_companyId);
        foreach (var employee in await _db.Employees.Where(e => e.CompanyId == _companyId).ToListAsync())
            run.AddEmployee(employee, policy);

        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync();

        var posted = await new PayrollPostingService(_db).PostAsync(run, "admin");
        return await _db.JournalEntries
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == posted.JournalEntryId);
    }

    /// <summary>مصروفُ الأجر المنسوبُ إلى مركزٍ — <c>null</c> للمركزِ المعدوم.</summary>
    private async Task<decimal> CenterWagesAsync(long? costCenterId)
    {
        var rows = await new CostCenterStatementQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        return rows
            .Where(r => r.CostCenterId == costCenterId
                        && r.AccountCode == SaudiChartOfAccounts.SystemCodes.SalariesExpense)
            .Sum(r => r.Debit - r.Credit);
    }

    private async Task<decimal> AccountTotalAsync(string code)
    {
        var accountId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == code)
            .Select(a => a.Id).SingleAsync();

        return await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == accountId)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0m;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المراكز", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
