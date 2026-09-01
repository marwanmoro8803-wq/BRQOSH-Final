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
/// (CEEF-230 · ط2-٤ · ط2-٥) <b>فصلُ البدلات، وملفُّ حماية الأجور.</b>
///
/// <para><b>ما كان:</b> <c>5202 بدلات السكن والانتقال</c> <b>حسابٌ ميّتٌ في الشجرة</b> —
/// الإجماليُّ كلُّه يذهب إلى <c>5201</c>. فمالكٌ يسأل «كم أنفقتُ على بدلات السكن؟» لا
/// يجد جواباً، <b>والرقمُ موجودٌ في كلّ سطرِ رواتبَ ولا يخرج منه</b>.</para>
///
/// <para><b>ونظامُ حماية الأجور صفرٌ تام</b> — ولا ملفَّ ولا حقلاً ولا ذكراً. <b>وهو
/// إلزاميٌّ في السعودية</b>: المنشأةُ التي لا تُقدّم ملفَّها تُعلَّق خدماتُها.</para>
/// </summary>
public class WageProtectionAndAllowanceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public WageProtectionAndAllowanceTests()
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

    // ── فصلُ البدلات ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>الأساسيُّ في حسابه والبدلاتُ في حسابها — والمجموعُ لا يتغيّر.</b>
    ///
    /// <para>أساسيٌّ 8,000 وسكنٌ 2,000 ونقلٌ 500 ⟵ <c>5201</c> يحمل 8,000 و<c>5202</c>
    /// يحمل 2,500. <b>وكان 5201 يحمل 10,500 و5202 صفراً أبداً.</b></para>
    /// </summary>
    [Fact]
    public async Task Allowances_PostToTheirOwnAccount()
    {
        await SetupAsync();
        await PostRunAsync(basic: 8_000m, housing: 2_000m, transport: 500m);

        Assert.Equal(8_000m, await BalanceAsync("5201"));
        Assert.Equal(2_500m, await BalanceAsync("5202"));

        // ‏والمجموعُ كما كان — فلا ينكسر صافي الربح ولا هويّة.
        Assert.Equal(10_500m, await BalanceAsync("5201") + await BalanceAsync("5202"));
    }

    /// <summary><b>ودورةٌ بلا بدلاتٍ لا تُنتج سطرَ بدلاتٍ فارغاً.</b></summary>
    [Fact]
    public async Task ARunWithNoAllowances_AddsNoAllowanceLine()
    {
        await SetupAsync();
        await PostRunAsync(basic: 5_000m, housing: 0m, transport: 0m);

        Assert.Equal(5_000m, await BalanceAsync("5201"));
        Assert.Equal(0m, await BalanceAsync("5202"));
    }

    // ── ملفُّ الأجور ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: الملفُّ يخرج بحقول النظام.</b>
    ///
    /// <para>هويّةٌ واسمٌ وآيبانٌ وأساسيٌّ وسكنٌ وبدلاتٌ واستقطاعاتٌ وصافٍ — <b>وبترويسةٍ
    /// صريحة</b>، فملفٌّ يُقرأ بالترتيب وحده يُرسل بدلَ السكن استقطاعاً إن أزاح عمودٌ عموداً.</para>
    /// </summary>
    [Fact]
    public async Task TheFile_CarriesTheRequiredFields()
    {
        await SetupAsync();
        var run = await PostRunAsync(basic: 8_000m, housing: 2_000m, transport: 500m,
            iban: "SA0380000000608010167519");

        var result = await new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id);

        Assert.Equal(1, result.Records);
        Assert.Empty(result.Refusals);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("NationalId,EmployeeName,Iban", lines[0], StringComparison.Ordinal);

        var fields = lines[1].Trim().Split(',');
        Assert.Equal("1011111111", fields[0]);
        Assert.Equal("SA0380000000608010167519", fields[2]);
        Assert.Equal("8000.00", fields[3]);      // أساسيّ
        Assert.Equal("2000.00", fields[4]);      // سكن
        Assert.Equal("500.00", fields[5]);       // بدلاتٌ أخرى
        Assert.Equal("975.00", fields[6]);       // استقطاعُ التأمينات
    }

    /// <summary>
    /// <b>وموظّفٌ بلا آيبان يُسمّى ولا يُبتلع.</b>
    ///
    /// <para><b>وملفٌّ ناقصٌ يُرفَض عند الجهة بلا بيانِ سبب</b> — فيُقال السببُ هنا، بالاسم،
    /// قبل أن يُرسَل.</para>
    /// </summary>
    [Fact]
    public async Task AnEmployeeWithoutAnIban_IsNamedNotSkippedSilently()
    {
        await SetupAsync();
        var run = await PostRunAsync(basic: 5_000m, housing: 0m, transport: 0m, iban: null);

        var result = await new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id);

        Assert.Equal(0, result.Records);
        Assert.Single(result.Refusals);
        Assert.Contains("بلا آيبان", result.Refusals[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا يُصدَّر من مسودّة.</b>
    ///
    /// <para>فالمسودّةُ تتغيّر بعد التصدير — <b>ويُرسَل إلى الجهة ملفٌّ لا يطابق الدفاتر</b>.</para>
    /// </summary>
    [Fact]
    public async Task ADraftRun_CannotBeExported()
    {
        await SetupAsync();
        var run = PayrollRun.CreateDraft(_companyId, 2026, 7, new DateOnly(2026, 7, 28));
        run.AddLine("موظّف", 5_000m);
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id));

        Assert.Contains("مرحّلة", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>واسمٌ فيه فاصلةٌ لاتينيةٌ يُقتبَس.</b>
    ///
    /// <para>وإلا <b>أزاح كلَّ عمودٍ بعده</b>، فيصير الآيبانُ في خانة الأساسيّ — عطلٌ لا
    /// يُكتشف إلا حين يُرفَض الملفّ عند الجهة.</para>
    ///
    /// <para><b>والفاصلةُ العربية «،» ليست فاصلةَ حقل</b> — وقد كتبتُ هذا القياسَ بها
    /// أوّلاً فسقط، <b>والشيفرةُ كانت على صواب</b>. فالحرفان يتشابهان في العين ويفترقان
    /// في المعنى، والقياسان أدناه يُثبّتان الفرقَ بدل أن يُنسى.</para>
    /// </summary>
    [Fact]
    public async Task ANameWithALatinComma_IsQuoted()
    {
        await SetupAsync();
        var run = await PostRunAsync(basic: 5_000m, housing: 0m, transport: 0m,
            iban: "SA0380000000608010167519", name: "Ali, Mohammed");

        var result = await new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id);
        var row = result.Csv.Split('\n')[1];

        Assert.Contains("\"Ali, Mohammed\"", row, StringComparison.Ordinal);

        // ‏والآيبانُ ما زال في خانته الثالثة رغم الفاصلة داخل الاسم.
        Assert.Equal("SA0380000000608010167519", SplitCsv(row)[2]);
    }

    /// <summary><b>والفاصلةُ العربيةُ لا تُقتبَس</b> — فليست فاصلةَ حقل، والاقتباسُ زينة.</summary>
    [Fact]
    public async Task AnArabicCommaNeedsNoQuoting()
    {
        await SetupAsync();
        var run = await PostRunAsync(basic: 5_000m, housing: 0m, transport: 0m,
            iban: "SA0380000000608010167519", name: "محمد، عبدالله");

        var row = (await new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id))
            .Csv.Split('\n')[1];

        Assert.DoesNotContain("\"", row, StringComparison.Ordinal);
        Assert.Equal("SA0380000000608010167519", SplitCsv(row)[2]);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>‏تقسيمٌ يحترم الاقتباس — فالقياسُ لا يُقسّم بسذاجةٍ ثمّ يشكو.</summary>
    private static string[] SplitCsv(string row)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        foreach (var ch in row.Trim())
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ',' && !quoted) { fields.Add(current.ToString()); current.Clear(); continue; }
            current.Append(ch);
        }

        fields.Add(current.ToString());
        return [.. fields];
    }

    private async Task<PayrollRun> PostRunAsync(decimal basic, decimal housing, decimal transport,
        string? iban = "SA0380000000608010167519", string name = "موظّف")
    {
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);

        var employee = Employee.Create(_companyId, $"E-{name.GetHashCode():X}", name, "1011111111",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        employee.SetPay(basic, housing, transport);
        employee.SetIban(iban);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var run = PayrollRun.CreateDraft(_companyId, 2026, 6, new DateOnly(2026, 6, 28));
        run.AddEmployee(employee, policy);
        await new PayrollPostingService(_db).PostAsync(run, "admin");
        return run;
    }

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الأجور", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
