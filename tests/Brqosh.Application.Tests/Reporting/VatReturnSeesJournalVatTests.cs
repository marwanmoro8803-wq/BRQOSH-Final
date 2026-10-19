using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-232 · ض-١) <b>الإقرارُ الضريبيّ أعمى عن كلّ ضريبةٍ لا يحملها مستند.</b>
///
/// <para><b>ما كان:</b> <c>VatReturnQuery</c> يقرأ خمسةَ مستندات — الفاتورةَ والمستخلصَ
/// وإشعارَي الدائن والمدين وفاتورةَ المشتريات — <b>ولا يقرأ دفترَ الأستاذ إطلاقاً</b>.
/// فضريبةٌ قُيّدت على <c>2106</c> بقيدٍ لا يحمل أحدَ هذه المستندات <b>لا تبلغ الإقرار
/// أبداً</b>: لا في فترتها ولا في غيرها.</para>
///
/// <para><b>والمسارُ ليس فرضياً:</b> بيعُ أصلٍ ثابت توريدٌ خاضعٌ نظاماً، وترحيلُه قيدٌ
/// لا فاتورة. ومحاسبٌ يُسوّي فرقاً بقيدٍ يدويٍّ على حساب المخرجات يفعل الشيءَ نفسَه.
/// <b>فيُقدَّم الإقرارُ ناقصاً بضريبةٍ مُحصَّلةٍ فعلاً</b> — وهي بعينها الحالةُ التي
/// يُطالَب فيها بالضريبة والغرامة معاً عند الفحص.</para>
///
/// <para><b>والتمييزُ الحاكم — وبغيره ينكسر الإقرارُ في الاتجاه المقابل:</b> قيدُ
/// <b>التسوية</b> ينقل الضريبةَ من <c>2106</c> إلى <c>2107</c> <b>وليس توريداً</b>.
/// فعدُّه مخرجاتٍ سالبةً يُفرِّغ الإقرارَ من ضريبته كلِّها. والقاعدةُ: <b>قيدٌ يمسّ
/// حساب صافي الضريبة تسويةٌ لا توريد</b>.</para>
///
/// <para><b>وهويّةُ ض-١:</b> ضريبةُ المخرجات في الإقرار = ما حملته المستندات + ما قُيّد
/// على <c>2106</c> بقيدٍ ليس مستنداً محسوباً <b>ولا تسويةً</b>.</para>
/// </summary>
public class VatReturnSeesJournalVatTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;

    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 3, 31);
    private static readonly DateOnly Inside = new(2026, 2, 10);

    public VatReturnSeesJournalVatTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
        ManualJournals.LiftApprovalThreshold(_db);   // ‏(accounting-core#6) قيودٌ يدويّةٌ كبيرةٌ لسببٍ غيرِ الاعتماد — منشأةٌ بمستخدمٍ واحد

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _customerId = customer.Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> ضريبةٌ قُيّدت بقيدٍ يدويٍّ على حساب المخرجات تبلغ الإقرار.
    ///
    /// <para><b>والمقياسُ على <c>NetOutputVat</c> لا على حقلٍ جديد</b> — فهو الرقمُ الذي
    /// يُقدَّم، <b>وحقلٌ يُحسب ولا يدخل الصافي عطلٌ آخر بالاسم نفسِه</b>.</para>
    /// </summary>
    [Fact]
    public async Task VatPostedByJournalEntry_ReachesTheReturn()
    {
        await SellAsync(100_000m);                       // ‏مخرجات 15,000 من مستند
        await PostSupplyVatByJournalAsync(3_000m);       // ‏ومخرجات 3,000 بقيد

        var result = await ReturnAsync();

        Assert.Equal(18_000m, result.NetOutputVat);
    }

    /// <summary>
    /// <b>وقيدُ التسوية ليس توريداً.</b>
    ///
    /// <para><b>وهو الاتجاهُ المعاكس للعطل نفسِه:</b> التسويةُ تُنقص <c>2106</c> بقيمة
    /// الضريبة كلِّها، <b>فعدُّها مخرجاتٍ سالبةً يُصفّر الإقرار</b> — ويُقدَّم بلا ريال
    /// على منشأةٍ باعت بمئة ألف.</para>
    /// </summary>
    [Fact]
    public async Task ASettlementToTheNetVatAccount_IsNotASupply()
    {
        await SellAsync(100_000m);
        await SettleVatToNetAccountAsync(15_000m);

        var result = await ReturnAsync();

        Assert.Equal(15_000m, result.NetOutputVat);
        Assert.Equal(0m, result.GlVariance);
    }

    /// <summary>
    /// <b>وضريبةُ المدخلات بقيدٍ تُخصم كذلك.</b>
    ///
    /// <para><b>والاتجاهُ هنا في صالح المنشأة</b> — وعماها عنه يجعلها <b>تدفع ضريبةً
    /// خصمُها حقٌّ لها</b>. وهو عطلٌ لا يشتكي منه أحد لأنّه لا يُخالف الهيئة، <b>ويُكلّف
    /// المالكَ مالاً كلَّ ربع</b>.</para>
    /// </summary>
    [Fact]
    public async Task InputVatPostedByJournalEntry_IsDeducted()
    {
        await SellAsync(100_000m);
        await PostInputVatByJournalAsync(2_000m);

        var result = await ReturnAsync();

        Assert.Equal(2_000m, result.NetDeductibleInputVat);
        Assert.Equal(13_000m, result.NetVatPayable);
    }

    /// <summary>
    /// <b>ولا يُحسب المستندُ مرّتين — وهو الخطرُ الذي يولد مع الإصلاح.</b>
    ///
    /// <para>فاتورةُ المبيعات تُرحَّل قيداً على <c>2106</c> كذلك. <b>فقراءةُ الأستاذ بلا
    /// تمييزٍ تُضاعف ضريبةَ كلّ فاتورة</b> — ويُقدَّم إقرارٌ بثلاثين ألفاً على مبيعاتِ
    /// مئة ألف، وهو ما تردّه الهيئةُ فوراً بنسبةٍ ٣٠٪ لا وجودَ لها.</para>
    /// </summary>
    [Fact]
    public async Task ADocumentsOwnJournalEntry_IsNotCountedTwice()
    {
        await SellAsync(100_000m);

        var result = await ReturnAsync();

        Assert.Equal(15_000m, result.NetOutputVat);
    }

    /// <summary>
    /// <b>والقيدُ خارجَ الفترة لا يدخلها.</b>
    ///
    /// <para><b>وبغير هذا القياس يمرّ «اقرأ كلَّ الأستاذ»</b>: تُجمع ضريبةُ السنة كلِّها
    /// في إقرار الربع الأول، <b>فيُدفع ما لم يُستحقّ بعد</b> ثمّ يُقدَّم الربعُ الثاني
    /// فارغاً — وكلاهما مخالفة.</para>
    /// </summary>
    [Fact]
    public async Task AJournalEntryOutsideThePeriod_StaysOutside()
    {
        await SellAsync(100_000m);
        await PostSupplyVatByJournalAsync(9_000m, on: new DateOnly(2026, 5, 5));

        var result = await ReturnAsync();

        Assert.Equal(15_000m, result.NetOutputVat);
    }

    /// <summary>
    /// (CEEF-232 · ض-٣) <b>والتسويةُ الحقيقيّة لا تمرّ بـ<c>2107</c> — لأنّ لا كاتبَ له.</b>
    ///
    /// <para><b>وهذا هو العطلُ الذي كاد الإصلاحُ يُدخله:</b> حارسُ التسوية كان يسأل
    /// «هل مسّ القيدُ حسابَ صافي الضريبة؟» — <b>وحسابٌ لا تكتب فيه شيفرةٌ قطّ لا يُمَسّ
    /// أبداً</b>، وهو مُوثَّقٌ نصّاً في <c>SystemAccountCoverageTests</c>. فكان الحارسُ
    /// يحرس باباً لا يمرّ منه أحد.</para>
    ///
    /// <para><b>والتسويةُ التي يعرفها المنتجُ فعلاً هي هذه:</b> <c>مدين 2106 / دائن البنك</c>
    /// — سدادٌ للهيئة بقيدٍ يدويّ. <b>وكانت تُقرأ مخرجاتٍ سالبة</b>: منشأةٌ باعت بمئة ألفٍ
    /// ثمّ سدّدت ضريبتَها تُقدّم إقرارَها <b>بصفر</b>، فتُطالَب بالضريبة والغرامة — <b>وهو
    /// عينُ الضرر الذي جاء ض-١ ليمنعه، مقلوباً على صاحبه</b>.</para>
    /// </summary>
    [Fact]
    public async Task PayingTheAuthority_IsNotANegativeSupply()
    {
        await SellAsync(100_000m);
        await PayVatToAuthorityAsync(15_000m);

        var result = await ReturnAsync();

        Assert.Equal(15_000m, result.NetOutputVat);
    }

    /// <summary>
    /// <b>ومطابقةُ الكشف البنكيّ على حساب الضريبة تسويةٌ كذلك.</b>
    ///
    /// <para><c>BankStatementDifferencePostingService</c> يقبل <b>أيَّ حسابٍ مقابل</b>
    /// ويُرحّل بمصدرٍ ليس من المستندات الخمسة. <b>فمحاسبٌ يُطابق دفعةَ الهيئة على
    /// <c>2106</c> يُنتج المسارَ نفسَه من بابٍ آخر</b> — والحارسُ يجب أن يكون على
    /// <b>اتّجاه الحركة</b> لا على اسم المصدر، وإلّا لزم إحصاءُ كلّ خدمةٍ تُرحّل قيداً.</para>
    /// </summary>
    [Fact]
    public async Task ABankMatchAgainstTheVatAccount_IsNotANegativeSupply()
    {
        await SellAsync(100_000m);
        await PayVatToAuthorityAsync(15_000m, source: "BankStatementDifference");

        Assert.Equal(15_000m, (await ReturnAsync()).NetOutputVat);
    }

    /// <summary>
    /// <b>وعكسُ قيدٍ يُلغي أثرَه — ولا يُقرأ سداداً.</b>
    ///
    /// <para><b>وهو الطرفُ الذي يمنع الحارسَ من أن يصير عمًى:</b> لو أُهمل كلُّ نقصٍ
    /// لبقي قيدٌ خاطئٌ محسوباً <b>بعد عكسه</b> — فيُقدَّم الإقرارُ بضريبةٍ أُلغيت.
    /// <b>فالزوجُ يُستبعد بطرفيه معاً</b>: الأصلُ المعكوس وعكسُه، فلا يبقى منه أثر.</para>
    /// </summary>
    [Fact]
    public async Task AReversedJournalSupply_LeavesNothingBehind()
    {
        await SellAsync(100_000m);
        await PostSupplyVatByJournalAsync(3_000m);

        Assert.Equal(18_000m, (await ReturnAsync()).NetOutputVat);

        var entry = await _db.JournalEntries
            .SingleAsync(e => e.CompanyId == _companyId && e.Description == "توريدٌ خاضعٌ بقيد");
        await new JournalReversalService(_db).ReverseAsync(_companyId, entry.Id, Inside, "admin");

        // ‏**ويعود الإقرارُ إلى ضريبة المستند وحدَها** — لا زيادةَ ولا نقص.
        Assert.Equal(15_000m, (await ReturnAsync()).NetOutputVat);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>سدادُ الضريبة للهيئة — <c>مدين 2106 / دائن البنك</c>، بلا مساسٍ بـ<c>2107</c>.</summary>
    private async Task PayVatToAuthorityAsync(decimal amount, string? source = null)
    {
        var output = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatOutput);
        var bank = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var entry = JournalEntry.CreateDraft(
            _companyId, new DateOnly(2026, 3, 31), "سداد ضريبة القيمة المضافة", source);
        entry.AddLine(output, amount, 0m);
        entry.AddLine(bank, 0m, amount);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task<VatReturnResult> ReturnAsync() =>
        await new VatReturnQuery(_db).ExecuteAsync(_companyId, From, To);

    private async Task SellAsync(decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>توريدٌ خاضعٌ رُحّل بقيد: نقدٌ مدين، وإيرادٌ وضريبةُ مخرجات دائنان.</summary>
    private async Task PostSupplyVatByJournalAsync(decimal vat, DateOnly? on = null)
    {
        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var revenue = await AccountAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue);
        var output = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatOutput);

        var net = Math.Round(vat / VatCalculator.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
        var entry = JournalEntry.CreateDraft(_companyId, on ?? Inside, "توريدٌ خاضعٌ بقيد");
        entry.AddLine(cash, net + vat, 0m);
        entry.AddLine(revenue, 0m, net);
        entry.AddLine(output, 0m, vat);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    /// <summary>مشترىً خاضعٌ رُحّل بقيد: مصروفٌ وضريبةُ مدخلات مدينان، والنقدُ دائن.</summary>
    private async Task PostInputVatByJournalAsync(decimal vat)
    {
        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var expense = await AccountAsync("5215");
        var input = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatInput);

        var net = Math.Round(vat / VatCalculator.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
        var entry = JournalEntry.CreateDraft(_companyId, Inside, "مشترىً خاضعٌ بقيد");
        entry.AddLine(expense, net, 0m);
        entry.AddLine(input, vat, 0m);
        entry.AddLine(cash, 0m, net + vat);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    /// <summary>تسويةٌ ضريبية: المخرجاتُ تُقفل في حساب صافي الضريبة المستحقة.</summary>
    private async Task SettleVatToNetAccountAsync(decimal amount)
    {
        var output = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatOutput);
        var netVat = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatNetPayable);

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 3, 31), "تسوية ضريبية");
        entry.AddLine(output, amount, 0m);
        entry.AddLine(netVat, 0m, amount);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task<Account> AccountAsync(string code) =>
        await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);
}
