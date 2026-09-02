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
/// (CEEF-230 · ط2-٥) <b>سُلفةُ موظّف — مالٌ يخرج ويُنسى.</b>
///
/// <para><b>ما كان:</b> المنشأةُ تُسلّف موظّفَها خمسةَ آلاف، <b>ولا شيءَ في النظام
/// يعرفها</b>. فالمالُ يخرج ويُقيَّد <b>مصروفَ رواتب</b> — والأصلُ الذي سيُستردّ يختفي
/// من الميزانية، <b>ومصروفُ الرواتب يُضخَّم بما سيعود</b>.</para>
///
/// <para><b>ثمّ يُنسى الاستقطاع</b> — ولا موضعَ له أصلاً: سطرُ الرواتب لا يعرف إلا
/// التأمينات. <b>فيقبض الموظّفُ راتبَه كاملاً وقد أخذ سُلفتَه</b>، والمنشأةُ دفعت مرّتين.
/// وفي منشأةٍ بمئةِ موظّفٍ لا يُكتشف إلا حين يستقيل أحدُهم وقد سُلِّف ولم يُستقطَع منه.</para>
///
/// <para><b>وهويّةُ هـ-١٧:</b> <c>الرصيدُ = المصروفُ − المُستقطَع</c>، ولا يكون سالباً.
/// <b>ومجموعُ الأرصدة = رصيدُ حساب <c>1116</c></b>.</para>
/// </summary>
public class EmployeeAdvanceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _employeeId;
    private int _seq;

    public EmployeeAdvanceTests()
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

    // ── الصرف: أصلٌ لا مصروف ────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: الصرفُ يُنشئ أصلاً ولا يمسّ مصروفَ الرواتب.</b>
    ///
    /// <para><b>وقيدُه مصروفاً كان يُضخّم كلفةَ الرواتب بما سيُستردّ</b> — فيرى المالكُ
    /// شهراً بمصروفٍ أعلى بلا سببٍ تشغيليّ، <b>ويختفي المالُ الذي في ذمّة موظّفه من
    /// الميزانية</b>.</para>
    /// </summary>
    [Fact]
    public async Task Disbursing_BooksAnAssetNotAnExpense()
    {
        await SetupAsync();
        var advance = await ApprovedAsync(amount: 5_000m, installment: 1_000m);

        var salariesBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense);

        await new EmployeeAdvanceService(_db).DisburseAsync(
            _companyId, advance.Id, new DateOnly(2026, 3, 1),
            SaudiChartOfAccounts.SystemCodes.CashOnHand);

        Assert.Equal(5_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));
        Assert.Equal(salariesBefore, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense));
        Assert.Equal(-5_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary>
    /// <b>وسُلفةٌ اعتُمدت ولم تُصرف ليست ديناً.</b>
    ///
    /// <para>واستقطاعُها من الموظّف <b>يأخذ منه مالاً لم يأخذه</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnApprovedButUndisbursedAdvance_HasNoBalance()
    {
        await SetupAsync();
        await ApprovedAsync(amount: 5_000m, installment: 1_000m);

        Assert.Empty(await new EmployeeAdvanceService(_db).BalancesAsync(_companyId));
        Assert.Equal(0m, await new EmployeeAdvanceService(_db)
            .DueInstallmentAsync(_companyId, _employeeId));
    }

    // ── هـ-١٧: الرصيدُ = المصروفُ − المُستقطَع ────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: الاستقطاعُ يُنقص الصافي ويُعفي السُّلفة — لا يُنقص المصروف.</b>
    ///
    /// <para>المصروفُ وقع كاملاً: <b>الموظّفُ استحقّ أجرَه كلَّه</b>. والمُستقطَعُ سدادُ
    /// دَينٍ سابق. <b>وإنقاصُ المصروف يُظهر شهراً برواتبَ أقلَّ بلا سببٍ تشغيليّ</b>،
    /// ويُبقي الأصلَ في الميزانية بعد استرداده.</para>
    /// </summary>
    [Fact]
    public async Task Deducting_RelievesTheAdvanceAndReducesNetPay_NotExpense()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(amount: 5_000m, installment: 1_000m);

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddLine("موظّف المسبار", basic: 8_000m, employeeId: _employeeId, advanceDeduction: 1_000m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        // ‏المصروفُ كاملٌ ٨٠٠٠، والصافي ٧٠٠٠، والسُّلفةُ صارت ٤٠٠٠.
        Assert.Equal(8_000m, run.TotalGross);
        Assert.Equal(7_000m, run.TotalNet);
        Assert.Equal(8_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense));
        Assert.Equal(4_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));

        var balances = await new EmployeeAdvanceService(_db).BalancesAsync(_companyId);
        Assert.Equal(4_000m, balances.Single(b => b.AdvanceId == advance.Id).Outstanding);
    }

    /// <summary>
    /// <b>ومجموعُ أرصدة السُّلَف = رصيدُ حساب 1116 (هـ-١٧).</b>
    ///
    /// <para>وهي الهويّةُ نفسُها التي تحكم الذممَ: <b>دفترٌ مساعدٌ يخالف حسابَه العامّ
    /// يجعل أحدَ الرقمين كاذباً ولا يقول أيَّهما</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheSubsidiaryTotal_EqualsTheControlAccount()
    {
        await SetupAsync();
        await DisbursedAsync(amount: 5_000m, installment: 1_000m);
        await DisbursedAsync(amount: 3_000m, installment: 500m);

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddLine("موظّف المسبار", basic: 8_000m, employeeId: _employeeId, advanceDeduction: 1_500m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var balances = await new EmployeeAdvanceService(_db).BalancesAsync(_companyId);
        var subsidiary = balances.Sum(b => b.Outstanding);

        Assert.Equal(6_500m, subsidiary);
        Assert.Equal(subsidiary, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.EmployeeAdvances));
    }

    /// <summary>
    /// <b>والسدادُ يُطفئ الأقدمَ أوّلاً.</b>
    ///
    /// <para>الاستقطاعُ إجماليٌّ في سطر الرواتب، <b>وتوزيعُه بالتساوي يُبقي الأقدمَ مفتوحةً
    /// إلى الأبد</b> — والأقدمُ هي التي يُقاس بها التأخّر ويُبنى عليها قرارُ المنع من
    /// سُلفةٍ جديدة.</para>
    /// </summary>
    [Fact]
    public async Task Repayment_ClearsTheOldestAdvanceFirst()
    {
        await SetupAsync();
        var first = await DisbursedAsync(amount: 2_000m, installment: 2_000m,
            on: new DateOnly(2026, 1, 5));
        var second = await DisbursedAsync(amount: 3_000m, installment: 1_000m,
            on: new DateOnly(2026, 2, 5));

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddLine("موظّف المسبار", basic: 8_000m, employeeId: _employeeId, advanceDeduction: 2_000m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var balances = await new EmployeeAdvanceService(_db).BalancesAsync(_companyId);
        Assert.Equal(0m, balances.Single(b => b.AdvanceId == first.Id).Outstanding);
        Assert.Equal(3_000m, balances.Single(b => b.AdvanceId == second.Id).Outstanding);
    }

    /// <summary>
    /// <b>وآخرُ قسطٍ هو الباقي لا القسطَ الكامل.</b>
    ///
    /// <para>سُلفةٌ بـ<c>٢٥٠٠</c> وقسطٍ <c>١٠٠٠</c>: الشهرُ الثالث يستقطع <b>٥٠٠ لا ألفاً</b>.
    /// <b>وبغيره يصير الرصيدُ سالباً</b> — وتصبح المنشأةُ مدينةً لموظّفها بخمسمئةٍ لا
    /// أحدَ يعرف سببَها.</para>
    /// </summary>
    [Fact]
    public async Task TheFinalInstallment_IsTheRemainderNotTheFullInstallment()
    {
        await SetupAsync();
        await DisbursedAsync(amount: 2_500m, installment: 1_000m);

        await DeductAsync(month: 4, amount: 1_000m);
        await DeductAsync(month: 5, amount: 1_000m);

        var due = await new EmployeeAdvanceService(_db).DueInstallmentAsync(_companyId, _employeeId);
        Assert.Equal(500m, due);
    }

    // ── الحرّاس ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>واستقطاعٌ يبتلع الصافيَ يُرفض.</b>
    ///
    /// <para>وصافٍ سالبٌ يعني <b>أنّ الموظّف يدفع للمنشأة من جيبه</b>، ويُخرج ملفَّ حماية
    /// الأجور بمبلغٍ سالبٍ ترفضه المنظومة.</para>
    /// </summary>
    [Fact]
    public async Task ADeductionExceedingNetPay_IsRefusedByName()
    {
        await SetupAsync();

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));

        var error = Assert.Throws<AccountingException>(() =>
            run.AddLine("موظّف المسبار", basic: 3_000m, gosiEmployee: 300m,
                employeeId: _employeeId, advanceDeduction: 2_800m));

        Assert.Contains("يتجاوز صافيَ أجره", error.Message);
        Assert.Empty(run.Lines);   // ‏**ولا يبقى السطرُ الفاسدُ في الدورة**
    }

    /// <summary>
    /// <b>وقسطٌ أكبرُ من السُّلفة يُرفض عند الإنشاء.</b>
    ///
    /// <para>يُنتج استقطاعاً أكبرَ من الدَّين في أوّل شهر، <b>فيصير الرصيدُ سالباً</b>.</para>
    /// </summary>
    [Fact]
    public void AnInstallmentLargerThanTheAdvance_IsRefused()
    {
        var error = Assert.Throws<AccountingException>(() =>
            EmployeeAdvance.Create(1L, 1L, "ADV-X", new DateOnly(2026, 3, 1),
                amount: 1_000m, installmentAmount: 1_500m));

        Assert.Contains("أكبر من السُّلفة", error.Message);
    }

    /// <summary>
    /// <b>ولا تُصرف سُلفةٌ مرّتين.</b>
    ///
    /// <para>وصرفُها مرّتين يُخرج المالَ مرّتين <b>ويُقيّد ديناً واحداً</b> — فيبقى نصفُ
    /// المصروف بلا مقابل.</para>
    /// </summary>
    [Fact]
    public async Task DisbursingTwice_IsRefused()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(amount: 1_000m, installment: 500m);

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new EmployeeAdvanceService(_db).DisburseAsync(
                _companyId, advance.Id, new DateOnly(2026, 3, 5),
                SaudiChartOfAccounts.SystemCodes.CashOnHand));

        Assert.Contains("معتمدةٌ لم تُصرف", error.Message);
    }

    /// <summary>
    /// <b>ولا تُلغى سُلفةٌ صُرفت.</b>
    ///
    /// <para>وإلغاؤها يُسقط الدَّينَ من السجلّ <b>والمالُ خرج فعلاً</b> — فيبقى حسابُ
    /// <c>1116</c> مديناً بلا سُلفةٍ تُقابله.</para>
    /// </summary>
    [Fact]
    public async Task CancellingADisbursedAdvance_IsRefused()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(amount: 1_000m, installment: 500m);

        var error = Assert.Throws<AccountingException>(advance.Cancel);
        Assert.Contains("لا تُلغى سُلفةٌ صُرفت", error.Message);
    }

    // ── الاتّجاهُ المعاكس: عكسُ الدورة يُعيد الدَّين ─────────────────────────

    /// <summary>
    /// <b>ودورةٌ تُعكَس تُعيد الدَّين.</b>
    ///
    /// <para><b>وهذا ما يمنعه العدّادُ المخزَّن ويسمح به الاشتقاق</b>: الاستقطاعُ الذي
    /// أقفل السُّلفةَ ارتدّ، <b>وسُلفةٌ تبقى «مسدَّدة» بعد ارتداد سدادها دَينٌ سقط بلا
    /// قبض</b> — والمالُ عند الموظّف والسجلُّ يقول إنّه سدّد.</para>
    /// </summary>
    [Fact]
    public async Task ReversingThePayrollRun_RestoresTheDebt()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(amount: 1_000m, installment: 1_000m);

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddLine("موظّف المسبار", basic: 8_000m, employeeId: _employeeId, advanceDeduction: 1_000m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var service = new EmployeeAdvanceService(_db);
        await service.RefreshStatusesAsync(_companyId);
        Assert.Equal(AdvanceStatus.Settled,
            (await _db.EmployeeAdvances.SingleAsync(a => a.Id == advance.Id)).Status);

        await new PayrollReversalService(_db).ReverseAsync(
            _companyId, run.Id, new DateOnly(2026, 5, 1), "admin");

        await service.RefreshStatusesAsync(_companyId);

        var restored = await _db.EmployeeAdvances.SingleAsync(a => a.Id == advance.Id);
        Assert.Equal(AdvanceStatus.Outstanding, restored.Status);

        var balances = await service.BalancesAsync(_companyId);
        Assert.Equal(1_000m, balances.Single(b => b.AdvanceId == advance.Id).Outstanding);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<EmployeeAdvance> ApprovedAsync(decimal amount, decimal installment)
    {
        var advance = EmployeeAdvance.Create(_companyId, _employeeId, $"ADV-{++_seq:000}",
            new DateOnly(2026, 3, 1), amount, installment);
        _db.EmployeeAdvances.Add(advance);
        await _db.SaveChangesAsync();
        return advance;
    }

    private async Task<EmployeeAdvance> DisbursedAsync(decimal amount, decimal installment,
        DateOnly? on = null)
    {
        var advance = await ApprovedAsync(amount, installment);
        await new EmployeeAdvanceService(_db).DisburseAsync(
            _companyId, advance.Id, on ?? new DateOnly(2026, 3, 1),
            SaudiChartOfAccounts.SystemCodes.CashOnHand);
        return advance;
    }

    private async Task DeductAsync(int month, decimal amount)
    {
        var run = PayrollRun.CreateDraft(_companyId, 2026, month, new DateOnly(2026, month, 28));
        run.AddLine("موظّف المسبار", basic: 8_000m, employeeId: _employeeId,
            advanceDeduction: amount);
        await new PayrollPostingService(_db).PostAsync(run, "admin");
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;

        var employee = Employee.Create(_companyId, "E-001", "موظّف المسبار", "1099999999",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        employee.SetPay(basic: 8_000m, housing: 0m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        _employeeId = employee.Id;
    }
}
