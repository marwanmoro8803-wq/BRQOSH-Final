using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٧ · <c>treasury-payroll-assets#4</c>) <b>السُّلفةُ القائمةُ تُستردّ من المخالصة النهائيّة — وكانت تبقى ديناً على
/// موظّفٍ رحل وتُدفع له مستحقّاتُه كاملة.</b>
///
/// <para>سعوديٌّ بـ٨٬٠٠٠ من ٢٠٢٤ تنتهي خدمتُه آخرَ أبريل ٢٠٢٦، وعليه سُلفةٌ لم يُستقطع منها شيء. <b>وكان رصيدُ السُّلفة يُشتقّ من سطور
/// الرواتب وحدَها</b> — فلا مخالصةَ تمسّه، ولا يُغلق أبداً.</para>
/// </summary>
public class ALeaversAdvanceIsRecoveredFromTheFinalSettlementTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _employeeId;

    public ALeaversAdvanceIsRecoveredFromTheFinalSettlementTests()
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
    /// <b>القياسُ الحاكم: سُلفةُ ٥٬٠٠٠ تُستردّ كلُّها</b> — فيُغلق رصيدُها وتصير «مسدَّدة»، و<c>1116</c> صفر، والمستحقُّ للموظّف ينقص بها.
    /// </summary>
    [Fact]
    public async Task AnAdvanceSmallerThanTheDues_IsRecoveredInFull()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(5_000m);

        var result = await SettleAsync();

        Assert.Equal(5_000m, result.AdvanceRecovered);
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));
        var balance = (await new EmployeeAdvanceService(_db).BalancesAsync(_companyId, _employeeId)).Single();
        Assert.Equal(0m, balance.Outstanding);
        Assert.Equal(AdvanceStatus.Settled, (await _db.EmployeeAdvances.AsNoTracking().SingleAsync(a => a.Id == advance.Id)).Status);
        Assert.Equal(result.Benefit + result.LeavePay - 5_000m,
            await new PayrollPaymentService(_db).EndOfServiceDueAsync(_companyId, _employeeId));
    }

    /// <summary>
    /// <b>وسُلفةٌ أكبرُ من المستحقّات تُستردّ بقدرها</b> — فلا يصير المستحقُّ للموظّف سالباً، ويبقى الباقي ديناً قائماً في <c>1116</c>.
    /// </summary>
    [Fact]
    public async Task AnAdvanceLargerThanTheDues_IsRecoveredUpToTheDues_AndTheRestStaysOwed()
    {
        await SetupAsync();
        await DisbursedAsync(100_000m);

        var result = await SettleAsync();

        var dues = result.Benefit + result.LeavePay;
        Assert.True(dues > 0m && dues < 100_000m, $"المستحقّات {dues}");
        Assert.Equal(dues, result.AdvanceRecovered);
        Assert.Equal(0m, await new PayrollPaymentService(_db).EndOfServiceDueAsync(_companyId, _employeeId));
        Assert.Equal(100_000m - dues, (await new EmployeeAdvanceService(_db).BalancesAsync(_companyId, _employeeId)).Single().Outstanding);
    }

    /// <summary><b>شاهدُ ضبط: بلا سُلفةٍ لا استرداد</b> — والمستحقُّ كلُّه في <c>2108</c> كما كان.</summary>
    [Fact]
    public async Task WithoutAnAdvance_TheSettlementIsUnchanged()
    {
        await SetupAsync();

        var result = await SettleAsync();

        Assert.Equal(0m, result.AdvanceRecovered);
        Assert.Equal(-(result.Benefit + result.LeavePay), await BalanceAsync("2108"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<EndOfServiceSettlementResult> SettleAsync()
    {
        var employee = await _db.Employees.SingleAsync(e => e.Id == _employeeId);
        employee.Terminate(new DateOnly(2026, 4, 30));
        await _db.SaveChangesAsync();
        return await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, _employeeId, EndOfServiceReason.EmployerTermination, new DateOnly(2026, 4, 30), "admin");
    }

    private async Task<EmployeeAdvance> DisbursedAsync(decimal amount)
    {
        var advance = EmployeeAdvance.Create(_companyId, _employeeId, "ADV-001", new DateOnly(2026, 3, 1), amount, 1_000m);
        _db.EmployeeAdvances.Add(advance);
        await _db.SaveChangesAsync();
        await new EmployeeAdvanceService(_db).DisburseAsync(
            _companyId, advance.Id, new DateOnly(2026, 3, 1), SaudiChartOfAccounts.SystemCodes.CashOnHand);
        return advance;
    }

    private async Task<decimal> BalanceAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;

        var employee = Employee.Create(_companyId, "E-001", "سعوديّ", "1099999999",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        employee.SetPay(basic: 8_000m, housing: 0m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        _employeeId = employee.Id;
    }
}
