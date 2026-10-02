using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٦ · <c>treasury-payroll-assets#4</c>) <b>المغادرُ والملتحقُ في منتصف الشهر يُصرف لهما ما عملا — لا صفرٌ ولا شهرٌ
/// كامل.</b>
///
/// <para><b>ما كان:</b> الدورةُ تُبنى ممّن هو «على رأس العمل يومَ الصرف» (٢٨ أبريل) ومن «النشِطين» وحدَهم. <b>فمغادرٌ في ١٥ أبريل يسقط
/// مرّتين</b> — <c>Terminate</c> يُطفئ علَمَ النشاط، ويومُ الصرف بعد انتهاء خدمته — <b>فلا يُصرف له نصفُ شهرٍ عمله</b>؛ وملتحقٌ في ٢٠ أبريل
/// على رأس العمل يومَ الصرف <b>فيُصرف له الشهرُ كلُّه</b>. والأجرُ ٦٬٠٠٠ واليومُ ٢٠٠ (الأجرُ ÷ ٣٠ كأيّام الغياب بلا أجر).</para>
/// </summary>
public class ALeaverAndAJoinerArePaidForTheDaysTheyWorkedTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private int _seq;

    public ALeaverAndAJoinerArePaidForTheDaysTheyWorkedTests()
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
    /// <b>القياسُ الحاكم: مغادرٌ في ١٥ أبريل يُصرف له خمسةَ عشرَ يوماً</b> — ٣٬٠٠٠ لا صفر، وسطرُه في الدورة.
    /// </summary>
    [Fact]
    public async Task ALeaverMidMonth_IsPaidForTheDaysWorked()
    {
        await SetupAsync();
        var leaver = await EmployeeAsync("E-L", new DateOnly(2024, 1, 1));
        leaver.Terminate(new DateOnly(2026, 4, 15));
        await _db.SaveChangesAsync();

        var run = await AprilAsync();

        var line = run.Lines.Single(l => l.EmployeeId == leaver.Id);
        Assert.Equal(3_000m, line.GrossPay);
    }

    /// <summary><b>وملتحقٌ في ٢٠ أبريل يُصرف له أحدَ عشرَ يوماً</b> — ٢٬٢٠٠ لا ٦٬٠٠٠.</summary>
    [Fact]
    public async Task AJoinerMidMonth_IsPaidFromTheHireDateOnly()
    {
        await SetupAsync();
        var joiner = await EmployeeAsync("E-J", new DateOnly(2026, 4, 20));

        var run = await AprilAsync();

        Assert.Equal(2_200m, run.Lines.Single(l => l.EmployeeId == joiner.Id).GrossPay);
    }

    /// <summary>
    /// <b>والمعاينةُ ترشّح المغادرَ في الشهر وتعرض ما يُصرف له</b> — فشاشةُ الرواتب تبني الدورةَ ممّن تُرشّحهم، ومغادرٌ لا يُرشَّح لا يُصرف له
    /// شيء.
    /// </summary>
    [Fact]
    public async Task ThePreview_CandidatesTheLeaver_AtHisProratedPay()
    {
        await SetupAsync();
        var leaver = await EmployeeAsync("E-L", new DateOnly(2024, 1, 1));
        leaver.Terminate(new DateOnly(2026, 4, 15));
        await _db.SaveChangesAsync();

        var candidates = await new PayrollRunBuilder(_db).CandidatesAsync(_companyId, new DateOnly(2026, 4, 28));

        Assert.Equal(3_000m, candidates.Single(c => c.EmployeeId == leaver.Id).GrossPay);
    }

    /// <summary><b>شاهدُ ضبط: مغادرُ الشهر الماضي ليس في الدورة</b> — ولا يُصرف له أبريل.</summary>
    [Fact]
    public async Task ALeaverFromLastMonth_IsNotInThisRun()
    {
        await SetupAsync();
        var gone = await EmployeeAsync("E-G", new DateOnly(2024, 1, 1));
        gone.Terminate(new DateOnly(2026, 3, 31));
        await _db.SaveChangesAsync();

        var run = await AprilAsync();

        Assert.DoesNotContain(run.Lines, l => l.EmployeeId == gone.Id);
    }

    /// <summary><b>شاهدُ ضبط: الشهرُ الكاملُ كما كان</b> — ٦٬٠٠٠ بلا خصم.</summary>
    [Fact]
    public async Task AFullMonth_IsUnchanged()
    {
        await SetupAsync();
        var full = await EmployeeAsync("E-F", new DateOnly(2024, 1, 1));

        var run = await AprilAsync();

        var line = run.Lines.Single(l => l.EmployeeId == full.Id);
        Assert.Equal(6_000m, line.GrossPay);
        Assert.Equal(0m, line.AbsenceDeduction);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<PayrollRun> AprilAsync() =>
        await new PayrollRunBuilder(_db).BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));

    private async Task<Employee> EmployeeAsync(string code, DateOnly hired)
    {
        _seq++;
        var employee = Employee.Create(_companyId, code, "موظّف " + code, $"20999999{_seq:D2}",
            EmployeeNationality.NonSaudi, "فنّي", hired);
        employee.SetPay(basic: 6_000m, housing: 0m, transport: 0m);
        employee.SetIban($"SA03800000006080101675{_seq:D2}");
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;

        // ‏موظّفٌ بشهرٍ كاملٍ دائماً — فلا تُرفض دورةٌ بلا سطورٍ حين يكون المقيسُ وحدَه خارجها.
        await EmployeeAsync("E-000", new DateOnly(2024, 1, 1));
    }
}
