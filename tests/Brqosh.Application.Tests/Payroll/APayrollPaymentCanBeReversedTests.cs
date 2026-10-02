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
/// (جاهزيّةُ الإطلاق · ح-٨٨ · <c>treasury-payroll-assets#10</c>) <b>صرفُ الرواتب والتأمينات ومستحقّ نهاية الخدمة يُعكس — وكانت دورةٌ صُرفت
/// لا تُعكس، وصرفُها لا عكسَ له.</b>
///
/// <para>ح-٨٤ يرفض عكسَ دورةٍ صُرفت (فعكسُها يُعيد <c>2108</c> مديناً والمالُ خارج) — وهو صواب، <b>لكنّ الصرفَ نفسَه لم يكن له عكس</b>،
/// فدورةٌ صُرفت خطأً لا تُصحَّح إلّا بقيدٍ يدويٍّ يرفضه الأستاذُ لأنّ لقيد الصرف مستنداً.</para>
/// </summary>
public class APayrollPaymentCanBeReversedTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _saudiId;

    public APayrollPaymentCanBeReversedTests()
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
    /// <b>القياسُ الحاكم: عكسُ الصرف يُعيد المستحقَّ قائماً، ثمّ تُعكس الدورة</b> — <c>2108</c> يعود ١٣٬٢٢٠ دائناً والبنكُ صفراً، وعكسُ
    /// الدورة الذي كان يُرفض يمرّ فيصفو <c>2108</c>.
    /// </summary>
    [Fact]
    public async Task ReversingTheSalariesPayment_RestoresTheLiability_AndUnblocksTheRunReversal()
    {
        await SetupAsync();
        var run = await PostedRunAsync();
        var payments = new PayrollPaymentService(_db);
        await payments.PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), ReceiptMethod.Bank);

        await payments.ReverseSalariesPaymentAsync(_companyId, run.Id, new DateOnly(2026, 5, 2), "admin");

        Assert.Equal(-13_220m, await BalanceAsync("2108"));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
        Assert.Equal(13_220m, await payments.SalariesDueAsync(_companyId, run.Id));

        await new PayrollReversalService(_db).ReverseAsync(_companyId, run.Id, new DateOnly(2026, 5, 2), "admin");
        Assert.Equal(0m, await BalanceAsync("2108"));
    }

    /// <summary><b>وعكسُ سداد التأمينات</b> يُعيد <c>2109</c> ١٬٨٤٠ دائناً والصندوقَ صفراً.</summary>
    [Fact]
    public async Task ReversingTheGosiPayment_RestoresGosiPayable()
    {
        await SetupAsync();
        var run = await PostedRunAsync();
        var payments = new PayrollPaymentService(_db);
        await payments.PayGosiAsync(_companyId, run.Id, new DateOnly(2026, 5, 10), ReceiptMethod.Cash);

        await payments.ReverseGosiPaymentAsync(_companyId, run.Id, new DateOnly(2026, 5, 11), "admin");

        Assert.Equal(-1_840m, await BalanceAsync("2109"));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary><b>ولا صرفَ قائماً ⟵ رفضٌ باسمه</b> — لا نجاحٌ صامتٌ يوهم أنّ شيئاً عُكس.</summary>
    [Fact]
    public async Task ReversingWhenNothingWasPaid_IsRefused()
    {
        await SetupAsync();
        var run = await PostedRunAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new PayrollPaymentService(_db).ReverseSalariesPaymentAsync(_companyId, run.Id, new DateOnly(2026, 5, 2), "admin"));
    }

    /// <summary>
    /// <b>وبعد العكس يُصرف من جديد، ويُعكس الجديدُ وحدَه</b> — فالمعكوسُ وعكسُه لا يُعكسان ثانيةً (القائمُ بمالكه).
    /// </summary>
    [Fact]
    public async Task AfterAReversal_TheRunCanBePaidAgain_AndOnlyTheNewPaymentIsReversed()
    {
        await SetupAsync();
        var run = await PostedRunAsync();
        var payments = new PayrollPaymentService(_db);
        await payments.PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 4, 30), ReceiptMethod.Bank);
        await payments.ReverseSalariesPaymentAsync(_companyId, run.Id, new DateOnly(2026, 5, 1), "admin");

        await payments.PaySalariesAsync(_companyId, run.Id, new DateOnly(2026, 5, 2), ReceiptMethod.Bank);
        Assert.Equal(0m, await BalanceAsync("2108"));

        await payments.ReverseSalariesPaymentAsync(_companyId, run.Id, new DateOnly(2026, 5, 3), "admin");
        Assert.Equal(-13_220m, await BalanceAsync("2108"));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    /// <summary><b>وعكسُ دفع مستحقّ نهاية الخدمة</b> يُعيده قائماً.</summary>
    [Fact]
    public async Task ReversingTheEndOfServicePayment_RestoresTheDue()
    {
        await SetupAsync();
        var employee = await _db.Employees.SingleAsync(e => e.Id == _saudiId);
        employee.Terminate(new DateOnly(2026, 4, 30));
        await _db.SaveChangesAsync();
        await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, _saudiId, EndOfServiceReason.EmployerTermination, new DateOnly(2026, 4, 30), "admin");
        var payments = new PayrollPaymentService(_db);
        var due = await payments.EndOfServiceDueAsync(_companyId, _saudiId);
        await payments.PayEndOfServiceAsync(_companyId, _saudiId, new DateOnly(2026, 5, 5), ReceiptMethod.Bank);

        await payments.ReverseEndOfServicePaymentAsync(_companyId, _saudiId, new DateOnly(2026, 5, 6), "admin");

        Assert.Equal(due, await payments.EndOfServiceDueAsync(_companyId, _saudiId));
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
        var expat = Employee.Create(_companyId, "E-002", "وافد", "2099999999",
            EmployeeNationality.NonSaudi, "فنّي", new DateOnly(2024, 1, 1));
        expat.SetPay(basic: 6_000m, housing: 0m, transport: 0m);
        _db.Employees.AddRange(saudi, expat);
        await _db.SaveChangesAsync();
        _saudiId = saudi.Id;
    }
}
