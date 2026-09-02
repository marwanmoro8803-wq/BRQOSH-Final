using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-231 · ط4-٢) <b>ربحيّةُ الفروع — والبيانات كانت كلُّها موجودةً ولا يقرؤها تقرير.</b>
///
/// <para><b>ما كان:</b> كلُّ مستندٍ يحمل <c>BranchId</c>، <b>وكلُّ قيدٍ كذلك</b>، ومنتقي
/// الفرع في الجلسة مبنيٌّ منذ <c>CEEF-024</c>. <b>وصفرُ تقريرٍ في المستودع كلِّه يُرشِّح
/// بالفرع.</b> فمنشأةٌ بثلاثة فروعٍ تعرف ربحَها مجموعاً <b>ولا تعرف أيَّ فرعٍ يكسبه</b> —
/// وقد يكون فرعٌ يخسر ويُخفيه مجموعُ إخوته.</para>
///
/// <para><b>وهويّةُ هـ-٢٢:</b> مجموعُ صافي الفروع = صافي قائمة الدخل للفترة نفسِها،
/// هللةً بهللة. <b>وتقريرُ فروعٍ لا يجمع إلى الكلّ يُقرأ خطأً في أحد الفرعين ولا يُعرف
/// أيُّهما.</b></para>
/// </summary>
public class BranchProfitabilityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _riyadhId;
    private long _jeddahId;
    private int _seq;

    public BranchProfitabilityTests()
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

    // ── هـ-٢٢: الفروعُ تجمع إلى الكلّ ───────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: كلُّ فرعٍ يُقرأ على حدة، ومجموعُهم صافي قائمة الدخل.</b>
    ///
    /// <para><b>والفرعان يختلفان عمداً</b>: مثالٌ بفرعين متساويين <b>يمرّ ولو وُزّع
    /// المجموعُ بالتساوي</b> — ولا يُفرّق إلا حين تفترق أرقامُهما.</para>
    /// </summary>
    [Fact]
    public async Task EachBranchIsReadAlone_AndTheySumToNetIncome()
    {
        await SetupAsync();

        await InvoiceInBranchAsync(_riyadhId, 10_000m);
        await InvoiceInBranchAsync(_jeddahId, 4_000m);

        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);

        var result = await new BranchProfitabilityQuery(_db).ExecuteAsync(_companyId, from, to);
        var income = await new IncomeStatementQuery(_db).ExecuteAsync(_companyId, from, to);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(10_000m, result.Rows.Single(r => r.BranchId == _riyadhId).Revenue);
        Assert.Equal(4_000m, result.Rows.Single(r => r.BranchId == _jeddahId).Revenue);

        // ‏هـ-٢٢
        Assert.Equal(income.NetIncome, result.NetIncome);
    }

    /// <summary>
    /// <b>والأعلى ربحاً أوّلاً — فالترتيبُ هو القرار.</b>
    /// </summary>
    [Fact]
    public async Task BranchesAreOrderedByNetIncome()
    {
        await SetupAsync();

        await InvoiceInBranchAsync(_jeddahId, 1_000m);
        await InvoiceInBranchAsync(_riyadhId, 9_000m);

        var result = await new BranchProfitabilityQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(_riyadhId, result.Rows[0].BranchId);
        Assert.Equal(_jeddahId, result.Rows[1].BranchId);
    }

    /// <summary>
    /// <b>وهامشٌ بلا إيرادٍ لا يُحسب — ولا يُقال صفراً.</b>
    ///
    /// <para><b>وصفرٌ مكانَ «لا يُحسب» كذبٌ صغير</b>: يُقرأ «تعادلاً» وهو خسارةٌ كاملة.</para>
    /// </summary>
    [Fact]
    public void AMarginWithoutRevenue_IsNotZero()
    {
        var row = new BranchProfitRow(1L, "فرعٌ بلا إيراد", Revenue: 0m, Expenses: 500m);

        Assert.Null(row.Margin);
        Assert.Equal(-500m, row.NetIncome);
    }

    /// <summary>
    /// <b>وهامشٌ بإيرادٍ يُحسب من صافي الدخل لا من الإيراد وحده.</b>
    /// </summary>
    [Fact]
    public void TheMargin_IsNetOverRevenue()
    {
        var row = new BranchProfitRow(1L, "فرع", Revenue: 1_000m, Expenses: 750m);

        Assert.Equal(25m, row.Margin);
    }

    /// <summary>
    /// <b>وما لا فرعَ له يُعرض صفّاً باسمه — لا يُطوى ولا يُوزَّع.</b>
    ///
    /// <para>قيدٌ بلا فرعٍ موجودٌ في كلّ قاعدةٍ رُقّيت من نسخةٍ سابقة لإسناد الفروع.
    /// <b>وطيُّه يكسر هـ-٢٢</b>، <b>وتوزيعُه بالتناسب يخترع أرقاماً لا سندَ لها</b>.</para>
    /// </summary>
    [Fact]
    public async Task EntriesWithoutABranch_AppearAsTheirOwnRow()
    {
        await SetupAsync();

        await InvoiceInBranchAsync(_riyadhId, 5_000m);

        // ‏قيدٌ بلا فرعٍ — كما يقع في قاعدةٍ رُقّيت قبل إسناد الفروع.
        var invoice = await InvoiceInBranchAsync(_riyadhId, 3_000m);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == invoice.JournalEntryId);
        _db.Entry(entry).Property("BranchId").CurrentValue = null;
        await _db.SaveChangesAsync();

        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);

        var result = await new BranchProfitabilityQuery(_db).ExecuteAsync(_companyId, from, to);
        var income = await new IncomeStatementQuery(_db).ExecuteAsync(_companyId, from, to);

        Assert.Contains(result.Rows, r => r.BranchId is null && r.Revenue == 3_000m);

        // ‏وهـ-٢٢ تصمد رغم القيد اليتيم.
        Assert.Equal(income.NetIncome, result.NetIncome);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<SalesInvoice> InvoiceInBranchAsync(long branchId, decimal amount)
    {
        BranchSessionContext.PreferredBranchId = branchId;

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, ++_seq));
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأةٌ بفرعين", "310123456700077", BusinessProfile.General);
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

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
