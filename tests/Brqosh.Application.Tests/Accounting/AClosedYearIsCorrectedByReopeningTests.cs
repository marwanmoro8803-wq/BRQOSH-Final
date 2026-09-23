using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٤٧ · <c>accounting-core#1</c>) <b>السنةُ المقفلةُ تُصحَّح بإعادة فتحها —
/// وكانت لا تُصحَّح أبداً.</b>
///
/// <para>‏<b>وقِيس قبل الإصلاح:</b> سنةٌ أُقفلت بربح ١٬٠٠٠، ثمّ فُتح مارس بالمسار المشروع ورُحّلت
/// فاتورةٌ فاتت بـ٥٠٠ — <b>فقُبلت</b>. ثمّ الإقفالُ ثانيةً ⟵ «السنة 2025 مقفلة مسبقاً»، <b>وعكسُ
/// قيد الإقفال ⟵ «مرتبط بمستند نظام»</b>. فبقي في الإيراد <b>٥٠٠ في سنةٍ مقفلة</b> والأرباحُ
/// المبقاةُ على ١٬٠٠٠ — <b>ولا سبيلَ إلى ردّه</b>: الحارسُ يسأل <c>Status != Draft</c> والمعكوسُ
/// ليس مسودّة (عينُ عطل ح-٣٨)، ولا مسارَ عكسٍ أصلاً.</para>
///
/// <para>‏<b>والدعوى في MASTER §2ب</b> («الإقفالُ السنويُّ وعكسُه ✅ مُثبَتٌ آلياً» — <c>accounting-core#2</c>)
/// <b>كانت تسبق وجودَ العكس</b>: الموضعُ الوحيدُ في الاختبارات يبني قيداً عكسيّاً بيده من النطاق
/// (<c>EntityLevelClosingTests</c>) — لا خدمةَ تفعلُه ولا شاشة.</para>
/// </summary>
public class AClosedYearIsCorrectedByReopeningTests : IDisposable
{
    private const int Year = 2025;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public AClosedYearIsCorrectedByReopeningTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    /// <summary>‏الرحلةُ كاملة — وهي ما كان مستحيلاً.</summary>
    [Fact]
    public async Task AClosedYear_IsReopenedCorrectedAndClosedAgain()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        await CloseAsync();

        await Service().ReopenYearAsync(_companyId, Year, "فاتورةُ مارس فاتت", "admin");
        await ReopenMonthAsync(3);
        await SellAsync(new DateOnly(Year, 3, 10), 500m);

        var again = await CloseAsync();

        Assert.Equal(1_500m, again.NetIncome);
        Assert.Equal(1_500m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
        Assert.Equal(0m, await NetCreditAsync("4101"));
        Assert.Equal(0m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.CurrentYearProfit));
    }

    /// <summary>
    /// ‏<b>وإعادةُ الفتح تُعيد الربحَ إلى قائمة الدخل حتى يُقفل ثانيةً</b> — بقيدٍ عكسيٍّ مؤرّخٍ
    /// بآخر يومٍ في السنة، على مستوى المنشأة كأصله، <b>والأصلُ يبقى في الدفتر معكوساً لا محذوفاً</b>.
    /// </summary>
    [Fact]
    public async Task ReopeningAYear_PutsItsProfitBack_UntilItIsClosedAgain()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        var close = await CloseAsync();

        var result = await Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin");

        Assert.Equal(0m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
        Assert.Equal(1_000m, await NetCreditAsync("4101"));

        var original = await _db.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == close.ClosingEntryId);
        Assert.Equal(JournalEntryStatus.Reversed, original.Status);
        Assert.Equal([close.ClosingEntryId], result.ReversedEntryIds);

        var reversal = await _db.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == original.ReversalEntryId);
        Assert.Equal(new DateOnly(Year, 12, 31), reversal.EntryDate);
        Assert.True(reversal.IsEntityLevel, "عكسُ الإقفال وُسم بفرع — والأصلُ على مستوى المنشأة (O-32).");
        Assert.Equal([reversal.EntryNumber!], result.ReversalEntryNumbers);
    }

    /// <summary>
    /// ‏<b>ويُفتح ديسمبر وحدَه</b> — موضعُ قيد الإقفال وعكسِه. <b>وبقيّةُ الأشهر تبقى مقفلةً</b> حتى
    /// يُفتح منها ما يُصحَّح بعينه: إعادةُ فتح سنةٍ لا تفتح اثنَي عشرَ شهراً اعتمدها المراجعُ صامتة.
    /// </summary>
    [Fact]
    public async Task AReopenedYear_OpensDecemberOnly()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        await CloseAsync();

        await Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin");

        var march = await Assert.ThrowsAsync<AccountingException>(() =>
            SellAsync(new DateOnly(Year, 3, 10), 500m));
        Assert.Contains($"{Year}-03", march.Message);

        // ‏الفاتورةُ المرفوضةُ بقيت متعقَّبةً في السياق المشترَك — **ولو بقيت لأدرجها الحفظُ التالي**
        // ‏فيقيس شاهدُ الضبط شيئاً آخر.
        _db.ChangeTracker.Clear();

        // ‏شاهدُ الضبط: ديسمبر يقبل.
        await SellAsync(new DateOnly(Year, 12, 20), 200m);
    }

    /// <summary>
    /// ‏<b>وسنةٌ لاحقةٌ مقفلةٌ تُفتح أوّلاً</b> — فأرباحُ السنة المُصحَّحة رصيدُ افتتاح التي بعدها،
    /// وتلك اعتُمدت عليه. <b>والترتيبُ المعكوسُ يعمل</b> (شاهدُ ضبط).
    /// </summary>
    [Fact]
    public async Task ALaterClosedYear_MustBeReopenedFirst()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        await SellAsync(new DateOnly(Year + 1, 6, 15), 700m);
        await CloseAsync(Year);
        await CloseAsync(Year + 1);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin"));
        Assert.Contains((Year + 1).ToString(), ex.Message);
        Assert.Equal(0, await ReversedClosesAsync());

        await Service().ReopenYearAsync(_companyId, Year + 1, "تصحيح", "admin");
        await Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin");
        Assert.Equal(2, await ReversedClosesAsync());
    }

    [Fact]
    public async Task AYearThatIsNotClosed_IsNotReopened()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        var before = await _db.JournalEntries.CountAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin"));

        Assert.Contains("غير مقفلة", ex.Message);
        Assert.Equal(before, await _db.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task AReopenNeedsAReason()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        var close = await CloseAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            Service().ReopenYearAsync(_companyId, Year, "   ", "admin"));

        Assert.Equal(JournalEntryStatus.Posted,
            (await _db.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == close.ClosingEntryId)).Status);
    }

    /// <summary>
    /// ‏<b>ومديرُ النظام وحدَه — كالإقفال نفسِه.</b> والعكسُ الداخليُّ يقبل المحاسبَ
    /// (<c>EnsureCanCancelOrReverse</c>)، <b>فلو غاب الحارسُ هنا لأعاد المحاسبُ فتحَ سنةٍ لا يملك إقفالَها</b>.
    /// </summary>
    [Fact]
    public async Task OnlyAnAdministrator_ReopensAYear()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        var close = await CloseAsync();

        SecurityContext.CurrentRole = UserRole.Accountant;
        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin"));
        SecurityContext.CurrentRole = UserRole.Administrator;

        Assert.Contains("مدير النظام", ex.Message);
        Assert.Equal(JournalEntryStatus.Posted,
            (await _db.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == close.ClosingEntryId)).Status);
    }

    [Fact]
    public async Task TheReopen_IsAuditedWithItsReason()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        await CloseAsync();

        await Service().ReopenYearAsync(_companyId, Year, "فاتورةُ مارس فاتت", "admin");

        var audit = await _db.AuditLogEntries.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.Action == "إعادة فتح سنة مالية");
        Assert.Equal(Year.ToString(), audit.EntityKey);
        Assert.Equal("فاتورةُ مارس فاتت", audit.Reason);
    }

    /// <summary>
    /// ‏<b>وسنةٌ أُقفلت بنسخةٍ سابقةٍ لـح-١ تنقصها صفوفُ أشهرها الساكنة</b> — فلا يُفتح شهرٌ بلا صفٍّ
    /// ولا يُرحَّل فيه (حارسُ الشهر الساكن يرى قيدَ الإقفال المعكوس). <b>فتُنشأ الصفوفُ الغائبةُ
    /// مقفلةً عند إعادة الفتح</b> — وهو الردمُ الذي سمّاه تعليقُ <c>CloseAllYearPeriodsAsync</c> بنداً مستقلّاً.
    /// </summary>
    [Fact]
    public async Task AYearClosedByAnOlderVersion_GetsItsMissingMonths()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        await CloseAsync();

        // ‏محاكاةُ الإقفال القديم: لا صفَّ إلّا لما رُحّل فيه وللإقفال.
        _db.FiscalPeriods.RemoveRange(await _db.FiscalPeriods
            .Where(p => p.CompanyId == _companyId && p.StartDate.Year == Year
                        && p.StartDate.Month != 6 && p.StartDate.Month != 12)
            .ToListAsync());
        await _db.SaveChangesAsync();

        await Service().ReopenYearAsync(_companyId, Year, "تصحيح", "admin");

        var periods = await _db.FiscalPeriods.AsNoTracking()
            .Where(p => p.CompanyId == _companyId && p.StartDate.Year == Year)
            .ToListAsync();
        Assert.Equal(12, periods.Count);
        Assert.All(periods.Where(p => p.StartDate.Month != 12),
            p => Assert.Equal(FiscalPeriodStatus.Closed, p.Status));

        await ReopenMonthAsync(3);
        await SellAsync(new DateOnly(Year, 3, 10), 500m);
    }

    /// <summary>
    /// ‏<b>والسنةُ الساكنةُ المقفلة</b> (فتراتٌ بلا قيد) <b>تُفتح كذلك</b> — لا قيدَ يُعكس، وديسمبر يُفتح.
    /// </summary>
    [Fact]
    public async Task ADormantClosedYear_Reopens()
    {
        await SetupAsync();
        var dormant = await CloseAsync(Year - 1);
        Assert.Equal(0, dormant.ClosingEntryId);

        var result = await Service().ReopenYearAsync(_companyId, Year - 1, "تصحيح", "admin");

        Assert.Empty(result.ReversedEntryIds);
        Assert.Equal(FiscalPeriodStatus.Open, (await _db.FiscalPeriods.AsNoTracking()
            .SingleAsync(p => p.CompanyId == _companyId && p.Code == $"{Year - 1}-12")).Status);
        await CloseAsync(Year - 1);
    }

    /// <summary>
    /// ‏<b>وقاعدةٌ من قبل CEEF-229 قد تحمل إقفالين قائمين للسنة نفسِها</b> — سباقُ الإقفالين قِيس
    /// يومَها ثلاثةَ قيودٍ من ستّة إقفالاتٍ متزامنة، <b>فتتضاعف الأرباحُ المبقاةُ صامتة</b>.
    /// <b>وإعادةُ الفتح تعكسها كلَّها والإقفالُ بعدها واحد</b> — فهي طريقُ إصلاح ذلك الأثر أيضاً،
    /// <b>ولو سألت عن قيدٍ واحد</b> (<c>SingleOrDefault</c>) <b>لرمت خاماً على القاعدة المعطوبة
    /// بعينها</b>، أي حيث يُحتاج إليها.
    /// </summary>
    [Fact]
    public async Task ADuplicatedCloseFromTheOldRace_IsReversedWhole()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        var close = await CloseAsync();
        await DuplicateAsync(close.ClosingEntryId);
        Assert.Equal(2_000m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));

        var result = await Service().ReopenYearAsync(_companyId, Year, "إقفالٌ مكرّر", "admin");

        Assert.Equal(2, result.ReversedEntryIds.Count);
        Assert.Equal(0m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));

        await CloseAsync();
        Assert.Equal(1_000m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
    }

    /// <summary>
    /// ‏<b>وإعادةُ الفتح الثانية تعكس الإقفالَ الثاني لا الأوّل</b> — وقيدُ العكس يرث وسمَ
    /// <c>YearEndClose:</c> فيبقى <c>Posted</c> تحت المفتاح نفسِه. <b>فالسؤالُ «أيُّها القائم؟»
    /// لا «أيُّها مرحّل؟»</b> — وهو <see cref="StandingJournalEntries"/>.
    /// </summary>
    [Fact]
    public async Task ReopeningTwice_ReversesTheSecondClose()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(Year, 6, 15), 1_000m);
        await CloseAsync();
        await Service().ReopenYearAsync(_companyId, Year, "أوّلاً", "admin");
        var second = await CloseAsync();

        var result = await Service().ReopenYearAsync(_companyId, Year, "ثانياً", "admin");

        Assert.Equal([second.ClosingEntryId], result.ReversedEntryIds);
        Assert.Equal(2, await ReversedClosesAsync());
        Assert.Equal(0m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));

        var third = await CloseAsync();
        Assert.Equal(1_000m, third.NetIncome);
        Assert.Equal(1_000m, await NetCreditAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private YearEndCloseService Service() => new(_db);

    private Task<YearEndCloseResult> CloseAsync(int year = Year) =>
        Service().CloseYearAsync(_companyId, year, "admin");

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "310123456700099", BusinessProfile.General);
        _companyId = company.Id;
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }

    private async Task SellAsync(DateOnly date, decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, date);
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task ReopenMonthAsync(int month)
    {
        var period = await _db.FiscalPeriods.SingleAsync(p =>
            p.CompanyId == _companyId && p.Code == $"{Year}-{month:D2}");
        await new FiscalPeriodService(_db).ReopenAsync(_companyId, period.Id, "admin");
    }

    /// <summary>‏صافي الدائن للحساب في السنة — <b>بقيود الإقفال وعكسِها</b> (<c>WhereReportable</c>).</summary>
    private async Task<decimal> NetCreditAsync(string code) =>
        await _db.JournalEntries.AsNoTracking()
            .WhereReportable(_companyId, new DateOnly(Year, 1, 1), new DateOnly(Year, 12, 31))
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .SumAsync(l => l.Credit - l.Debit);

    /// <summary>
    /// ‏يُحاكي أثرَ سباق الإقفالين قبل CEEF-229: قيدُ إقفالٍ ثانٍ قائمٌ بالوسم نفسِه والسطورِ نفسِها.
    /// </summary>
    private async Task DuplicateAsync(long entryId)
    {
        var original = await _db.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == entryId);
        var copy = JournalEntry.CreateDraft(
            _companyId, original.EntryDate, original.Description, original.SourceDocument);
        copy.MarkEntityLevel();
        foreach (var line in original.Lines)
        {
            var account = await _db.Accounts.IgnoreQueryFilters()
                .SingleAsync(a => a.CompanyId == _companyId && a.Code == line.AccountCode);
            copy.AddClosingLine(account, line.Debit, line.Credit, line.Description);
        }

        copy.Post($"JV-RACE-{entryId}");
        _db.JournalEntries.Add(copy);
        await _db.SaveChangesAsync();
    }

    private Task<int> ReversedClosesAsync() =>
        _db.JournalEntries.CountAsync(e =>
            e.CompanyId == _companyId
            && e.SourceDocument != null && e.SourceDocument.StartsWith("YearEndClose:")
            && e.Status == JournalEntryStatus.Reversed);

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
