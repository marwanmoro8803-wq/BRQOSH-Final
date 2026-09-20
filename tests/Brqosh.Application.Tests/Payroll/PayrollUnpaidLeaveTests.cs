using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢) <b>الرواتبُ لا تُرحَّل في شهرٍ فيه إجازةٌ بلا أجر.</b>
///
/// <para><b>ما كان:</b> سطرا المصروف يأخذان <c>BasicSalary</c> والبدلاتِ خامّةً — أي
/// <b>الأجرَ الكامل</b> — بينما الطرفُ الدائنُ يأخذ <c>TotalNet</c> المبنيَّ على
/// <c>GrossPay</c> <b>بعد</b> خصم الغياب. فالقيدُ ينقص بمقدار الغياب بالضبط ويرتدّ على
/// حارس التوازن.</para>
///
/// <para><b>والنيّةُ كانت مكتوبةً في الشيفرة نفسِها</b> (<c>AbsenceDeduction</c>):
/// «الغيابُ بلا أجرٍ أجرٌ لم يُستحقّ، فالمصروفُ نفسُه ينقص». <b>وثيقةٌ وشيفرةٌ افترقتا</b> —
/// والوثيقةُ كانت على حقّ.</para>
///
/// <para><b>والأثرُ تشغيليٌّ لا تجميليّ:</b> إجازةٌ واحدةٌ بلا أجرٍ لموظّفٍ واحد
/// <b>تُعطّل رواتبَ المنشأة كلِّها</b> ذلك الشهر.</para>
/// </summary>
public class PayrollUnpaidLeaveTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private static readonly DateOnly PayDate = new(2026, 4, 30);

    public PayrollUnpaidLeaveTests()
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

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary><b>القياسُ الحاكم:</b> الدورةُ تُرحَّل، والقيدُ متوازن.</summary>
    [Fact]
    public async Task PayrollWithUnpaidLeave_PostsAndTheEntryBalances()
    {
        await SetupAsync();
        await EmployeeAsync("E-1", "1011111111", basic: 10_000m, housing: 0m);

        var entry = await RunPayrollAsync(absence: 1_000m);

        Assert.True(entry.IsBalanced,
            $"قيدُ الرواتب غيرُ متوازن: مدين {entry.TotalDebit:N2} / دائن {entry.TotalCredit:N2}");
    }

    /// <summary>
    /// <b>والخصمُ يُنقص المصروف لا الصافيَ وحدَه.</b>
    ///
    /// <para>أجرٌ كاملُه ١٠٬٠٠٠ وغيابٌ ١٬٠٠٠ ⟵ مصروفُ الرواتب <b>٩٬٠٠٠ بالضبط</b>.
    /// ولو نقص الصافي وحدَه لبقي المصروفُ ١٠٬٠٠٠ — <b>أجرُ أيّامٍ لم تُعمَل</b> —
    /// ولأُنشئ التزامٌ لا صاحبَ له.</para>
    /// </summary>
    [Fact]
    public async Task UnpaidLeave_ReducesTheWageExpense_ByExactlyTheAbsence()
    {
        await SetupAsync();
        await EmployeeAsync("E-1", "1011111111", basic: 10_000m, housing: 0m);

        var entry = await RunPayrollAsync(absence: 1_000m);

        Assert.Equal(9_000m, WageExpense(entry));
    }

    /// <summary>
    /// <b>شاهدُ ضبط:</b> شهرٌ بلا غيابٍ يبقى كما كان حرفاً — المصروفُ هو الأجرُ الكامل.
    /// ولولاه لكان «الإصلاح» قد يُنقص كلَّ راتبٍ في النظام.
    /// </summary>
    [Fact]
    public async Task WithoutAbsence_TheWageExpense_IsTheFullWage()
    {
        await SetupAsync();
        await EmployeeAsync("E-1", "1011111111", basic: 10_000m, housing: 0m);

        var entry = await RunPayrollAsync(absence: 0m);

        Assert.True(entry.IsBalanced);
        Assert.Equal(10_000m, WageExpense(entry));
    }

    /// <summary>
    /// <b>ومع البدلات يُقسَم الخصمُ تناسُبيّاً</b> — لأنّ الخصمَ يُحسب على الأجر الكامل:
    /// أساسيٌّ ٧٬٠٠٠ وسكنٌ ٣٬٠٠٠ وغيابٌ ١٬٠٠٠ ⟵ <c>5201</c> يحمل ٦٬٣٠٠ و<c>5202</c> يحمل ٢٬٧٠٠.
    /// وحملُ الخصم كلِّه على الأساسيّ كان يُظهر بدلَ سكنٍ دُفع عن أيّامٍ لم تُعمَل.
    /// </summary>
    [Fact]
    public async Task WithAllowances_TheAbsence_IsSplitProportionally()
    {
        await SetupAsync();
        await EmployeeAsync("E-1", "1011111111", basic: 7_000m, housing: 3_000m);

        var entry = await RunPayrollAsync(absence: 1_000m);

        Assert.True(entry.IsBalanced);
        Assert.Equal(6_300m, AccountDebit(entry, SaudiChartOfAccounts.SystemCodes.SalariesExpense));
        Assert.Equal(2_700m, AccountDebit(entry, "5202"));
    }

    /// <summary>
    /// <b>والقسمةُ اجتهادٌ، أمّا المجموعُ فهويّة.</b> مبالغُ تُنتج كسورَ هللةٍ عند القسمة —
    /// ومهما فعل التقريبُ بالحصّتَين يبقى مجموعُهما <c>GrossPay</c> بالضبط. <b>وهللةٌ واحدةٌ
    /// تُسقط القيدَ كلَّه</b>، فهذه هي الهويّةُ التي يقوم عليها التوازن.
    /// </summary>
    [Theory]
    [InlineData(3_333.33, 6_666.67, 333.33)]
    [InlineData(7_777.77, 2_222.23, 1_111.11)]
    [InlineData(0.01, 9_999.99, 5_000.00)]
    [InlineData(10_000.00, 0.00, 10_000.00)]
    public void TheSplit_AlwaysSumsToGrossPay(decimal basic, decimal housing, decimal absence)
    {
        var run = PayrollRun.CreateDraft(1, PayDate.Year, PayDate.Month, PayDate);
        run.AddLine("موظّف", basic, housing, transport: 0m, absenceDeduction: absence);

        var line = run.Lines[0];

        Assert.Equal(line.GrossPay, line.BasicAfterAbsence + line.AllowancesAfterAbsence);
        Assert.True(line.BasicAfterAbsence >= 0m, "الأساسيُّ بعد الغياب سالب");
        Assert.True(line.AllowancesAfterAbsence >= 0m, "البدلاتُ بعد الغياب سالبة");
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "310123456700099", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task<Employee> EmployeeAsync(string code, string nationalId, decimal basic, decimal housing)
    {
        var employee = Employee.Create(_companyId, code, $"موظّف {code}", nationalId,
            EmployeeNationality.Saudi, "فنّيّ", new DateOnly(2024, 1, 1));
        employee.SetPay(basic: basic, housing: housing, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    private async Task<JournalEntry> RunPayrollAsync(decimal absence)
    {
        var run = PayrollRun.CreateDraft(_companyId, PayDate.Year, PayDate.Month, PayDate);
        var policy = GosiPolicy.CreateDefault(_companyId);
        foreach (var employee in await _db.Employees.Where(e => e.CompanyId == _companyId).ToListAsync())
            run.AddEmployee(employee, policy, advanceDeduction: 0m, absenceDeduction: absence);

        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync();

        var posted = await new PayrollPostingService(_db).PostAsync(run, "admin");
        return await _db.JournalEntries
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == posted.JournalEntryId);
    }

    /// <summary>مصروفُ الأجر كلُّه — الأساسيُّ والبدلاتُ معاً، فالقسمةُ بينهما ليست موضوعَ الحالة.</summary>
    private static decimal WageExpense(JournalEntry entry) =>
        AccountDebit(entry, SaudiChartOfAccounts.SystemCodes.SalariesExpense)
        + AccountDebit(entry, "5202");

    private static decimal AccountDebit(JournalEntry entry, string code) =>
        entry.Lines.Where(l => l.AccountCode == code).Sum(l => l.Debit - l.Credit);
}
