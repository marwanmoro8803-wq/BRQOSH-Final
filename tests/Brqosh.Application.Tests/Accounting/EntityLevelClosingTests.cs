using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Companies;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-231 · O-32) <b>الإقفالُ على الشخصية الاعتبارية — لا على فرعٍ صادف أن يكون مختاراً.</b>
///
/// <para><b>وهذا عطلٌ أحدثه إصلاحٌ سابق.</b> أُغلق <c>ط4-٢</c> بواسمِ فروعٍ في
/// <c>SaveChanges</c> — <b>وهو نقطةُ اختناقٍ واحدةٌ أصلحت سبعاً وثلاثين خدمةً كانت
/// تُرحّل قيداً بلا فرع</b>. <b>لكنّه يسم كلَّ قيدٍ جديدٍ بلا استثناء</b>، ومنه قيدُ
/// الإقفال السنويّ.</para>
///
/// <para><b>وأثرُه:</b> الإقفالُ إجراءٌ نظاميٌّ على الشخصية الاعتبارية، <b>والفرعُ بُعدٌ
/// إداريٌّ لا كيانٌ يُقفل</b>. فيُوسَم قيدُ الإقفال بفرع جلسة المحاسب — <b>فتُسند أرباحُ
/// المنشأة المبقاةُ كلُّها إلى الفرع الذي صادف أن يكون مفتوحاً على الشاشة حين ضُغط
/// الزرّ</b>. ولا يظهر ذلك في قائمة الدخل لأنّها تستثني قيودَ الإقفال — <b>ويستقرّ
/// صامتاً في حقوق الملكية</b>، وهو صنفُ العطل نفسُه الذي وقع في <c>م1-١٥</c>.</para>
///
/// <para><b>وهويّةُ هـ-٢٦:</b> قيدٌ مُعلَنٌ على مستوى المنشأة يبقى بلا فرعٍ بعد الحفظ،
/// <b>مهما كان الفرعُ المختارُ في الجلسة</b>.</para>
/// </summary>
public class EntityLevelClosingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _riyadhId;
    private long _jeddahId;
    private int _seq;

    private const int Year = 2026;

    public EntityLevelClosingTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        BranchSessionContext.PreferredBranchId = null;
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: قيدُ الإقفال بلا فرعٍ — وفرعُ الجلسة مضبوطٌ عمداً على جدّة.</b>
    ///
    /// <para><b>وضبطُ الجلسة ليس تفصيلاً</b>: بلا فرعٍ مختارٍ يقع الوسمُ على المقرّ
    /// الرئيس، <b>واختبارٌ يقرأ «الرياض» لا يُفرّق بين «وُسم بالمقرّ» و«لم يُوسَم»</b>.
    /// فيُختار غيرُ المقرّ ليكون الفرقُ مرئياً.</para>
    /// </summary>
    [Fact]
    public async Task TheYearEndClosingEntry_CarriesNoBranch_EvenWithABranchSelected()
    {
        await SetupAsync();

        await InvoiceInBranchAsync(_riyadhId, 10_000m);
        await InvoiceInBranchAsync(_jeddahId, 4_000m);

        // ‏**وجدّةُ مفتوحةٌ على الشاشة حين يُضغط زرُّ الإقفال** — وهي الحالةُ الواقعية.
        BranchSessionContext.PreferredBranchId = _jeddahId;

        await new YearEndCloseService(_db).CloseYearAsync(_companyId, Year, "admin");

        var closing = await _db.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.CompanyId == _companyId
                              && e.SourceDocument == $"YearEndClose:{Year}");

        Assert.True(closing.IsEntityLevel,
            "قيدُ الإقفال غيرُ مُعلَنٍ على مستوى المنشأة — فيسمه واسمُ الفروع.");

        // ‏هـ-٢٦
        Assert.True(closing.BranchId is null,
            $"وُسم قيدُ الإقفال بالفرع {closing.BranchId} — فأُسندت أرباحُ المنشأة المبقاةُ "
            + "كلُّها إلى الفرع الذي صادف أن يكون مختاراً في الجلسة.");
    }

    /// <summary>
    /// <b>وهـ-٢٢ تصمد بعد الإقفال — فالفروعُ تجمع إلى الكلّ كما كانت.</b>
    ///
    /// <para><b>وهذا نصفُ القياس الذي لا يُنسى</b>: قيدُ إقفالٍ يدخل قائمةَ الدخل يُضاعف
    /// النتيجةَ أو يُصفّرها، <b>وحارسٌ يقيس الفرعَ وحدَه لا يرى ذلك</b>.</para>
    /// </summary>
    [Fact]
    public async Task BranchProfitability_SurvivesTheClose()
    {
        await SetupAsync();

        await InvoiceInBranchAsync(_riyadhId, 10_000m);
        await InvoiceInBranchAsync(_jeddahId, 4_000m);

        var from = new DateOnly(Year, 1, 1);
        var to = new DateOnly(Year, 12, 31);

        var before = await new BranchProfitabilityQuery(_db).ExecuteAsync(_companyId, from, to);

        BranchSessionContext.PreferredBranchId = _jeddahId;
        await new YearEndCloseService(_db).CloseYearAsync(_companyId, Year, "admin");

        var after = await new BranchProfitabilityQuery(_db).ExecuteAsync(_companyId, from, to);
        var income = await new IncomeStatementQuery(_db).ExecuteAsync(_companyId, from, to);

        Assert.Equal(before.NetIncome, after.NetIncome);
        Assert.Equal(income.NetIncome, after.NetIncome);   // ‏هـ-٢٢

        Assert.Equal(
            before.Rows.Single(r => r.BranchId == _jeddahId).NetIncome,
            after.Rows.Single(r => r.BranchId == _jeddahId).NetIncome);
    }

    /// <summary>
    /// <b>وقيدُ التشغيل يبقى موسوماً — فالإعلانُ استثناءٌ ضيّقٌ لا نزعٌ للواسم.</b>
    ///
    /// <para><b>وحارسٌ يقيس الاستثناءَ وحدَه يمرّ على واسمٍ عُطِّل كلُّه</b> — فيعود
    /// سبعٌ وثلاثون خدمةً تُرحّل بلا فرعٍ صامتةً، وهو العطلُ الذي أُغلق في ط4-٢.</para>
    /// </summary>
    [Fact]
    public async Task AnOperationalEntry_IsStillStamped()
    {
        await SetupAsync();

        BranchSessionContext.PreferredBranchId = _jeddahId;
        var invoice = await InvoiceInBranchAsync(_jeddahId, 2_500m);

        var entry = await _db.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.Id == invoice.JournalEntryId);

        Assert.False(entry.IsEntityLevel);
        Assert.Equal(_jeddahId, entry.BranchId);
    }

    /// <summary>
    /// <b>والإعلانُ لا يقع على قيدٍ مرحّل — فالوسمُ يقع عند الحفظ وما وُسم لا يُنزع.</b>
    /// </summary>
    [Fact]
    public async Task MarkingAPostedEntry_IsRefused()
    {
        await SetupAsync();

        var cash = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var bank = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(Year, 4, 1), "قيدٌ عاديّ");
        entry.AddLine(cash, 100m, 0m);
        entry.AddLine(bank, 0m, 100m);
        entry.Post("JE-EL-1");

        Assert.Throws<AccountingException>(entry.MarkEntityLevel);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<SalesInvoice> InvoiceInBranchAsync(long branchId, decimal amount)
    {
        BranchSessionContext.PreferredBranchId = branchId;

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(Year, 3, ++_seq));
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأةٌ تُقفل", "310123456700088", BusinessProfile.General);
        _companyId = company.Id;

        var existing = await _db.Branches.Where(b => b.CompanyId == _companyId).ToListAsync();
        _riyadhId = existing.FirstOrDefault()?.Id ?? 0;

        if (_riyadhId == 0)
        {
            var head = Branch.Create(_companyId, "HQ", "الرياض", isHeadquarters: true);
            _db.Branches.Add(head);
            await _db.SaveChangesAsync();
            _riyadhId = head.Id;
        }

        var jeddah = Branch.Create(_companyId, "JED", "جدة");
        _db.Branches.Add(jeddah);
        await _db.SaveChangesAsync();
        _jeddahId = jeddah.Id;

        var customer = Customer.Create(_companyId, "عميل", "311111111100004");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
