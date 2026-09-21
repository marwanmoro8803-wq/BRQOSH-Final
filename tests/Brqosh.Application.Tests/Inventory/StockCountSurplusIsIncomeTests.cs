using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٣) <b>فائضُ الجرد كان يُدائن الأرباحَ المبقاة — فلا يمرّ
/// بقائمة الدخل أبداً.</b>
///
/// <para><b>والنقصُ يُقيَّد مصروفاً</b> على <c>5215</c>. <b>فالمعالجتان غيرُ متماثلتين</b>:
/// جردٌ يجد نقصاً بألفٍ وفائضاً بألفٍ يترك حقوقَ الملكية كما هي <b>ويُنقص الربحَ المُعلَن
/// ألفاً كاملة</b> — <b>ويتكرّر مع كلّ جرد</b>.</para>
///
/// <para><b>وتنكسر هويّةٌ أساسيّة</b>: صافي الدخل + الأرباحُ المبقاة أوّلَ المدّة ≠
/// الأرباحُ المبقاة آخرَها. <b>فقائمةُ الدخل تكفّ عن تفسير تغيّر حقوق الملكية</b> —
/// <b>ولا يشكو ميزانُ المراجعة</b>، فالقيدُ متوازن.</para>
///
/// <para><b>والأثرُ ماليٌّ لا عرضيّ</b>: أرباحٌ أدنى من الحقيقة تُبنى عليها الزكاةُ
/// والتوزيعات.</para>
/// </summary>
public class StockCountSurplusIsIncomeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _productId;
    private long _warehouseId;

    public StockCountSurplusIsIncomeTests()
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

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> فائضُ جردٍ بألفٍ ⟵ <b>إيرادٌ في قائمة الدخل</b> (<c>4106</c>)،
    /// <b>ولا يُمَسّ حسابُ الأرباح المبقاة بريال</b>.
    /// </summary>
    [Fact]
    public async Task ACountSurplus_IsBookedAsIncome_NotStraightToEquity()
    {
        await SetupAsync();
        await AdjustInAsync(quantity: 10m, unitCost: 100m);

        Assert.Equal(-1_000m, await LedgerAsync("4106"));
        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
        Assert.Equal(1_000m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
    }

    /// <summary>
    /// <b>والمعالجتان متماثلتان:</b> نقصٌ بألفٍ وفائضٌ بألفٍ ⟵ <b>أثرُهما على الربح صفر</b>.
    ///
    /// <para><b>وهذا هو القياسُ الذي كان يسقط</b>: الفائضُ كان يذهب حقوقَ ملكيّةٍ والنقصُ
    /// مصروفاً، <b>فيُعلَن خسارةُ ألفٍ عن جردٍ لم يخسر شيئاً</b>.</para>
    /// </summary>
    [Fact]
    public async Task AShortageAndAnEqualSurplus_NetToZeroInTheIncomeStatement()
    {
        await SetupAsync();
        await AdjustInAsync(quantity: 10m, unitCost: 100m);       // ‏فائضٌ بألف
        await AdjustOutAsync(quantity: 10m);                      // ‏ونقصٌ بألف

        var income = -await LedgerAsync("4106");                  // ‏الإيرادُ دائنٌ فيُقلَب
        var expense = await LedgerAsync("5215");

        Assert.Equal(1_000m, income);
        Assert.Equal(1_000m, expense);
        Assert.Equal(0m, income - expense);
        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — النقصُ لم يتغيّر:</b> ما زال مصروفاً على <c>5215</c>.
    /// <b>فالإصلاحُ مسّ طرفاً واحداً</b>، ولو مسّ الطرفين لانقلب العطلُ ولم يُغلق.
    /// </summary>
    [Fact]
    public async Task AShortage_IsStillAnExpense()
    {
        await SetupAsync();
        await AdjustInAsync(quantity: 10m, unitCost: 100m);
        await AdjustOutAsync(quantity: 4m);

        Assert.Equal(400m, await LedgerAsync("5215"));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — الرصيدُ الافتتاحيُّ يبقى حقوقَ ملكية.</b>
    ///
    /// <para><b>وهو الفرقُ الذي يجعل الإصلاحَ صواباً لا تعميماً</b>: بضاعةٌ تدخل يومَ
    /// التأسيس <b>ليست ربحاً كُسب في فترة</b> — فلو سِيقت إلى الإيراد <b>لأُعلن ربحٌ
    /// بقيمة المخزون الافتتاحيّ كلِّه</b>، وهو عطلٌ أكبرُ ممّا أُصلح.</para>
    /// </summary>
    [Fact]
    public async Task AnOpeningBalance_RemainsEquity_NotIncome()
    {
        await SetupAsync();
        await OpeningAsync(quantity: 20m, unitCost: 50m);

        Assert.Equal(-1_000m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
        Assert.Equal(0m, await LedgerAsync("4106"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر الجرد", "310123456700077", BusinessProfile.Retail);
        _companyId = company.Id;

        var product = Product.Create(_companyId, "P-C", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _productId = product.Id;
        _warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
    }

    private Task AdjustInAsync(decimal quantity, decimal unitCost) =>
        PostAsync(StockMovementType.AdjustmentIn, quantity, unitCost);

    private Task AdjustOutAsync(decimal quantity) =>
        PostAsync(StockMovementType.AdjustmentOut, quantity, 0m);

    private Task OpeningAsync(decimal quantity, decimal unitCost) =>
        PostAsync(StockMovementType.Opening, quantity, unitCost);

    private async Task PostAsync(StockMovementType type, decimal quantity, decimal unitCost)
    {
        var movement = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 8, 1), type, _warehouseId, $"جرد {type}");
        movement.AddLine(_productId, quantity, unitCost);
        await new StockMovementPostingService(_db).PostAsync(movement);
    }

    /// <summary>رصيدٌ مُوقَّع: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
