using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٤) <b>قسيمةُ الراتب كانت تُخالف ما يصل حسابَ الموظّف.</b>
///
/// <para><c>PayrollLine.NetPay</c> — وهو ما يُرحَّل ويُحوَّل وتحمله ملفّاتُ الأجور —
/// <c>= GrossPay − GosiEmployee − AdvanceDeduction</c>، و<c>GrossPay</c> محسوبٌ
/// <b>بعد خصم الغياب</b>. <b>والقسيمةُ كانت تحسب غيرَ ذلك مرّتين:</b></para>
///
/// <list type="number">
/// <item><b>الاستحقاقُ خامّ</b>: تجمع مكوّناتِ الراتب ولا تطرح الغياب.</item>
/// <item><b>والاستقطاعُ تأميناتٌ فقط</b>: <c>TotalDeductions => GosiEmployee</c> —
/// <b>ولا تعرف السلفة</b>، ولم تكن تُمرَّر إليها أصلاً.</item>
/// </list>
///
/// <para><b>وكلا الخطأين يرفع الرقم</b>: الورقةُ التي يأخذها الموظّف تَعِد بأكثرَ ممّا
/// يُحوَّل إليه. <b>وهو خلافٌ يقع في كلّ شهرٍ فيه سلفةٌ أو إجازةٌ بلا أجر</b> — وأوّلُ
/// من يكتشفه الموظّفُ لا المحاسب.</para>
///
/// <para><b>والقياسُ الحاكم هويّةٌ لا رقم</b>: <c>قسيمة.NetPay == سطر.NetPay</c> —
/// فلا تُصلَح بمقارنةِ مثالٍ واحد، <b>ولا تنكسر صامتةً إن تغيّر أحدُ الطرفين</b>.</para>
/// </summary>
public class PayslipAgreesWithWhatIsPaidTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public PayslipAgreesWithWhatIsPaidTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── الهويّةُ الحاكمة ────────────────────────────────────────────────────

    /// <summary>
    /// <b>ما في الورقة هو ما يصل الحساب</b> — مع سلفةٍ وغيابٍ معاً، وهي الحالةُ التي
    /// كان الخطآن يتراكمان فيها.
    /// </summary>
    [Fact]
    public async Task ThePayslipNet_EqualsThePostedNet_WithAnAdvanceAndAnAbsence()
    {
        var run = await RunAsync(advance: 500m, absence: 300m);

        var slip = (await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id)).Single();
        var line = run.Lines.Single();

        Assert.Equal(line.NetPay, slip.NetPay);
        Assert.Equal(line.GrossPay, slip.GrossPay);
    }

    /// <summary>
    /// <b>والسلفةُ تظهر استقطاعاً</b> — لا مطويّةً في الصافي. <b>فالموظّفُ يقرأ سببَ
    /// النقص لا يحسبه.</b>
    /// </summary>
    [Fact]
    public async Task TheAdvance_AppearsAsADeduction_NotFoldedIntoTheNet()
    {
        var run = await RunAsync(advance: 500m, absence: 0m);

        var slip = (await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id)).Single();

        Assert.Equal(500m, slip.AdvanceDeduction);
        Assert.Equal(slip.GosiEmployee + 500m, slip.TotalDeductions);
    }

    /// <summary>
    /// <b>والغيابُ يُطرح من الاستحقاق</b> — فالإجماليُّ في الورقة هو الإجماليُّ المُرحَّل.
    /// </summary>
    [Fact]
    public async Task TheAbsence_ReducesTheGrossOnThePaper_AsItDoesInTheLedger()
    {
        var run = await RunAsync(advance: 0m, absence: 300m);

        var slip = (await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id)).Single();
        var line = run.Lines.Single();

        Assert.Equal(300m, slip.AbsenceDeduction);
        Assert.Equal(line.GrossPay, slip.GrossPay);
    }

    // ── شاهدُ الضبط ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وشهرٌ بلا سلفةٍ ولا غياب لم يتغيّر بريال</b> — وهو حالُ أكثر الشهور.
    /// <b>فالإصلاحُ أضاف ما كان ناقصاً ولم يُزحزح ما كان صحيحاً.</b>
    /// </summary>
    [Fact]
    public async Task AnOrdinaryMonth_IsUnchanged()
    {
        var run = await RunAsync(advance: 0m, absence: 0m);

        var slip = (await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id)).Single();
        var line = run.Lines.Single();

        Assert.Equal(line.NetPay, slip.NetPay);
        Assert.Equal(0m, slip.AdvanceDeduction);
        Assert.Equal(0m, slip.AbsenceDeduction);
        Assert.Equal(slip.GosiEmployee, slip.TotalDeductions);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<PayrollRun> RunAsync(decimal advance, decimal absence)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الرواتب", "310123456700088", BusinessProfile.General);
        _companyId = company.Id;

        var employee = Employee.Create(_companyId, "E-1", "موظّف", "1012345678",
            EmployeeNationality.Saudi, "فنّيّ", new DateOnly(2024, 1, 1));
        employee.SetPay(basic: 6_000m, housing: 1_500m, transport: 500m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var payDate = new DateOnly(2026, 8, 31);
        var run = PayrollRun.CreateDraft(_companyId, payDate.Year, payDate.Month, payDate);
        run.AddEmployee(employee, GosiPolicy.CreateDefault(_companyId),
            advanceDeduction: advance, absenceDeduction: absence);

        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync();

        // ‏**والقسيمةُ لا تُصدَر من مسودّة** — حارسٌ قائمٌ في `PayslipQuery`، وهو صوابٌ:
        // ‏المسودّةُ تتغيّر بعد التسليم. فتُرحَّل الدورةُ كما تُرحَّل في الإنتاج.
        await new PayrollPostingService(_db).PostAsync(run, "admin");
        return run;
    }
}
