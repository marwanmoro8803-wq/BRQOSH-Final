using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (CEEF-230 · ط2-١ · ط2-٢) <b>سجلُّ الموظفين وحسابُ التأمينات.</b>
///
/// <para><b>ما كان:</b> لا كيانَ موظّفٍ في النظام كلِّه، والاسمُ <b>نصٌّ حرٌّ يُعاد كتابتُه
/// كلَّ شهر</b> في شبكة إدخال. <b>وحصّتا التأمينات رقمان يُكتبان باليد</b> — بلا نسبةٍ
/// ولا سقفِ أجرٍ ولا تفرقةٍ بين سعوديٍّ ووافد.</para>
///
/// <para><b>فمنشأةٌ بعشرين موظّفاً تحسبها في ورقةٍ خارجية كلَّ شهر</b>، وأوّلُ خطأِ نسخٍ
/// يمرّ إلى الأستاذ بلا حارس.</para>
/// </summary>
public class EmployeeAndGosiTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public EmployeeAndGosiTests()
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

    // ── السجلّ ──────────────────────────────────────────────────────────────

    /// <summary><b>ورقمُ الهوية عشرةُ أرقام</b> — وهو مفتاحُ الموظّف لدى الجهات.</summary>
    [Theory]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345678A0")]
    [InlineData("")]
    public void AnInvalidNationalId_IsRefused(string id) =>
        Assert.Throws<AccountingException>(() => Employee.Create(
            1L, "E-1", "موظّف", id, EmployeeNationality.Saudi, "محاسب", new DateOnly(2026, 1, 1)));

    /// <summary>
    /// <b>ولا يتكرّر الرقمُ في المنشأة.</b>
    ///
    /// <para>موظّفان بالرقم نفسِه <b>راتبٌ مزدوجٌ لشخصٍ واحد</b>، أو ملفُّ أجورٍ ترفضه
    /// الجهةُ المستقبِلة.</para>
    /// </summary>
    [Fact]
    public async Task ADuplicateNationalId_IsRefusedByTheDatabase()
    {
        await SetupAsync();
        _db.Employees.Add(EmployeeFor("E-1", "1012345678"));
        await _db.SaveChangesAsync();

        _db.Employees.Add(EmployeeFor("E-2", "1012345678"));
        await Assert.ThrowsAnyAsync<Exception>(() => _db.SaveChangesAsync());
    }

    /// <summary><b>والآيبانُ السعوديّ «SA» و22 رقماً</b> — ويلزم ملفَّ حماية الأجور.</summary>
    [Fact]
    public void AnInvalidIban_IsRefused()
    {
        var employee = EmployeeFor("E-3", "1011111111");

        Assert.Throws<AccountingException>(() => employee.SetIban("SA123"));

        employee.SetIban("SA03 8000 0000 6080 1016 7519");
        Assert.Equal("SA0380000000608010167519", employee.Iban);

        employee.SetIban(null);
        Assert.Null(employee.Iban);
    }

    /// <summary><b>وانتهاءُ الخدمة إيقافٌ لا حذف</b> — فالراتبُ المرحَّل جزءٌ من دفاتر سنة.</summary>
    [Fact]
    public void Termination_StopsWithoutErasing()
    {
        var employee = EmployeeFor("E-4", "1022222222");
        employee.Terminate(new DateOnly(2026, 6, 30));

        Assert.False(employee.IsActive);
        Assert.True(employee.IsEmployedOn(new DateOnly(2026, 6, 30)));
        Assert.False(employee.IsEmployedOn(new DateOnly(2026, 7, 1)));

        Assert.Throws<AccountingException>(() => employee.Terminate(new DateOnly(2025, 1, 1)));
    }

    // ── التأمينات ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: الأجرُ الخاضع أساسيٌّ + سكن، لا الإجماليّ.</b>
    ///
    /// <para>أساسيٌّ 10,000 وسكن 2,500 ونقل 800 ⟵ الخاضع <b>12,500 لا 13,300</b>.
    /// <b>وطيُّ البدلات كلِّها يُنتج اشتراكاً أعلى ممّا يستحقّ</b> — ويدفع صاحبُ المنشأة
    /// فرقاً لا يسترجعه.</para>
    /// </summary>
    [Fact]
    public void TheContributoryWage_ExcludesTransportAndOther()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        var employee = EmployeeFor("E-5", "1033333333");
        employee.SetPay(basic: 10_000m, housing: 2_500m, transport: 800m, other: 400m);

        Assert.Equal(12_500m, policy.ContributoryWage(employee));

        var gosi = policy.ContributionFor(employee);
        Assert.Equal(1_218.75m, gosi.Employee);      // 12,500 × 9.75%
        Assert.Equal(1_468.75m, gosi.Employer);      // 12,500 × 11.75%
    }

    /// <summary>
    /// <b>والوافدُ يختلف اختلافاً جوهرياً.</b>
    ///
    /// <para>لا يُشترك في المعاش ولا الساند — <b>فحصّتُه صفر</b>، وحصّةُ صاحب العمل
    /// الأخطارُ المهنية وحدها. <b>وحسابٌ لا يفرّق بينهما يُبالغ في الاستقطاع من راتبٍ
    /// لا يستحقّه</b>.</para>
    /// </summary>
    [Fact]
    public void ANonSaudi_PaysNothingAndTheEmployerPaysOccupationalHazardsOnly()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        var employee = Employee.Create(1L, "E-6", "وافد", "2044444444",
            EmployeeNationality.NonSaudi, "فنّي", new DateOnly(2026, 1, 1));
        employee.SetPay(basic: 6_000m, housing: 1_500m, transport: 500m);

        var gosi = policy.ContributionFor(employee);
        Assert.Equal(0m, gosi.Employee);
        Assert.Equal(150m, gosi.Employer);           // 7,500 × 2%
    }

    /// <summary>
    /// <b>والسقفُ يُقيّد الأجرَ المرتفع.</b>
    ///
    /// <para>أساسيٌّ 50,000 وسكن 10,000 ⟵ الخاضع <b>45,000 لا 60,000</b>. <b>وحسابٌ بلا
    /// سقفٍ يُضاعف اشتراكَ الإدارة العليا</b> — وهو أكثرُ الأخطاء تكلفةً في الورقة الخارجية.</para>
    /// </summary>
    [Fact]
    public void TheWageCap_Applies()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        var employee = EmployeeFor("E-7", "1055555555");
        employee.SetPay(basic: 50_000m, housing: 10_000m, transport: 0m);

        Assert.Equal(45_000m, policy.ContributoryWage(employee));
        Assert.Equal(4_387.50m, policy.ContributionFor(employee).Employee);
    }

    /// <summary><b>والنِّسَبُ مملوكةٌ للمنشأة</b> — فالمُنظِّم يُعدّلها، وثابتٌ يُلغي التعديل.</summary>
    [Fact]
    public void TheRates_AreOwnedByTheCompany()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        policy.SetRates(saudiEmployee: 10m, saudiEmployer: 12m,
            nonSaudiEmployee: 0m, nonSaudiEmployer: 2m);

        var employee = EmployeeFor("E-8", "1066666666");
        employee.SetPay(basic: 10_000m, housing: 0m, transport: 0m);

        Assert.Equal(1_000m, policy.ContributionFor(employee).Employee);
        Assert.Throws<AccountingException>(() => policy.SetRates(-1m, 12m, 0m, 2m));
    }

    /// <summary><b>وتُبذَر لكلّ منشأةٍ نسختُها</b> — فالحسابُ متاحٌ من أوّل يوم.</summary>
    [Fact]
    public async Task ANewCompany_IsSeededWithAGosiPolicy()
    {
        await SetupAsync();
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);

        Assert.Equal(9.75m, policy.SaudiEmployeePercent);
        Assert.Equal(45_000m, policy.ContributoryWageCap);
    }

    // ── الدورة ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ودورةُ الرواتب تُبنى من السجلّ — لا تُكتب باليد.</b>
    ///
    /// <para>وهذا هو الفرق كلُّه: <b>التأميناتُ تُحسب ولا تُنسخ</b>، والسطرُ يحمل معرّفَ
    /// الموظّف فتُعرف قسيمتُه.</para>
    /// </summary>
    [Fact]
    public async Task ARunBuiltFromTheRegister_ComputesItsOwnGosi()
    {
        await SetupAsync();
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);

        var employee = EmployeeFor("E-9", "1077777777");
        employee.SetPay(basic: 8_000m, housing: 2_000m, transport: 500m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var run = PayrollRun.CreateDraft(_companyId, 2026, 3, new DateOnly(2026, 3, 28));
        run.AddEmployee(employee, policy);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var line = run.Lines[0];
        Assert.Equal(employee.Id, line.EmployeeId);
        Assert.Equal(975m, line.GosiEmployee);       // 10,000 × 9.75%
        Assert.Equal(1_175m, line.GosiEmployer);     // 10,000 × 11.75%
        Assert.Equal(10_500m, line.GrossPay);
        Assert.Equal(9_525m, line.NetPay);
    }

    /// <summary><b>ولا يُدرَج موظّفٌ مرّتين</b> — راتبٌ مزدوجٌ لشخصٍ واحد.</summary>
    [Fact]
    public async Task AnEmployeeCannotBeAddedTwiceToOneRun()
    {
        await SetupAsync();
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);

        var employee = EmployeeFor("E-10", "1088888888");
        employee.SetPay(5_000m, 0m, 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddEmployee(employee, policy);

        Assert.Throws<AccountingException>(() => run.AddEmployee(employee, policy));
    }

    /// <summary>
    /// <b>ومَن ليس على رأس العمل يومَ الصرف لا يُدرَج.</b>
    ///
    /// <para>موظّفٌ انتهت خدمتُه في مارس لا يُصرف له راتبُ أبريل — <b>وحارسٌ في النطاق
    /// أوثقُ من ترشيحٍ في شاشة</b>.</para>
    /// </summary>
    [Fact]
    public async Task ATerminatedEmployee_IsNotPaidAfterLeaving()
    {
        await SetupAsync();
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);

        var employee = EmployeeFor("E-11", "1099999999");
        employee.SetPay(5_000m, 0m, 0m);
        employee.Terminate(new DateOnly(2026, 3, 31));

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        var error = Assert.Throws<AccountingException>(() => run.AddEmployee(employee, policy));
        Assert.Contains("ليس على رأس العمل", error.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Employee EmployeeFor(string code, string nationalId) =>
        Employee.Create(_companyId == 0 ? 1L : _companyId, code, "موظّف", nationalId,
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2026, 1, 1));

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الرواتب", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
