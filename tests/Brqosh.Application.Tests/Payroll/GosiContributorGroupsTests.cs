using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٨ · <c>treasury-payroll-assets#7</c>) <b>فئتا المشتركين في نظام التأمينات الاجتماعيّة الجديد (٢٠٢٤).</b>
///
/// <para>‏كانت سياسةُ التأمينات نسبةً سعوديّةً واحدةً لكلّ منشأة. <b>والنظامُ الجديدُ الساري من ٣ يوليو ٢٠٢٤</b> يخصّ السعوديَّ
/// المسجَّلَ أوّلَ مرّةٍ بعده بلا مددِ اشتراكٍ سابقة: <b>نسبةُ المعاش ترتفع نصفَ نقطةٍ لكلّ طرفٍ كلَّ يوليو من ٢٠٢٥ إلى ٢٠٢٨</b>
/// (من ٩٪ إلى ١١٪)، والساندُ ٠٫٧٥٪ والأخطارُ المهنيّةُ ٢٪ على صاحب العمل كما هما — <b>ومن سبق تسجيلُه يبقى على النسب
/// القديمة</b>. فموظّفان سعوديّان بأجرٍ واحدٍ في شهرٍ واحدٍ يختلف اشتراكُهما.</para>
///
/// <para>‏<b>والنِّسَبُ افتراضاتٌ يملكها صاحبُ المنشأة</b> كما كانت (الجدولُ في السياسة لا في الشيفرة)، <b>والفئةُ على الموظّف
/// صراحةً</b>: تاريخُ الالتحاق لا يكفي — أكثرُ من يلتحق بعد يوليو ٢٠٢٤ له مددٌ سابقةٌ فهو على القديم.</para>
/// </summary>
public class GosiContributorGroupsTests
{
    private static Employee Saudi(GosiRegime regime, DateOnly hired)
    {
        var employee = Employee.Create(1L, "E-1", "مشترك", "1011111111", EmployeeNationality.Saudi, "محاسب", hired);
        employee.SetPay(basic: 10_000m, housing: 2_000m, transport: 0m);
        employee.SetGosiRegime(regime);
        return employee;
    }

    /// <summary>
    /// ‏<b>القياسُ الحاكم — الجدولُ شهراً شهراً:</b> قبل يوليو ٢٠٢٥ كالقديم، ثمّ نصفُ نقطةٍ لكلّ طرفٍ كلَّ يوليو، ويقف بعد الرابعة.
    /// </summary>
    [Theory]
    [InlineData(2024, 9, 9.75, 11.75)]
    [InlineData(2025, 6, 9.75, 11.75)]
    [InlineData(2025, 7, 10.25, 12.25)]
    [InlineData(2026, 6, 10.25, 12.25)]
    [InlineData(2026, 7, 10.75, 12.75)]
    [InlineData(2027, 7, 11.25, 13.25)]
    [InlineData(2028, 7, 11.75, 13.75)]
    [InlineData(2031, 1, 11.75, 13.75)]
    public void ANewLawContributor_FollowsTheAnnualSchedule(int year, int month, double employeePct, double employerPct)
    {
        var policy = GosiPolicy.CreateDefault(1L);
        var employee = Saudi(GosiRegime.NewLaw2024, new DateOnly(2024, 8, 1));

        var gosi = policy.ContributionFor(employee, new DateOnly(year, month, 1));

        Assert.Equal(Math.Round(12_000m * (decimal)employeePct / 100m, 2), gosi.Employee);
        Assert.Equal(Math.Round(12_000m * (decimal)employerPct / 100m, 2), gosi.Employer);
    }

    /// <summary>‏<b>ومن سبق تسجيلُه على القديم في كلّ شهر</b> — بالأجر نفسِه في الشهر نفسِه يختلف عن الجديد.</summary>
    [Fact]
    public void ALegacyContributor_KeepsTheOldRates_InTheSameMonth()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        var month = new DateOnly(2026, 10, 1);

        var legacy = policy.ContributionFor(Saudi(GosiRegime.Legacy, new DateOnly(2024, 8, 1)), month);
        var newLaw = policy.ContributionFor(Saudi(GosiRegime.NewLaw2024, new DateOnly(2024, 8, 1)), month);

        Assert.Equal(1_170m, legacy.Employee);   // 12,000 × 9.75%
        Assert.Equal(1_410m, legacy.Employer);   // 12,000 × 11.75%
        Assert.Equal(1_290m, newLaw.Employee);   // 12,000 × 10.75%
        Assert.Equal(1_530m, newLaw.Employer);   // 12,000 × 12.75%
    }

    /// <summary>‏<b>والوافدُ لا تمسّه الفئة</b> — الأخطارُ المهنيّةُ وحدَها على صاحب العمل.</summary>
    [Fact]
    public void ANonSaudi_IsUnaffected()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        var expat = Employee.Create(1L, "E-2", "وافد", "2022222222", EmployeeNationality.NonSaudi, "فنّي", new DateOnly(2025, 1, 1));
        expat.SetPay(basic: 6_000m, housing: 1_500m, transport: 0m);

        var gosi = policy.ContributionFor(expat, new DateOnly(2027, 8, 1));

        Assert.Equal(0m, gosi.Employee);
        Assert.Equal(150m, gosi.Employer);
    }

    /// <summary>
    /// ‏<b>والجدولُ يملكه صاحبُ المنشأة</b> — المنظِّمُ يُعدّل، والتعديلُ إعدادٌ لا إصدار؛ وقيمٌ لا معنى لها تُرفض.
    /// </summary>
    [Fact]
    public void TheSchedule_IsTheOwnersToCorrect_WithinSaneBounds()
    {
        var policy = GosiPolicy.CreateDefault(1L);
        policy.SetNewLawSchedule(employeePercent: 9.75m, employerPercent: 11.75m, annualStepPercent: 0.25m,
            firstStep: new DateOnly(2025, 7, 1), steps: 8);

        var gosi = policy.ContributionFor(Saudi(GosiRegime.NewLaw2024, new DateOnly(2024, 8, 1)), new DateOnly(2026, 7, 1));
        Assert.Equal(Math.Round(12_000m * 10.25m / 100m, 2), gosi.Employee);

        Assert.Throws<AccountingException>(() =>
            policy.SetNewLawSchedule(9.75m, 11.75m, -0.5m, new DateOnly(2025, 7, 1), 4));
        Assert.Throws<AccountingException>(() =>
            policy.SetNewLawSchedule(9.75m, 11.75m, 0.5m, new DateOnly(2025, 7, 1), 0));
        Assert.Throws<AccountingException>(() =>
            policy.SetNewLawSchedule(99m, 11.75m, 0.5m, new DateOnly(2025, 7, 1), 4));
    }

    /// <summary>‏<b>والفئةُ للسعوديّ وحدَه</b> — وافدٌ «على النظام الجديد» لا معنى له فيُرفض.</summary>
    [Fact]
    public void OnlyASaudi_CanBeOnTheNewLaw()
    {
        var expat = Employee.Create(1L, "E-3", "وافد", "2033333333", EmployeeNationality.NonSaudi, "فنّي", new DateOnly(2025, 1, 1));

        Assert.Throws<AccountingException>(() => expat.SetGosiRegime(GosiRegime.NewLaw2024));
        Assert.Equal(GosiRegime.Legacy, expat.GosiRegime);
    }

    /// <summary>
    /// ‏<b>وسياسةُ منشأةٍ قائمةٍ تخرج من الهجرة بافتراضات النطاق لا أصفار</b> — على PostgreSQL حقيقيّ. والمولَّدُ كان يضع ٠٪
    /// و٠٠٠١-٠١-٠١ وصفرَ زيادات، فيُشترك موظّفُ النظام الجديد في كلّ منشأةٍ قائمةٍ بلا شيء.
    /// </summary>
    [PostgresFact]
    public async Task AnExistingPolicy_LeavesTheMigrationWithTheDomainDefaults()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("gosi_groups_upgrade");
        var all = scope.Db.Database.GetMigrations().ToList();
        await scope.Db.GetService<IMigrator>().MigrateAsync(all[^2]);
        await scope.Db.Database.ExecuteSqlRawAsync("INSERT INTO gosi_policies (\"CompanyId\") VALUES (77)");

        var folder = Path.Combine(Path.GetTempPath(), "brqosh-gosi-" + Guid.NewGuid().ToString("N")[..8]);
        await new DatabaseSchemaService(scope.Db, new PreUpgradeBackup(scope.Db, folder)).EnsureSchemaAsync();

        await using var db = scope.OpenIndependentContext();
        var policy = await db.GosiPolicies.SingleAsync(g => g.CompanyId == 77);
        Assert.Equal(9.75m, policy.NewLawEmployeePercent);
        Assert.Equal(11.75m, policy.NewLawEmployerPercent);
        Assert.Equal(0.5m, policy.NewLawAnnualStepPercent);
        Assert.Equal(new DateOnly(2025, 7, 1), policy.NewLawFirstStep);
        Assert.Equal(4, policy.NewLawSteps);
        try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
    }

    /// <summary>‏<b>والافتراضُ «القديم»</b> — فلا يتغيّر راتبٌ قائمٌ صامتاً عند الترقية، ويختار صاحبُ المنشأة الفئةَ صراحةً.</summary>
    [Fact]
    public void ANewEmployee_DefaultsToTheLegacyGroup() =>
        Assert.Equal(GosiRegime.Legacy, Employee.Create(1L, "E-4", "جديد", "1044444444",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2026, 1, 1)).GosiRegime);
}
