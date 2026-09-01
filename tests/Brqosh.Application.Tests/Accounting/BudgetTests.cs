using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-230 · ط1-٦) <b>الموازنة والانحراف.</b>
///
/// <para><b>ما كان:</b> صفرٌ تام — لا كيانَ ولا جدولَ ولا تقريرَ ولا حارس. <b>والمطابقةُ
/// الوحيدة لكلمة «موازنة» في المستودع كلِّه كانت نصَّ اقتراحٍ في مساعد الذكاء الاصطناعيّ</b>:
/// «قارِنِ التدفّقَ النقديَّ بالموازنة» — اقتراحٌ لا يقود إلى شيء.</para>
/// </summary>
public class BudgetTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public BudgetTests()
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

    // ── النطاق ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وسطران للحساب نفسِه في الشهر نفسِه يُجمَعان لا يُكرَّران.</b>
    ///
    /// <para>فتكرارُهما يجعل «المُخطَّط» رقمين، <b>ويُقرأ التقريرُ بأيّهما صادف</b>.</para>
    /// </summary>
    [Fact]
    public void SettingTheSameLineTwice_Replaces()
    {
        var budget = Budget.Create(1L, "B-1", "موازنة 2026", 2026);
        budget.SetLine("5201", 3, 10_000m);
        budget.SetLine("5201", 3, 12_000m);

        Assert.Single(budget.Lines);
        Assert.Equal(12_000m, budget.TotalPlanned);
    }

    /// <summary><b>ومركزُ التكلفة يفصل السطرَ عن أخيه</b> — فالمُخطَّطُ لكلّ مركزٍ مستقلّ.</summary>
    [Fact]
    public void LinesAreSeparatedByCostCenter()
    {
        var budget = Budget.Create(1L, "B-2", "موازنة", 2026);
        budget.SetLine("5201", 3, 10_000m, costCenterId: 1L);
        budget.SetLine("5201", 3, 4_000m, costCenterId: 2L);

        Assert.Equal(2, budget.Lines.Count);
        Assert.Equal(14_000m, budget.TotalPlanned);
    }

    /// <summary><b>والمُعتمَدةُ لا تُعدَّل</b> — وإلا صار «المُخطَّط» ما بعد وقوع الفعل.</summary>
    [Fact]
    public void AnApprovedBudget_CannotBeEdited()
    {
        var budget = Budget.Create(1L, "B-3", "موازنة", 2026);
        budget.SetLine("5201", 1, 1_000m);
        budget.Approve();

        Assert.Throws<AccountingException>(() => budget.SetLine("5201", 2, 2_000m));

        budget.Reopen();
        budget.SetLine("5201", 2, 2_000m);
        Assert.Equal(2, budget.Lines.Count);
    }

    /// <summary><b>ولا تُعتمد موازنةٌ بلا سطور.</b></summary>
    [Fact]
    public void AnEmptyBudget_CannotBeApproved() =>
        Assert.Throws<AccountingException>(() => Budget.Create(1L, "B-4", "فارغة", 2026).Approve());

    // ── الانحراف ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: المُخطَّطُ والفعليُّ في سطرٍ واحد.</b>
    ///
    /// <para>وخُطّط ١٠٬٠٠٠ وأُنفق ٧٬٠٠٠ — فالوفرُ ٣٬٠٠٠ والاستهلاكُ ٧٠٪.</para>
    /// </summary>
    [Fact]
    public async Task PlannedAndActual_MeetInOneRow()
    {
        await SetupAsync();
        var budget = await ApprovedBudgetAsync("B-10", ("5201", 3, 10_000m));
        await PostExpenseAsync("5201", 7_000m, new DateOnly(2026, 3, 10));

        var rows = await new BudgetVarianceQuery(_db).ExecuteAsync(
            _companyId, budget.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var row = Assert.Single(rows);
        Assert.Equal(10_000m, row.Planned);
        Assert.Equal(7_000m, row.Actual);
        Assert.Equal(3_000m, row.Variance);
        Assert.Equal(70m, row.ConsumedPercent);
        Assert.False(row.IsOverspent);
    }

    /// <summary>
    /// <b>وما أُنفق ولم يُخطَّط يُعرض — وهو أهمُّ الثلاث وأكثرُ ما يُنسى.</b>
    ///
    /// <para>تقريرٌ يبدأ من سطور الموازنة وحدها <b>يُخفي كلَّ مصروفٍ لم يُخطَّط له</b>،
    /// فيمدح موازنةً لم تُتجاوَز <b>لأنّها لم تُكتب</b> — وهو بالضبط ما يبحث عنه قارئُها.</para>
    /// </summary>
    [Fact]
    public async Task SpendWithNoBudgetLine_IsShown()
    {
        await SetupAsync();
        var budget = await ApprovedBudgetAsync("B-11", ("5201", 3, 5_000m));
        await PostExpenseAsync("5203", 2_500m, new DateOnly(2026, 3, 10));

        var rows = await new BudgetVarianceQuery(_db).ExecuteAsync(
            _companyId, budget.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var unplanned = Assert.Single(rows, r => r.AccountCode == "5203");
        Assert.Equal(0m, unplanned.Planned);
        Assert.Equal(2_500m, unplanned.Actual);
        Assert.True(unplanned.IsOverspent);
        Assert.Null(unplanned.ConsumedPercent);
    }

    /// <summary><b>وما خُطّط ولم يُنفق يُعرض كذلك</b> — فالوفرُ معلومةٌ لا فراغ.</summary>
    [Fact]
    public async Task PlannedButUnspent_IsShown()
    {
        await SetupAsync();
        var budget = await ApprovedBudgetAsync("B-12", ("5201", 3, 8_000m));

        var rows = await new BudgetVarianceQuery(_db).ExecuteAsync(
            _companyId, budget.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var row = Assert.Single(rows);
        Assert.Equal(8_000m, row.Planned);
        Assert.Equal(0m, row.Actual);
        Assert.Equal(8_000m, row.Variance);
    }

    /// <summary>
    /// <b>وإشارةُ الانحراف تنقلب للإيراد.</b>
    ///
    /// <para>فالمصروفُ أقلَّ من مُخطَّطه <b>وفر</b>، والإيرادُ أكثرَ من مُخطَّطه <b>زيادة</b> —
    /// <b>وإشارةٌ واحدةٌ لمعنيين متعاكسين تُقرأ خطأً حتماً</b>.</para>
    /// </summary>
    [Fact]
    public async Task RevenueVariance_IsSignedTheOtherWay()
    {
        await SetupAsync();
        var budget = await ApprovedBudgetAsync("B-13", ("4101", 3, 50_000m));
        await PostRevenueAsync(60_000m, new DateOnly(2026, 3, 10));

        var row = Assert.Single(await new BudgetVarianceQuery(_db).ExecuteAsync(
            _companyId, budget.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        Assert.Equal(60_000m, row.Actual);
        Assert.Equal(10_000m, row.Variance);      // زيادةٌ لا تجاوز
        Assert.False(row.IsOverspent);
    }

    /// <summary>
    /// <b>وشهرٌ خارج المدى لا يُحسب.</b>
    ///
    /// <para>وبغير هذا القياس يمرّ تقريرٌ يجمع السنةَ كلَّها في كلّ مدى — <b>ويُقرأ تجاوزاً
    /// حيث لا تجاوز</b>.</para>
    /// </summary>
    [Fact]
    public async Task MonthsOutsideTheRange_AreExcluded()
    {
        await SetupAsync();
        var budget = await ApprovedBudgetAsync("B-14", ("5201", 3, 6_000m), ("5201", 7, 9_000m));
        await PostExpenseAsync("5201", 1_000m, new DateOnly(2026, 3, 10));

        var row = Assert.Single(await new BudgetVarianceQuery(_db).ExecuteAsync(
            _companyId, budget.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        Assert.Equal(6_000m, row.Planned);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Budget> ApprovedBudgetAsync(string code, params (string Account, int Month, decimal Amount)[] lines)
    {
        var budget = Budget.Create(_companyId, code, "موازنة القياس", 2026);
        foreach (var (account, month, amount) in lines)
            budget.SetLine(account, month, amount);
        budget.Approve();

        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
        return budget;
    }

    private async Task PostExpenseAsync(string accountCode, decimal amount, DateOnly date)
    {
        var expense = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == accountCode);
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101");

        var entry = JournalEntry.CreateDraft(_companyId, date, "مصروف");
        entry.AddLine(expense, amount, 0m);
        entry.AddLine(cash, 0m, amount);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task PostRevenueAsync(decimal amount, DateOnly date)
    {
        var revenue = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "4101");
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101");

        var entry = JournalEntry.CreateDraft(_companyId, date, "إيراد");
        entry.AddLine(cash, amount, 0m);
        entry.AddLine(revenue, 0m, amount);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الموازنة", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
