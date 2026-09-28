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
/// (جاهزيّةُ الإطلاق · ح-٨٤ · <c>treasury-payroll-assets#3</c>) <b>الرواتبُ تُصرف والتأميناتُ تُسدَّد ومستحقُّ نهاية الخدمة يُدفع —
/// وكان <c>2108</c> و<c>2109</c> يُدانان ولا يُمَسّان.</b>
///
/// <para>دورةُ أبريل: سعوديٌّ بـ٨٬٠٠٠ (تأميناتُه ٧٨٠ عليه و٩٤٠ على المنشأة) ووافدٌ بـ٦٬٠٠٠ (١٢٠ أخطاراً مهنيّة) — <b>الصافي ١٣٬٢٢٠
/// والتأميناتُ ١٬٨٤٠</b>. وكان كلاهما يبقى في الميزانيّة إلى الأبد.</para>
/// </summary>
public class SalariesAndGosiAreActuallyPaidTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _saudiId;

    public SalariesAndGosiAreActuallyPaidTests()
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
    /// <b>القياسُ الحاكم: صرفُ الدورة يُصفّر الرواتبَ المستحقّة</b> — ١٣٬٢٢٠ من البنك، و<c>2108</c> صفر.
    /// </summary>
    [Fact]
    public async Task PayingTheRun_ClearsSalariesPayable_FromTheBank()
    {
        await SetupAsync();
        var run = await PostedRunAsync();

        await new PayrollPaymentService(_db).PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), ReceiptMethod.Bank);

        Assert.Equal(0m, await BalanceAsync("2108"));
        Assert.Equal(-13_220m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    /// <summary><b>وسدادُ التأمينات يُصفّر <c>2109</c></b> — حصّتا الموظّف وصاحب العمل معاً، ١٬٨٤٠ نقداً.</summary>
    [Fact]
    public async Task PayingGosi_ClearsGosiPayable_BothShares()
    {
        await SetupAsync();
        var run = await PostedRunAsync();

        await new PayrollPaymentService(_db).PayGosiAsync(_companyId, run.Id, new DateOnly(2026, 5, 10), ReceiptMethod.Cash);

        Assert.Equal(0m, await BalanceAsync("2109"));
        Assert.Equal(-1_840m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary>
    /// <b>ولا تُصرف الدورةُ مرّتين</b> — والثانيةُ تُرفض باسمها؛ <b>وإلّا صار <c>2108</c> مديناً بـ١٣٬٢٢٠ وخرج من البنك ضعفُ الرواتب</b>.
    /// </summary>
    [Fact]
    public async Task ARunCannotBePaidTwice()
    {
        await SetupAsync();
        var run = await PostedRunAsync();
        var service = new PayrollPaymentService(_db);
        await service.PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), ReceiptMethod.Bank);

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 5, 1), ReceiptMethod.Bank));

        Assert.Equal(0m, await BalanceAsync("2108"));
        Assert.Equal(0m, await service.SalariesDueAsync(_companyId, run.Id));
    }

    /// <summary>
    /// <b>ودورةٌ صُرفت لا تُعكس</b> — فعكسُها يُعيد <c>2108</c> مديناً بما صُرف، <b>ويبقى المالُ خارجاً والالتزامُ سالباً</b>.
    /// </summary>
    [Fact]
    public async Task APaidRun_CannotBeReversed()
    {
        await SetupAsync();
        var run = await PostedRunAsync();
        await new PayrollPaymentService(_db).PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), ReceiptMethod.Bank);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new PayrollReversalService(_db).ReverseAsync(_companyId, run.Id, new DateOnly(2026, 5, 2), "admin"));

        Assert.Equal(PayrollRunStatus.Posted,
            (await _db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == run.Id)).Status);
        Assert.Equal(0m, await BalanceAsync("2108"));
    }

    /// <summary><b>ودورةٌ عُكست لا يُصرف لها شيء</b> — قيدُها قوبل بعكسه، ولا مستحقَّ قائم.</summary>
    [Fact]
    public async Task AReversedRun_HasNothingToPay()
    {
        await SetupAsync();
        var run = await PostedRunAsync();
        await new PayrollReversalService(_db).ReverseAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), "admin");

        var service = new PayrollPaymentService(_db);
        Assert.Equal(0m, await service.SalariesDueAsync(_companyId, run.Id));
        await Assert.ThrowsAsync<AccountingException>(() =>
            service.PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 5, 1), ReceiptMethod.Bank));
    }

    /// <summary>
    /// <b>ومستحقُّ نهاية الخدمة يُدفع ويُصفّر ما قيّدته التصفية</b> — وكانت التصفيةُ تُدينه في <c>2108</c> ولا يدفعه شيء.
    /// </summary>
    [Fact]
    public async Task TheEndOfServiceDue_IsPaid_AndClearsWhatTheSettlementOwed()
    {
        await SetupAsync();
        var employee = await _db.Employees.SingleAsync(e => e.Id == _saudiId);
        employee.Terminate(new DateOnly(2026, 4, 30));
        await _db.SaveChangesAsync();
        await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, _saudiId, EndOfServiceReason.EmployerTermination, new DateOnly(2026, 4, 30), "admin");

        var service = new PayrollPaymentService(_db);
        var due = await service.EndOfServiceDueAsync(_companyId, _saudiId);
        Assert.True(due > 0m, $"المستحقّ {due}");
        Assert.Equal(-due, await BalanceAsync("2108"));

        await service.PayEndOfServiceAsync(_companyId, _saudiId, new DateOnly(2026, 5, 5), ReceiptMethod.Bank);

        Assert.Equal(0m, await BalanceAsync("2108"));
        Assert.Equal(0m, await service.EndOfServiceDueAsync(_companyId, _saudiId));
    }

    /// <summary>
    /// <b>شاهدُ ضبط: صرفُ الرواتب لا يمسّ التأمينات</b> — فـ<c>2109</c> باقٍ ١٬٨٤٠ حتى يُسدَّد؛ <b>والصرفان مستقلّان كما في الواقع</b>
    /// (الرواتبُ آخرَ الشهر، والتأميناتُ بفاتورة المؤسّسة في الشهر التالي).
    /// </summary>
    [Fact]
    public async Task PayingSalaries_LeavesGosiOwed()
    {
        await SetupAsync();
        var run = await PostedRunAsync();

        await new PayrollPaymentService(_db).PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), ReceiptMethod.Bank);

        Assert.Equal(-1_840m, await BalanceAsync("2109"));
        Assert.Equal(1_840m, await new PayrollPaymentService(_db).GosiDueAsync(_companyId, run.Id));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<PayrollRun> PostedRunAsync()
    {
        var run = await new PayrollRunBuilder(_db).BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");
        return run;
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

        var saudi = Employee.Create(_companyId, "E-001", "سعوديّ", "1099999999",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        saudi.SetPay(basic: 8_000m, housing: 0m, transport: 0m);
        saudi.SetIban("SA0380000000608010167519");

        var expat = Employee.Create(_companyId, "E-002", "وافد", "2099999999",
            EmployeeNationality.NonSaudi, "فنّي", new DateOnly(2024, 1, 1));
        expat.SetPay(basic: 6_000m, housing: 0m, transport: 0m);
        expat.SetIban("SA0380000000608010167520");

        _db.Employees.AddRange(saudi, expat);
        await _db.SaveChangesAsync();

        _saudiId = saudi.Id;
    }
}
