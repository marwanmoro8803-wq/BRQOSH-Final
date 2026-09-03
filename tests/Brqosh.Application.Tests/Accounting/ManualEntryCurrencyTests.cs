using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-231 · O-34) <b>العملةُ على القيد اليدويّ — مذكرةٌ على السطر، والدفترُ بالريال.</b>
///
/// <para><b>ما كان:</b> كلُّ مستندٍ في النطاق يحمل <c>SetCurrency</c>، <b>والقيدُ اليدويّ
/// وحدَه لا يحمل شيئاً</b>. فمَن قيّد تحويلاً بالدولار كتب الريالَ بيده — <b>وضاع أصلُ
/// المعاملة وسعرُ ترجمتها</b>، فاستحال بعد شهرٍ أن يُعرف أنّ الألفَ كانت مئتي دولارٍ
/// بسعر ٣٫٧٥. <b>ولا يُراجَع سعرٌ لا أثرَ له.</b></para>
///
/// <para><b>والقرارُ المعماريّ يُكتب لا يُستنتج:</b> <b>لا <c>SetCurrency</c> على رأس
/// القيد</b>. فرأسٌ بعملةٍ يعني ميزاناً بعملتين، <b>ودفترُ الأستاذ النظاميُّ بالريال</b>
/// — والقيدُ سطورٌ قد تختلف مصادرُها. فالعملةُ <b>مذكرةٌ على السطر</b>: بأيّ عملةٍ وقعت،
/// وبأيّ سعرٍ تُرجمت، <b>والمبلغُ المُرحَّلُ ريالٌ دائماً</b>.</para>
///
/// <para><b>وهويّةُ هـ-٢٥:</b> <c>round(ForeignAmount × ExchangeRate, 2) = Debit + Credit</c>.
/// <b>وكسرُها ليس خطأً حسابياً بل توثيقٌ كاذب</b>: سطرٌ يُقرأ بعد سنةٍ فيُظنّ خطأً في
/// الترجمة وهو خطأٌ في المذكرة — <b>أو العكس، وهو أسوأ</b>.</para>
/// </summary>
public class ManualEntryCurrencyTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private static readonly DateOnly EntryDate = new(2026, 3, 10);

    public ManualEntryCurrencyTests()
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
    /// <b>القياسُ الحاكم: يُرحَّل بالريال، وتبقى المذكرةُ بعد الحفظ والقراءة.</b>
    ///
    /// <para><b>والقراءةُ من قاعدةٍ ثانيةٍ عمداً</b>: مذكرةٌ تعيش في الذاكرة ولا عمودَ
    /// لها <b>تمرّ في اختبارٍ يقرأ الكيانَ المتعقَّب</b> — وتضيع عند أوّل إقلاع.</para>
    /// </summary>
    [Fact]
    public async Task AForeignCurrencyLine_PostsInRiyal_AndKeepsItsMemo()
    {
        await SetupAsync();

        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var bank = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var entry = JournalEntry.CreateDraft(_companyId, EntryDate, "تحويلٌ بالدولار");

        // ‏مئتا دولارٍ بسعر ٣٫٧٥ = ٧٥٠ ريالاً.
        entry.AddLine(cash, 750m, 0m, "قبضٌ نقديّ", null, "USD", 3.75m, 200m);
        entry.AddLine(bank, 0m, 750m, "سحبٌ من البنك", null, "USD", 3.75m, 200m);

        entry.Post("JE-CUR-1");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        var stored = await _db.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.EntryNumber == "JE-CUR-1");

        Assert.Equal(750m, stored.TotalDebit);
        Assert.Equal(750m, stored.TotalCredit);

        Assert.All(stored.Lines, l =>
        {
            Assert.Equal("USD", l.CurrencyCode);
            Assert.Equal(3.75m, l.ExchangeRate);
            Assert.Equal(200m, l.ForeignAmount);
        });
    }

    /// <summary>
    /// <b>ومذكرةٌ تخالف المُرحَّل تُرفض — هـ-٢٥.</b>
    ///
    /// <para><b>وهي أخطرُ من غياب المذكرة</b>: الغيابُ يُقرأ «لا نعلم»، <b>والمخالفةُ
    /// تُقرأ «نعلم» وهي كاذبة</b>.</para>
    /// </summary>
    [Fact]
    public async Task AMemoThatContradictsThePostedAmount_IsRefused()
    {
        await SetupAsync();
        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);

        var entry = JournalEntry.CreateDraft(_companyId, EntryDate, "مذكرةٌ مخالفة");

        // ‏٢٠٠ × ٣٫٧٥ = ٧٥٠ — والمُرحَّلُ ٧٠٠.
        var ex = Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 700m, 0m, null, null, "USD", 3.75m, 200m));

        Assert.Contains("750", ex.Message);
    }

    /// <summary>
    /// <b>ونصفُ مذكرةٍ مرفوضٌ كذلك.</b>
    ///
    /// <para>عملةٌ بلا سعرٍ <b>لا تُترجم</b>، وسعرٌ بلا عملةٍ <b>لا يعني شيئاً</b>،
    /// ومبلغٌ أجنبيٌّ بلا عملةٍ <b>رقمٌ بلا وحدة</b>.</para>
    /// </summary>
    [Fact]
    public async Task AHalfWrittenMemo_IsRefused()
    {
        await SetupAsync();
        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var entry = JournalEntry.CreateDraft(_companyId, EntryDate, "نصفُ مذكرة");

        Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 750m, 0m, null, null, "USD"));

        Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 750m, 0m, null, null, null, 3.75m));

        Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 750m, 0m, null, null, null, null, 200m));

        Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 750m, 0m, null, null, "USD", 0m, 200m));

        // ‏**وهاتان أخرجتهما طفرتان نجتا.** نُزع فحصُ السعر فمرّ الاختبار، ونُزع فحصُ
        // ‏المبلغ فمرّ كذلك — **لأنّ كلَّ حالةٍ كانت يُمسكها فحصٌ آخرُ قبله**. فحارسٌ
        // ‏يقيس أربعَ حالاتٍ مجتمعةً لا يقيس أربعةَ حرّاس، **ولا يُعرف أيُّها ميّت**.
        //
        // ‏فتُعزل كلُّ حالةٍ إلى ما ينقصها وحدَه: عملةٌ ومبلغٌ **بلا سعر**، وعملةٌ
        // ‏وسعرٌ **بلا مبلغ**. وبغيرِ فحصهما تُقرأ `Nullable.Value` على فراغٍ
        // ‏فيُرمى استثناءٌ ليس `AccountingException` — **ورسالةٌ لا يفهمها محاسب**.
        Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 750m, 0m, null, null, "USD", null, 200m));

        Assert.Throws<AccountingException>(() =>
            entry.AddLine(cash, 750m, 0m, null, null, "USD", 3.75m, null));

        Assert.Empty(entry.Lines);
    }

    /// <summary>
    /// <b>والسطرُ الرياليُّ يبقى بلا مذكرة — فالإضافةُ لا تُغيّر قيداً قائماً.</b>
    ///
    /// <para><b>وافتراضُ «SAR» على القديم كذبٌ على الماضي</b>: يجعل قيوداً رُحّلت بلا
    /// مذكرةٍ تبدو موثَّقةً بعملةٍ لم يُدخلها أحد.</para>
    /// </summary>
    [Fact]
    public async Task ARiyalLine_CarriesNoMemo()
    {
        await SetupAsync();

        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var bank = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var entry = JournalEntry.CreateDraft(_companyId, EntryDate, "قيدٌ رياليّ");
        entry.AddLine(cash, 100m, 0m);
        entry.AddLine(bank, 0m, 100m);
        entry.Post("JE-CUR-2");
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        var stored = await _db.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.EntryNumber == "JE-CUR-2");

        Assert.All(stored.Lines, l =>
        {
            Assert.Null(l.CurrencyCode);
            Assert.Null(l.ExchangeRate);
            Assert.Null(l.ForeignAmount);
        });
    }

    /// <summary>
    /// <b>والقيدُ يبقى متوازناً بالريال ولو اختلفت عملاتُ سطوره.</b>
    ///
    /// <para><b>وهذا هو سببُ كون المذكرة على السطر لا على الرأس</b>: تحويلٌ من حسابٍ
    /// دولاريٍّ إلى حسابٍ يوروِّي <b>سطراه بعملتين وميزانُه واحد</b> — ورأسٌ بعملةٍ
    /// واحدةٍ يُحيل هذا القيدَ مستحيلاً.</para>
    /// </summary>
    [Fact]
    public async Task TwoCurrenciesInOneEntry_StillBalanceInRiyal()
    {
        await SetupAsync();

        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var bank = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var entry = JournalEntry.CreateDraft(_companyId, EntryDate, "من دولارٍ إلى يورو");

        entry.AddLine(cash, 750m, 0m, null, null, "USD", 3.75m, 200m);
        entry.AddLine(bank, 0m, 750m, null, null, "EUR", 4.0m, 187.5m);

        entry.Post("JE-CUR-3");

        Assert.True(entry.IsBalanced);
        Assert.Equal("USD", entry.Lines[0].CurrencyCode);
        Assert.Equal("EUR", entry.Lines[1].CurrencyCode);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Account> AccountAsync(string code) =>
        await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة العملات", "310123456700166", BusinessProfile.General);
        _companyId = company.Id;
    }
}
