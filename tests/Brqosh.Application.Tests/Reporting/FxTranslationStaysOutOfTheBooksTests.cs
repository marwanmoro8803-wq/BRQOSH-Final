using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤٦ · <c>accounting-core#4</c>) <b>فرقُ الترجمة عرضٌ لا قيد — لا
/// يدخل دفاترَ الشركة التابعة.</b>
///
/// <para><b>ما كان:</b> <c>FxClosingTranslationService</c> يحسب
/// <c>صافي الدخل × (سعر الإقفال − المتوسّط)</c> — <b>مبلغاً بعملة العرض</b> — ويُرحّله قيداً
/// <b>بعملة الشركة الوظيفيّة في دفاترها هي</b>: مدين 4107 «أرباح فروق عملة» / دائن 3106.
/// <b>ومعيارُ المحاسبة الدوليّ 21 يجعل فروقَ الترجمة في القوائم المُترجَمة وحدَها</b>، في
/// الدخل الشامل الآخر — <b>لا ربحاً ولا خسارةً في دفاتر المنشأة أبداً</b>.</para>
///
/// <para><b>وثلاثةُ أضرار:</b> قائمةُ دخل التابعة تتغيّر بفرقٍ لا وجودَ له بعملتها؛ <b>والقيدُ
/// مقلوبٌ</b> — ربحُ الترجمة <b>يُدين</b> حسابَ الأرباح فيُنقص الإيراد؛ <b>والتوحيدُ يُطبّقه
/// مرّتين</b>: الموحَّدُ يشتقّ الترجمةَ موازِناً (ح-٤٥) <b>وقائمةُ دخله تحمل البندَ المزيّف</b>.</para>
/// </summary>
public class FxTranslationStaysOutOfTheBooksTests : IDisposable
{
    private static readonly DateOnly July1 = new(2026, 7, 1);
    private static readonly DateOnly July31 = new(2026, 7, 31);

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly ExchangeRateService _rates;

    public FxTranslationStaysOutOfTheBooksTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _rates = new ExchangeRateService(_db);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> قائمةُ دخل الشركة التابعة بعملتها لا تتغيّر بتشغيل الترجمة.
    /// </summary>
    [Fact]
    public async Task TheSubsidiaryIncomeStatement_IsUntouched()
    {
        var (_, companyId) = await SeedAsync();
        var before = (await new IncomeStatementQuery(_db).ExecuteAsync(companyId, July1, July31)).NetIncome;

        await TranslateAsync(companyId);

        var after = (await new IncomeStatementQuery(_db).ExecuteAsync(companyId, July1, July31)).NetIncome;
        Assert.Equal(100m, before);
        Assert.Equal(before, after);
    }

    /// <summary><b>ولا قيدَ يُرحَّل في دفاتر التابعة أصلاً.</b></summary>
    [Fact]
    public async Task NoJournalEntry_IsPosted()
    {
        var (_, companyId) = await SeedAsync();

        await TranslateAsync(companyId);

        Assert.False(await _db.JournalEntries.AnyAsync(e =>
            e.CompanyId == companyId && e.SourceDocument != null
            && e.SourceDocument.StartsWith("FxClosingTranslation")));
    }

    /// <summary>
    /// <b>وقائمةُ الدخل الموحَّدة = دخلُ التابعة بمتوسّط الفترة وحدَه</b> — وكانت تحمل البندَ
    /// المزيّف فوقه، فتُطبَّق الترجمةُ مرّتين.
    /// </summary>
    [Fact]
    public async Task TheConsolidatedIncome_IsTheTranslatedIncomeOnly()
    {
        var (groupId, companyId) = await SeedAsync();
        var average = await _rates.GetAverageRateAsync(companyId, "USD", "SAR", July1, July31);

        await TranslateAsync(companyId);

        var consolidated = await new ConsolidatedIncomeStatementQuery(_db, _rates)
            .ExecuteAsync(groupId, July1, July31);
        Assert.Equal(Math.Round(100m * average, 2, MidpointRounding.AwayFromZero), consolidated.NetIncome);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>يُستدعى ولا يُقرأ ناتجُه</b> — كي يُترجَم الاختبارُ على الكود قبل الإصلاح وبعده معاً،
    /// فيُقاس العطلُ على الواجهة القديمة ذاتها.
    /// </summary>
    private async Task TranslateAsync(long companyId) =>
        await new FxClosingTranslationService(_db, _rates).TranslatePeriodAsync(companyId, July1, July31, "admin");

    /// <summary>
    /// تابعةٌ بالدولار في مجموعةٍ بالريال، بيعٌ نقديٌّ ١٠٠ في يوليو، وأسعارٌ متوسّطُها غيرُ الإقفال:
    /// ٣٫٧٠ و٣٫٧٠ و٣٫٧٥ ⟵ فرقُ ترجمةٍ غيرُ صفريّ.
    /// </summary>
    private async Task<(long GroupId, long CompanyId)> SeedAsync()
    {
        var group = CompanyGroup.Create("GRP-H46", "مجموعة", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة دولار", "310123456700010", BusinessProfile.General);
        company.AssignCompanyGroup(group.Id);
        company.SetCurrencies("USD", "USD");
        await _db.SaveChangesAsync();

        await _rates.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 1), 3.70m);
        await _rates.RecordRateAsync(company.Id, "USD", "SAR", new DateOnly(2026, 7, 15), 3.70m);
        await _rates.RecordRateAsync(company.Id, "USD", "SAR", July31, 3.75m);

        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "4101");
        var entry = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 7, 5), "بيع");
        entry.AddLine(cash, 100m, 0m);
        entry.AddLine(sales, 0m, 100m);
        entry.Post("JE-H46");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        return (group.Id, company.Id);
    }
}
