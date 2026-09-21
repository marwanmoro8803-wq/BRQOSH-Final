using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٨) <b>إعادةُ تقييمٍ بسعرٍ خاطئ كانت حلقةً مغلقة — لا تُعكَس
/// ولا تُعاد.</b>
///
/// <list type="number">
/// <item><b>رسالةُ الإعادة تأمر بما لا يُجدي</b>: «أُعيد التقييمُ لهذا التاريخ سلفاً —
/// <b>واعكسْ قيدَه إن أردت إعادته</b>». وحارسُها <c>Status != Draft</c> —
/// <b>والمعكوسُ ليس مسودّة</b>، فمن يعكس يُمنع كما مُنِع قبلَه.</item>
///
/// <item><b>والعكسُ المباشرُ مرفوضٌ أصلاً</b>: <c>JournalReversalService</c> يرمي
/// «هذا القيد مرتبط بمستند نظام — يُصحَّح من المستند المصدر»، <b>ولا مسارَ تصحيحٍ في
/// المستند</b>.</item>
/// </list>
///
/// <para><b>فيُحال المستخدمُ من رسالةٍ إلى رسالةٍ إلى لا شيء</b> — <b>وسعرٌ خاطئٌ يبقى
/// في دفاترِ سنةٍ تُقفَل عليه</b>، وأثرُه في <c>4107</c>/<c>5217</c> لا يُنزع.</para>
///
/// <para><b>والطرفان يُعكسان معاً</b>: التقييمُ يُنشئ قيدَه وقيدَ عكسِه في اليوم التالي.
/// <b>وعكسُ الأوّل وحدَه يترك الثاني حيّاً فيبقى فرقٌ دائم</b> — وهو عينُ ما يحذّر منه
/// التعليقُ في الخدمة نفسِها.</para>
/// </summary>
public class AWrongRateRevaluationCanBeUndoneTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public AWrongRateRevaluationCanBeUndoneTests()
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

    // ── الحلقةُ المغلقة ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: سعرٌ خاطئ يُلغى ثمّ يُعاد التقييمُ بالصحيح.</b>
    ///
    /// <para><b>وهو ما كان ممتنعاً</b>: الإلغاءُ لا مسارَ له، والإعادةُ مرفوضةٌ أبداً.</para>
    /// </summary>
    [Fact]
    public async Task AfterCancelling_TheRevaluationCanBeRedoneAtTheRightRate()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);
        var asOf = new DateOnly(2026, 12, 31);

        await RevalueAsync(asOf, 9.99m);                       // ‏سعرٌ خاطئ
        await CancelAsync(asOf);

        var again = await RevalueAsync(asOf, 3.85m);           // ‏وبالصحيح

        Assert.Equal(115m, again.ReceivableAdjustment);        // ‏١٬١٥٠ × (3.85 − 3.75)
    }

    /// <summary>
    /// <b>وأثرُ السعر الخاطئ يزول من الدفتر</b> — <b>ويُقاس على ميزان المراجعة لا على
    /// حالة صفّ</b>: حسابُ فروق العملة غيرِ المحقَّقة يعود إلى ما كان.
    /// </summary>
    [Fact]
    public async Task Cancelling_LeavesNoTraceOfTheWrongRate()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);
        var asOf = new DateOnly(2026, 12, 31);
        var before = await BalanceAsync("4107");

        await RevalueAsync(asOf, 9.99m);
        await CancelAsync(asOf);

        Assert.Equal(before, await BalanceAsync("4107"));
    }

    /// <summary>
    /// <b>والطرفان يُعكسان معاً:</b> قيدُ التقييم وقيدُ عكسِه التلقائيّ في الغد.
    ///
    /// <para><b>وعكسُ الأوّل وحدَه يترك الثاني حيّاً فيبقى فرقٌ دائم</b> — فيُقاس
    /// <b>عددُ ما بقي مُرحَّلاً</b> لا مجرّدُ نجاح العمليّة.</para>
    /// </summary>
    [Fact]
    public async Task BothLegs_AreReversed_NotOne()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);
        var asOf = new DateOnly(2026, 12, 31);

        await RevalueAsync(asOf, 3.85m);
        var cancelled = await CancelAsync(asOf);

        Assert.Equal(2, cancelled);

        // ‏**والقياسُ على حالة الأصليّين لا على غياب `Posted`**:
        // ‏قيدُ العكس يرث المستندَ نفسَه **فيبقى `Posted` تحت المفتاح**.
        var originals = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId
                        && e.SourceDocument!.StartsWith("MonetaryRevaluation:")
                        && e.ReversalEntryId != null)
            .ToListAsync();

        Assert.Equal(2, originals.Count);
        Assert.All(originals, e => Assert.Equal(JournalEntryStatus.Reversed, e.Status));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — والحارسُ ما زال حيّاً:</b> تقييمٌ ثانٍ بلا إلغاءٍ يُرفض.
    ///
    /// <para><b>ولولاه لَقِيس «تُعاد الإعادة» ومرّ</b> — وفتحُ البابِ على مصراعيه
    /// <b>يُضاعف الأثرَ ويترك فرقاً دائماً</b>، وهو ما بُني الحارسُ لأجله.</para>
    /// </summary>
    [Fact]
    public async Task WithoutCancelling_ASecondRevaluation_IsStillRefused()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);
        var asOf = new DateOnly(2026, 12, 31);

        await RevalueAsync(asOf, 3.85m);

        await Assert.ThrowsAsync<AccountingException>(() => RevalueAsync(asOf, 3.90m));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — إلغاءُ ما لا وجودَ له يُرفض صراحةً</b> ولا يمرّ صامتاً.
    /// </summary>
    [Fact]
    public async Task CancellingWhatWasNeverRevalued_IsRefused()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);

        await Assert.ThrowsAsync<AccountingException>(() => CancelAsync(new DateOnly(2026, 11, 30)));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — الرسالةُ تصف مساراً قائماً:</b> لم تعد تأمر بعكسِ القيد،
    /// <b>إذ ذاك مرفوضٌ في `JournalReversalService` لأنّ القيدَ لمستندِ نظام</b>.
    /// </summary>
    [Fact]
    public async Task TheRefusalMessage_NamesAPathThatExists()
    {
        await SetupAsync();
        await SalesInvoiceAsync(1_000m, 3.75m);
        var asOf = new DateOnly(2026, 12, 31);
        await RevalueAsync(asOf, 3.85m);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => RevalueAsync(asOf, 3.90m));

        Assert.DoesNotContain("اعكسْ", ex.Message, StringComparison.Ordinal);
        Assert.Contains("يُلغى", ex.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<MonetaryRevaluationResult> RevalueAsync(DateOnly asOf, decimal usdRate) =>
        new MonetaryRevaluationService(_db).RevalueAsync(
            _companyId, asOf, new Dictionary<string, decimal> { ["USD"] = usdRate }, "admin");

    private Task<int> CancelAsync(DateOnly asOf) =>
        new MonetaryRevaluationService(_db)
            .CancelAsync(_companyId, asOf, new DateOnly(2027, 1, 15), "admin");

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        var row = trial.FirstOrDefault(r => r.AccountCode == code);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }

    private async Task SalesInvoiceAsync(decimal amount, decimal rate)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.SetCurrency("USD", rate);
        invoice.AddLine("تصدير", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة التصدير", "310123456700142", BusinessProfile.General);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل دوليّ", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
