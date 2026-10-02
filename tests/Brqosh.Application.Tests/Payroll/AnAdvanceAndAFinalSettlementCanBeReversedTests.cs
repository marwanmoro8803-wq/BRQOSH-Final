using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩١ · <c>treasury-payroll-assets#10</c>) <b>صرفُ السُّلفة وتصفيةُ نهاية الخدمة يُعكسان — كلٌّ بحارس حالته.</b>
///
/// <para>كانا لا يُعكسان أبداً: العكسُ العامُّ يرفض قيداً له مستند، <b>و<c>EmployeeAdvance.Cancel()</c> يقول «سدِّدْها أو اعكسْ صرفَها» ولا عكسَ
/// للصرف في الشجرة كلِّها</b>. والتصفيةُ تُقرأ «تمّت» من <c>Posted || Reversed</c> — فلو عُكست لبقي الموظّفُ «مُصفًّى» ولا يُصفّى ثانيةً.</para>
/// </summary>
public class AnAdvanceAndAFinalSettlementCanBeReversedTests : IDisposable
{
    private static readonly DateOnly Leaving = new(2026, 4, 30);
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _employeeId;

    public AnAdvanceAndAFinalSettlementCanBeReversedTests()
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
    /// <b>القياسُ الحاكم للسُّلفة: صرفٌ لم يُستقطع منه يُعكس</b> — فيعود النقدُ و<c>1116</c> صفراً، وتعود السُّلفةُ «معتمدةً» فتُلغى أو تُصرف من جديد.
    /// </summary>
    [Fact]
    public async Task AnUntouchedDisbursement_IsReversed_AndTheAdvanceCanThenBeCancelled()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(5_000m);

        await new EmployeeAdvanceService(_db).ReverseDisbursementAsync(_companyId, advance.Id, new DateOnly(2026, 3, 2), "admin");

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        var reloaded = await _db.EmployeeAdvances.SingleAsync(a => a.Id == advance.Id);
        Assert.Equal(AdvanceStatus.Approved, reloaded.Status);
        reloaded.Cancel();
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// <b>وسُلفةٌ استُقطع منها لا يُعكس صرفُها</b> — فعكسُه يُطفئ ديناً سُدِّد بعضُه، فيصير المستقطَعُ من الراتب مالاً بلا سبب.
    /// </summary>
    [Fact]
    public async Task AnAdvanceAlreadyDeductedFrom_IsNotReversed()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(5_000m);
        var run = await new PayrollRunBuilder(_db).BuildAsync(_companyId, 2026, 3, new DateOnly(2026, 3, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");
        Assert.Equal(4_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));

        await Assert.ThrowsAsync<AccountingException>(() =>
            new EmployeeAdvanceService(_db).ReverseDisbursementAsync(_companyId, advance.Id, new DateOnly(2026, 4, 1), "admin"));
        Assert.Equal(4_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));
    }

    /// <summary>
    /// <b>القياسُ الحاكم للتصفية: عكسُها يُعيد الحساباتِ إلى ما قبلها، ويُصفّى الموظّفُ من جديد</b> — وكانت «تمّت» تُقرأ من المعكوس أيضاً.
    /// </summary>
    [Fact]
    public async Task ReversingTheFinalSettlement_RestoresTheBooks_AndAllowsSettlingAgain()
    {
        await SetupAsync();
        await TerminateAsync();
        var before = await BalancesAsync();
        var first = await SettleAsync();

        await new EndOfServiceSettlementService(_db).ReverseAsync(_companyId, _employeeId, new DateOnly(2026, 5, 2), "admin");

        Assert.Equal(before, await BalancesAsync());
        var second = await SettleAsync();
        Assert.Equal(first.Benefit, second.Benefit);
    }

    /// <summary>
    /// <b>وعكسُ تصفيةٍ استردّت سُلفةً يُعيدها قائمة</b> — فالاستردادُ كان سدادَها، وارتدادُه يُعيد الدَّين.
    /// </summary>
    [Fact]
    public async Task ReversingASettlementThatRecoveredAnAdvance_ReopensTheAdvance()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(5_000m);
        await TerminateAsync();
        await SettleAsync();
        Assert.Equal(AdvanceStatus.Settled, (await _db.EmployeeAdvances.AsNoTracking().SingleAsync(a => a.Id == advance.Id)).Status);

        await new EndOfServiceSettlementService(_db).ReverseAsync(_companyId, _employeeId, new DateOnly(2026, 5, 2), "admin");

        Assert.Equal(AdvanceStatus.Outstanding, (await _db.EmployeeAdvances.AsNoTracking().SingleAsync(a => a.Id == advance.Id)).Status);
        Assert.Equal(5_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));
    }

    /// <summary>
    /// <b>وتصفيةٌ دُفع مستحقُّها لا تُعكس حتى يُعكس الدفع</b> — وإلّا خرج المالُ ولا التزامَ يقابله.
    /// </summary>
    [Fact]
    public async Task APaidSettlement_IsRefused_UntilThePaymentIsReversed()
    {
        await SetupAsync();
        await TerminateAsync();
        await SettleAsync();
        var payments = new PayrollPaymentService(_db);
        await payments.PayEndOfServiceAsync(_companyId, _employeeId, new DateOnly(2026, 5, 5), ReceiptMethod.Bank);
        var settlements = new EndOfServiceSettlementService(_db);

        await Assert.ThrowsAsync<AccountingException>(() =>
            settlements.ReverseAsync(_companyId, _employeeId, new DateOnly(2026, 5, 6), "admin"));

        await payments.ReverseEndOfServicePaymentAsync(_companyId, _employeeId, new DateOnly(2026, 5, 6), "admin");
        await settlements.ReverseAsync(_companyId, _employeeId, new DateOnly(2026, 5, 6), "admin");
        Assert.Equal(0m, await BalanceAsync("2108"));
    }

    /// <summary><b>ولا تصفيةَ قائمة ⟵ رفضٌ باسمه</b> — لا نجاحٌ صامت.</summary>
    [Fact]
    public async Task NoStandingSettlement_IsRefused()
    {
        await SetupAsync();
        await TerminateAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new EndOfServiceSettlementService(_db).ReverseAsync(_companyId, _employeeId, new DateOnly(2026, 5, 2), "admin"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task TerminateAsync()
    {
        var employee = await _db.Employees.SingleAsync(e => e.Id == _employeeId);
        employee.Terminate(Leaving);
        await _db.SaveChangesAsync();
    }

    private Task<EndOfServiceSettlementResult> SettleAsync() =>
        new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, _employeeId, EndOfServiceReason.EmployerTermination, Leaving, "admin");

    private async Task<EmployeeAdvance> DisbursedAsync(decimal amount)
    {
        var advance = EmployeeAdvance.Create(_companyId, _employeeId, "ADV-001", new DateOnly(2026, 3, 1), amount, 1_000m);
        _db.EmployeeAdvances.Add(advance);
        await _db.SaveChangesAsync();
        await new EmployeeAdvanceService(_db).DisburseAsync(
            _companyId, advance.Id, new DateOnly(2026, 3, 1), SaudiChartOfAccounts.SystemCodes.CashOnHand);
        return advance;
    }

    private async Task<string> BalancesAsync()
    {
        var codes = new[] { "2108", "2202", "2203", "5219", "5220", SaudiChartOfAccounts.SystemCodes.EmployeeAdvances };
        var parts = new List<string>();
        foreach (var code in codes)
            parts.Add(code + "=" + (await BalanceAsync(code)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(" ", parts);
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
