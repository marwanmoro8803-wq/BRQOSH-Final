using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Retail;

/// <summary>
/// (CEEF-232 · ن-١) <b>فرقُ نقدِ الوردية — كان يُحسب ويُعرض ولا يُقيَّد.</b>
///
/// <para><b>ما كان:</b> <c>PosShift</c> يحسب <c>ExpectedCash</c> و<c>CashDifference</c>،
/// <b>والشاشةُ تعرضهما وتُجمّع «ورديات بعجز» في مؤشّر</b> — ثمّ يُغلق الزرُّ الورديةَ
/// ويكتب <b>سجلَّ تدقيقٍ فقط</b>. فلا قيدَ ولا حساب.</para>
///
/// <para><b>والأثرُ دائمٌ لا عابر:</b> المبيعاتُ النقدية تُقيَّد على <c>1101</c> بالكامل،
/// <b>فرصيدُ الصندوق في الأستاذ هو المتوقَّع لا المعدود</b>. وكلُّ عجزٍ يُترك <b>يُباعد
/// بين الدفتر والدرج إلى الأبد</b> — ولا يُجدي جردٌ بعده إذ لا يُعرف أيُّ ورديّةٍ أحدثت
/// الفارق. <b>وهو بابُ اختلاسٍ لا يترك أثراً محاسبياً</b>: مَن يأخذ مئةً كلَّ يومٍ لا
/// يظهر في أيّ قائمة.</para>
///
/// <para><b>وهويّةُ ن-١:</b> بعد إغلاق الوردية وتقييد فرقها:
/// <c>رصيدُ 1101 = النقدُ المعدود</c> — لا المتوقَّع.</para>
/// </summary>
public class PosShiftVarianceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _productId;

    public PosShiftVarianceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// <b>القياسُ الحاكم: العجزُ يُنزل الدفترَ إلى المعدود ويُحمَّل مصروفاً.</b>
    ///
    /// <para>مبيعاتٌ نقديةٌ بـ١١٥ والمعدودُ ٦٥ — <b>فالعجزُ خمسون</b>.
    /// <b>والأستاذُ كان يقول ١١٥ إلى الأبد</b>.</para>
    ///
    /// <para><b>والافتتاحُ صفرٌ هنا عمداً:</b> نقدُ الدرج وخزنةُ المنشأة <b>حسابٌ واحد</b>
    /// (<c>1101</c>)، فنقلُ عهدةٍ بينهما لا يُحرّك الرصيد. <b>وقاعدةُ اختبارٍ بلا أرصدةٍ
    /// افتتاحيّة لا تحمل تلك العهدة أصلاً</b> — فإدخالُها يُقارن رقمين بمرجعين.</para>
    /// </summary>
    [Fact]
    public async Task AShortage_BringsTheLedgerDownToTheCountedCash()
    {
        await SetupAsync();
        var shift = await SellThenCloseAsync(openingCash: 0m, counted: 65m);

        var result = await new PosShiftVariancePostingService(_db)
            .PostForShiftAsync(_companyId, shift.Id, "admin");

        Assert.Equal(-50m, result.Difference);
        Assert.NotNull(result.JournalEntryId);

        // ‏**هويّةُ ن-١** — والرقمُ يُقرأ من الأستاذ لا يُكتب حرفاً.
        Assert.Equal(65m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));

        // ‏**والعجزُ مصروفٌ** — لا يُبتلع في النقد ولا يُقاصّ.
        Assert.Equal(50m, await BalanceAsync("5215"));
    }

    /// <summary>
    /// <b>والزيادةُ إيرادٌ — ولا تُترك في الدرج بلا قيد.</b>
    ///
    /// <para><b>والاتجاهُ هنا لا يشتكي منه أحد</b>: الدرجُ أكثرُ من الدفتر، فيبدو الأمرُ
    /// خيراً — <b>وهو خللٌ في العدّ أو في التسعير</b>، وتركُه بلا قيدٍ يجعل الصندوقَ
    /// المعدودَ لا يُطابق أبداً.</para>
    /// </summary>
    [Fact]
    public async Task AnOverage_IsIncome_AndTheLedgerRisesToTheCountedCash()
    {
        await SetupAsync();
        var shift = await SellThenCloseAsync(openingCash: 0m, counted: 135m);

        var result = await new PosShiftVariancePostingService(_db)
            .PostForShiftAsync(_companyId, shift.Id, "admin");

        Assert.Equal(20m, result.Difference);
        Assert.Equal(135m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(-20m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.OtherIncome));
    }

    /// <summary>
    /// <b>وورديةٌ مطابقةٌ لا تُنشئ قيداً — والصفرُ ليس حدثاً.</b>
    ///
    /// <para><b>وقيدٌ بلا مبلغٍ يملأ الأستاذ بضجيجٍ يُخفي الفروقَ الحقيقيّة</b> — وأكثرُ
    /// الوردياتِ تُغلق مطابقةً بحقّ.</para>
    /// </summary>
    [Fact]
    public async Task AMatchingShift_PostsNothing()
    {
        await SetupAsync();
        var shift = await SellThenCloseAsync(openingCash: 0m, counted: 115m);

        var result = await new PosShiftVariancePostingService(_db)
            .PostForShiftAsync(_companyId, shift.Id, "admin");

        Assert.Equal(0m, result.Difference);
        Assert.Null(result.JournalEntryId);
        Assert.Equal(115m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary>
    /// <b>ولا يُقيَّد الفرقُ مرّتين، ولا يُقيَّد لوردية مفتوحة.</b>
    ///
    /// <para><b>والثانيةُ أخطرُ ممّا تبدو</b>: وردةٌ مفتوحةٌ لم يقع عدُّها بعد،
    /// <c>ClosingCash</c> فيها صفر — <b>فـ«الفرق» هو المتوقَّعُ كلُّه سالباً</b>،
    /// فيُقيَّد عجزٌ بقيمة نقدِ الدرج كلِّه.</para>
    /// </summary>
    [Fact]
    public async Task Posting_RefusesAnOpenShift_AndRefusesToRepeat()
    {
        await SetupAsync();
        var service = new PosShiftVariancePostingService(_db);

        var open = PosShift.Open(_companyId, "كاشير", 1_000m);
        _db.PosShifts.Add(open);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.PostForShiftAsync(_companyId, open.Id, "admin"));

        var closed = await SellThenCloseAsync(openingCash: 0m, counted: 65m);
        await service.PostForShiftAsync(_companyId, closed.Id, "admin");

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.PostForShiftAsync(_companyId, closed.Id, "admin"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Service, salePrice: 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _productId = product.Id;
    }

    /// <summary>وردية: بيعٌ نقديٌّ بمئةٍ وضريبتِها (١١٥) ثمّ إغلاقٌ بالمعدود.</summary>
    private async Task<PosShift> SellThenCloseAsync(decimal openingCash, decimal counted)
    {
        var shift = PosShift.Open(_companyId, "كاشير", openingCash);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 1m)],
            isVatRegistered: true, PosPaymentMethod.Cash, "كاشير");

        var stored = await _db.PosShifts.SingleAsync(s => s.Id == shift.Id);
        stored.Close(counted);
        await _db.SaveChangesAsync();
        return stored;
    }

    /// <summary>رصيدُ حسابٍ من القيود المرحَّلة — <b>مدين ناقص دائن</b>.</summary>
    private async Task<decimal> BalanceAsync(string code)
    {
        var accountId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == code)
            .Select(a => a.Id).SingleAsync();

        return await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == accountId)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0m;
    }
}
