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
/// (جاهزيّةُ الإطلاق · ح-٤٥ · <c>accounting-core#5</c>) <b>المركزُ الموحَّدُ يحمل أرباحَ
/// السنوات غير المقفلة أرباحاً — لا «احتياطيَ ترجمة».</b>
///
/// <para><b>ما كان:</b> حقوقُ كلّ شركةٍ تُجمع <b>بلا 3999</b> (الأرباحُ غير المقفلة منذ التأسيس)،
/// ثمّ يُضاف <b>ربحُ السنة التقويميّة الجارية وحدَها</b>، <b>والفرقُ كلُّه يُرحَّل إلى 3106
/// «احتياطي ترجمة العملات»</b>. فمجموعةٌ بالريال كلُّها — لا ترجمةَ فيها أصلاً — تُعرض ربحَ
/// السنة الماضية غيرِ المقفلة <b>احتياطيَ ترجمة</b>. وهو عينُ ما أصلحه CEEF-201 (ع-R1) في
/// المركز المفرد <b>ولم يبلغ الموحَّد</b>.</para>
///
/// <para><b>والموازنةُ القسريّةُ تبتلع كلَّ خطأ توحيدٍ آخر</b> فيقول التقرير «متوازن» دائماً.</para>
/// </summary>
public class ConsolidatedEquityCarriesPriorYearsTests : IDisposable
{
    private static readonly DateOnly AsOf = new(2026, 6, 30);

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly ExchangeRateService _rates;
    private int _entrySeq;

    public ConsolidatedEquityCarriesPriorYearsTests()
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
    /// <b>القياسُ الحاكم — مسبارُ التدقيق S6 حرفاً:</b> شركةٌ بالريال في مجموعةٍ بالريال، إيرادُ
    /// ١٬٠٠٠ في ٢٠٢٥ بلا إقفالٍ و٢٠٠ في ٢٠٢٦. <b>وكان الموحَّدُ: 3106 = 1,000 · 3999 = 200.</b>
    /// </summary>
    [Fact]
    public async Task LastYearsUnclosedProfit_IsEarnings_NotATranslationReserve()
    {
        var (groupId, companyId) = await SeedGroupAsync("SAR");
        await PostAsync(companyId, new DateOnly(2025, 6, 1), "1101", "4101", 1_000m);
        await PostAsync(companyId, new DateOnly(2026, 3, 1), "1101", "4101", 200m);

        var result = await ConsolidateAsync(groupId);

        Assert.DoesNotContain(result.Equity, e => e.AccountCode == SaudiChartOfAccounts.SystemCodes.TranslationReserve);
        Assert.Equal(1_200m, result.Equity.Single(e => e.AccountCode == "3999").Amount);
        Assert.True(result.IsBalanced);
    }

    /// <summary>
    /// <b>والموحَّدُ لشركةٍ واحدةٍ بالريال == مركزُها المفرد</b> — والمفردُ صحيحٌ منذ CEEF-201.
    /// </summary>
    [Fact]
    public async Task ASingleRiyalCompany_ConsolidatesToItsOwnBalanceSheet()
    {
        var (groupId, companyId) = await SeedGroupAsync("SAR");
        await PostAsync(companyId, new DateOnly(2024, 9, 1), "1101", "4101", 300m);
        await PostAsync(companyId, new DateOnly(2025, 6, 1), "1101", "4101", 1_000m);
        await PostAsync(companyId, new DateOnly(2026, 3, 1), "1101", "4101", 200m);

        var single = await new BalanceSheetQuery(_db).ExecuteAsync(companyId, AsOf);
        var consolidated = await ConsolidateAsync(groupId);

        Assert.Equal(
            single.Equity.Select(e => (e.AccountCode, e.Amount)),
            consolidated.Equity.Select(e => (e.AccountCode, e.Amount)));
    }

    /// <summary>
    /// <b>والسنةُ السابقةُ غيرُ المقفلة لشركةٍ أجنبيّة تُترجَم بمتوسّط سنتِها</b> — لا تُهمل فتصير
    /// «ترجمةً». USD: ١٠٠ في ٢٠٢٥ (متوسّطُها ٣٫٧٠) و٥٠ في ٢٠٢٦ (٣٫٨٠، وهو سعرُ الإقفال):
    /// <c>الأرباح = 370 + 190 = 560</c> والنقدُ <c>150 × 3.80 = 570</c> <b>والترجمةُ الحقيقيّة ١٠</b>.
    /// وكان: الأرباح ١٩٠ والترجمة ٣٨٠.
    /// </summary>
    [Fact]
    public async Task APriorUnclosedYear_IsTranslatedAtItsOwnAverage()
    {
        var (groupId, companyId) = await SeedGroupAsync("USD");
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2025, 6, 1), 3.70m);
        await _rates.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 6, 30), 3.80m);
        await PostAsync(companyId, new DateOnly(2025, 6, 1), "1101", "4101", 100m);
        await PostAsync(companyId, new DateOnly(2026, 3, 1), "1101", "4101", 50m);

        var result = await ConsolidateAsync(groupId);

        Assert.Equal(570m, result.Assets.Single(a => a.AccountCode == "1101").Amount);
        Assert.Equal(560m, result.Equity.Single(e => e.AccountCode == "3999").Amount);
        Assert.Equal(10m, result.Equity.Single(e =>
            e.AccountCode == SaudiChartOfAccounts.SystemCodes.TranslationReserve).Amount);
        Assert.True(result.IsBalanced);
    }

    /// <summary>
    /// <b>وفرقُ توحيدٍ لا ترجمةَ وراءه يُعلَن ولا يُدفن</b>: قيدٌ بين الشركتين سجّلته إحداهما
    /// وحدَها في مجموعةٍ بالريال. <b>وكانت الموازنةُ القسريّةُ تسمّيه «احتياطيَ ترجمة» وتقول
    /// «متوازن»</b> — فلا يراه أحد. والآن لا 3106، <b>والتقريرُ يقول «غير متوازن»</b>.
    /// </summary>
    [Fact]
    public async Task AnUnexplainedDifference_IsFlagged_NotCalledTranslation()
    {
        var (groupId, companyA) = await SeedGroupAsync("SAR");
        var companyB = await AddCompanyAsync(groupId, "SAR", additional: true);
        await PostAsync(companyA, new DateOnly(2026, 3, 1), "1101", "4101", 500m, intercompanyWith: companyB);

        var result = await ConsolidateAsync(groupId);

        Assert.DoesNotContain(result.Equity, e => e.AccountCode == SaudiChartOfAccounts.SystemCodes.TranslationReserve);
        Assert.False(result.IsBalanced);
    }

    /// <summary>
    /// <b>شاهدُ ضبط — سنةٌ سابقةٌ مُقفلةٌ تبقى في حسابها</b> (قيدُ الإقفال نقلها إلى حقوق الملكية)،
    /// وأرباحُ السنة الجارية وحدَها في 3999 — كما كانت قبل الإصلاح حرفاً.
    /// </summary>
    [Fact]
    public async Task AClosedPriorYear_StaysWhereTheCloseMovedIt()
    {
        var (groupId, companyId) = await SeedGroupAsync("SAR");
        await PostAsync(companyId, new DateOnly(2025, 6, 1), "1101", "4101", 1_000m);
        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");
        await PostAsync(companyId, new DateOnly(2026, 3, 1), "1101", "4101", 200m);

        var result = await ConsolidateAsync(groupId);

        Assert.DoesNotContain(result.Equity, e => e.AccountCode == SaudiChartOfAccounts.SystemCodes.TranslationReserve);
        Assert.Equal(200m, result.Equity.Single(e => e.AccountCode == "3999").Amount);
        Assert.Equal(1_200m, result.TotalEquity);
        Assert.True(result.IsBalanced);
    }

    /// <summary>
    /// <b>وسنةٌ أُقفلت في آخر يومها لا تُحسب مرّتين.</b> كان ربحُ «السنة الجارية» يُقرأ
    /// <b>باستثناء قيد الإقفال</b> (الافتراضيّ)، والحسابُ الذي نُقل إليه الربحُ مجموعٌ أيضاً —
    /// <b>فيتضاعف، ويدفن الموازنُ الضعفَ في «احتياطي ترجمة» بالسالب</b>.
    /// </summary>
    [Fact]
    public async Task AYearClosedOnItsLastDay_IsNotCountedTwice()
    {
        var (groupId, companyId) = await SeedGroupAsync("SAR");
        await PostAsync(companyId, new DateOnly(2025, 6, 1), "1101", "4101", 1_000m);
        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");

        var result = await new ConsolidatedBalanceSheetQuery(_db, _rates)
            .ExecuteAsync(groupId, new DateOnly(2025, 12, 31));

        Assert.DoesNotContain(result.Equity, e => e.AccountCode == SaudiChartOfAccounts.SystemCodes.TranslationReserve);
        Assert.DoesNotContain(result.Equity, e => e.AccountCode == "3999");
        Assert.Equal(1_000m, result.TotalEquity);
        Assert.True(result.IsBalanced);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<ConsolidatedBalanceSheetResult> ConsolidateAsync(long groupId) =>
        new ConsolidatedBalanceSheetQuery(_db, _rates).ExecuteAsync(groupId, AsOf);

    private async Task<(long GroupId, long CompanyId)> SeedGroupAsync(string functionalCurrency)
    {
        var group = CompanyGroup.Create("GRP-H45", "مجموعة", "SAR");
        _db.CompanyGroups.Add(group);
        await _db.SaveChangesAsync();
        return (group.Id, await AddCompanyAsync(group.Id, functionalCurrency));
    }

    /// <summary>
    /// <b>والشركةُ الثانيةُ بـ<c>CreateAdditionalCompanyAsync</c></b> — <c>InitializeAsync</c> ثانيةً
    /// <b>يُعيد الشركةَ الأولى نفسَها</b>، فتصير «الشركةُ المقابلة» هي الشركة، ويرمي القيدُ قبل أن
    /// يُقاس شيء. <b>وقع ذلك في أوّل قياسٍ لي فسُجّل «ساقطاً» وهو لم يقس العطلَ أصلاً.</b>
    /// </summary>
    private async Task<long> AddCompanyAsync(long groupId, string functionalCurrency, bool additional = false)
    {
        var initializer = new DatabaseInitializer(_db);
        var company = additional
            ? await initializer.CreateAdditionalCompanyAsync("شركة ب", "310123456700021", BusinessProfile.General)
            : await initializer.InitializeAsync("شركة أ", "310123456700010", BusinessProfile.General);
        company.AssignCompanyGroup(groupId);
        company.SetCurrencies(functionalCurrency, functionalCurrency);
        await _db.SaveChangesAsync();
        return company.Id;
    }

    private async Task PostAsync(long companyId, DateOnly on, string debitCode, string creditCode, decimal amount,
        long? intercompanyWith = null)
    {
        var debit = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == debitCode);
        var credit = await _db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == creditCode);

        var entry = JournalEntry.CreateDraft(companyId, on, "قيد");
        entry.AddLine(debit, amount, 0m);
        entry.AddLine(credit, 0m, amount);
        if (intercompanyWith is long counterparty)
            entry.MarkIntercompany(counterparty);
        entry.Post($"JE-H45-{++_entrySeq}");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
    }
}
