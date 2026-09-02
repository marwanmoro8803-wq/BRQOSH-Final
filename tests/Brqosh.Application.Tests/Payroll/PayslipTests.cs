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
/// (CEEF-230 · ط2-٦) <b>قسيمةُ الراتب.</b>
///
/// <para><b>ما كان:</b> لا مستندَ لموظّفٍ من أيّ نوع — لا قسيمةَ ولا تقريرَ ولا سجلّاً
/// فردياً، <b>وشاشةُ الرواتب تعرض المجاميعَ وحدها بعد الترحيل</b>. فلا يستطيع المالكُ أن
/// يُعطي موظّفاً ورقةً تشرح راتبَه، <b>فيكتبها في وورد</b> — وأرقامُها لا تُطابق الدفاتر
/// بالضرورة.</para>
/// </summary>
public class PayslipTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public PayslipTests()
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
    /// <b>القياسُ المركزيّ: القسيمةُ تُفصّل ما جُمع.</b>
    ///
    /// <para>والصافي = الاستحقاقُ − الاستقطاع، <b>ويطابق ما رُحِّل في الدفاتر</b>.</para>
    /// </summary>
    [Fact]
    public async Task ThePayslip_DetailsWhatWasPosted()
    {
        var run = await PostRunAsync(basic: 8_000m, housing: 2_000m, transport: 500m);

        var slips = await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id);
        var slip = Assert.Single(slips);

        Assert.Equal("2026-06", slip.PeriodCode);
        Assert.Equal(8_000m, slip.BasicSalary);
        Assert.Equal(2_000m, slip.HousingAllowance);
        Assert.Equal(10_500m, slip.GrossPay);
        Assert.Equal(975m, slip.GosiEmployee);        // 10,000 × 9.75%
        Assert.Equal(9_525m, slip.NetPay);

        // ‏ويطابق ما حمله السطرُ المرحَّل حرفاً.
        Assert.Equal(run.Lines[0].NetPay, slip.NetPay);
    }

    /// <summary>
    /// <b>وتُبنى من الدورة لا من السجلّ الحاليّ.</b>
    ///
    /// <para>فأجرُ الموظّف يتغيّر، <b>وبناؤها من السجلّ يُعطيه قسيمةَ يناير بأرقام يونيو</b>
    /// — وهي إعادةُ كتابةِ تاريخٍ مضى.</para>
    /// </summary>
    [Fact]
    public async Task ARaiseAfterPosting_DoesNotRewriteThePayslip()
    {
        var run = await PostRunAsync(basic: 8_000m, housing: 2_000m, transport: 0m);

        var employee = await _db.Employees.FirstAsync(e => e.CompanyId == _companyId);
        employee.SetPay(basic: 20_000m, housing: 5_000m, transport: 0m);
        await _db.SaveChangesAsync();

        var slip = (await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id))[0];

        Assert.Equal(8_000m, slip.BasicSalary);
        Assert.Equal(10_000m, slip.GrossPay);
    }

    /// <summary><b>ولا تُصدَر من مسودّة</b> — وقسيمةٌ سُلّمت ثمّ تغيّر أصلُها خلافٌ لا يُحسم.</summary>
    [Fact]
    public async Task ADraftRun_HasNoPayslips()
    {
        await SetupAsync();
        var run = PayrollRun.CreateDraft(_companyId, 2026, 8, new DateOnly(2026, 8, 28));
        run.AddLine("موظّف", 5_000m);
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(
            () => new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id));
    }

    /// <summary>
    /// <b>وسطرٌ بلا سجلٍّ يخرج قسيمةً كذلك.</b>
    ///
    /// <para>أرقامُ الراتب كلُّها على السطر، والناقصُ هويّةٌ ومسمّى. <b>وحجبُ قسيمته يحرم
    /// موظّفاً حقّاً لأنّ سجلَّه لم يُنشأ بعد</b> — والنقصُ يُرى في الورقة لا يُخفيها.</para>
    /// </summary>
    [Fact]
    public async Task AManualLineWithoutARegisterEntry_StillGetsASlip()
    {
        await SetupAsync();
        var run = PayrollRun.CreateDraft(_companyId, 2026, 9, new DateOnly(2026, 9, 28));
        run.AddLine("عاملٌ بلا سجلّ", basic: 3_000m, housing: 500m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var slip = Assert.Single(await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id));

        Assert.Equal("عاملٌ بلا سجلّ", slip.EmployeeName);
        Assert.Equal("—", slip.NationalId);
        Assert.Equal(3_500m, slip.GrossPay);
    }

    /// <summary><b>وتُطبَع ورقةً</b> — فقسيمةٌ لا تُطبَع لا تصل الموظّف.</summary>
    [Fact]
    public async Task ThePayslip_RendersToPdf()
    {
        var run = await PostRunAsync(basic: 6_000m, housing: 1_500m, transport: 300m);
        var slip = (await new PayslipQuery(_db).ExecuteAsync(_companyId, run.Id))[0];

        var pdf = PayslipPdfGenerator.Build(
            PrintBranding.From("منشأة القياس", "310123456700003"), slip);

        Assert.True(pdf.Length > 1_000, $"ملفٌّ بحجم {pdf.Length} بايت — أصغرُ من صفحةٍ حقيقية.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<PayrollRun> PostRunAsync(decimal basic, decimal housing, decimal transport)
    {
        await SetupAsync();
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);

        var employee = Employee.Create(_companyId, "E-1", "موظّف القياس", "1011111111",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        employee.SetPay(basic, housing, transport);
        employee.SetIban("SA0380000000608010167519");
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var run = PayrollRun.CreateDraft(_companyId, 2026, 6, new DateOnly(2026, 6, 28));
        run.AddEmployee(employee, policy);
        await new PayrollPostingService(_db).PostAsync(run, "admin");
        return run;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة القسائم", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
