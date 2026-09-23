using Brqosh.Application.Reporting;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤٠) <b>الشاشةُ كانت تحسب صافيَ الضريبة بمعادلةٍ أقدمَ من الإقرار.</b>
///
/// <para><c>VatReturnDto</c> — وهو ما تقرؤه شاشةُ الإقرار — <b>يُعيد اشتقاقَ الصافي</b>:
/// <c>OutputVat + ProgressBillingsVat − CreditNotesVat</c>. <b>ولا يحمل الحقولَ الأربعة
/// التي أُضيفت إلى الإقرار بعده</b>: تسويةَ إلغاء مستندات الفترات السابقة (CEEF-219)
/// والضريبةَ المقيّدة بلا مستند (CEEF-232 · ض-١) — مخرجاتٍ ومدخلات.</para>
///
/// <para><b>فالإصلاحان بلغا المطبوعةَ ولم يبلغا الشاشة.</b> وبيعُ أصلٍ ثابتٍ — ضريبتُه قيدٌ
/// بلا مستند — <b>يختفي من «صافي الضريبة: … مستحقة للهيئة»</b> الذي يراه المالك،
/// ويبقى في المطبوعة. <b>رقمان لإقرارٍ واحد.</b></para>
///
/// <para><b>وهو صنفُ «قارئٌ ثانٍ يُعيد اشتقاقَ ما تملكه الشجرة»</b> — وملفُّ
/// <c>VatReturnSeesJournalVatTests</c> نفسُه كتب: «حقلٌ يُحسب ولا يدخل الصافي عطلٌ آخر
/// بالاسم نفسِه». <b>ووقع ذلك طبقةً أعلى.</b></para>
///
/// <para><b>والقياسُ على المسار الحقيقيّ</b>: <c>IVatReturnQuery</c> من حاوية الخدمات —
/// المُحوِّلُ <c>file sealed</c> الذي تستدعيه الشاشة — لا على دالّة التحويل وحدَها.</para>
/// </summary>
public class VatReturnScreenCarriesTheWholeNetTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;

    private static readonly DateOnly Q2From = new(2026, 4, 1);
    private static readonly DateOnly Q2To = new(2026, 6, 30);

    public VatReturnScreenCarriesTheWholeNetTests()
    {
        TestActor.Administrator();

        // ‏قاعدةٌ في الذاكرة مشترَكةٌ بالاسم — تبقى ما بقي اتّصالٌ مفتوح، ولا ملفَّ على القرص.
        var cs = $"Data Source=brqosh-h40-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(cs);
        _keepAlive.Open();

        var services = new ServiceCollection();
        services.AddBrqoshInfrastructure(cs, usePostgres: false);
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _scope.Dispose();
        _provider.Dispose();
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> صافي الضريبة على الشاشة == صافيها في الإقرار — <b>برقمٍ
    /// مستقلٍّ لا بالمقارنة وحدَها</b>، كي لا يمرّ خطأٌ يقع في الاثنين معاً.
    ///
    /// <para>الربعُ الثاني: بيعٌ ١٠٠٬٠٠٠ (١٥٬٠٠٠) · <b>إلغاءُ بيعٍ من الربع الأوّل</b>
    /// (−١٬٥٠٠) · <b>توريدٌ بقيد</b> (+٣٬٠٠٠) · <b>مشترىً بقيد</b> (مدخلات ٢٬٠٠٠) ·
    /// <b>إلغاءُ شراءٍ من الربع الأوّل</b> (−٣٠٠).</para>
    ///
    /// <code>
    /// مخرجات = 15,000 − 1,500 + 3,000 = 16,500
    /// مدخلات =  2,000 −   300          =  1,700
    /// صافي   = 14,800
    /// </code>
    /// </summary>
    [Fact]
    public async Task TheScreenNet_IsTheReturnNet()
    {
        await SetupAsync();
        await ScenarioAsync();

        var screen = await ScreenAsync();
        var filed = await new VatReturnQuery(_db).ExecuteAsync(_companyId, Q2From, Q2To);

        Assert.Equal(16_500m, filed.NetOutputVat);
        Assert.Equal(1_700m, filed.NetDeductibleInputVat);
        Assert.Equal(14_800m, filed.NetVatPayable);

        Assert.Equal(16_500m, screen.NetOutputVat);
        Assert.Equal(1_700m, screen.NetDeductibleInputVat);
        Assert.Equal(14_800m, screen.NetVatPayable);
    }

    /// <summary>
    /// <b>وضريبةُ بيع الأصل — المسارُ الذي ليس فرضيّاً</b> — تبلغ الشاشة. قيدٌ بلا مستند
    /// هو كلُّ ما يُرحَّل عند بيع أصلٍ ثابت.
    /// </summary>
    [Fact]
    public async Task VatPostedByJournal_ReachesTheScreen()
    {
        await SetupAsync();
        await PostSupplyVatByJournalAsync(3_000m, new DateOnly(2026, 5, 20));

        var screen = await ScreenAsync();

        Assert.Equal(3_000m, screen.JournalOutputVat);
        Assert.Equal(3_000m, screen.NetVatPayable);
    }

    /// <summary>
    /// <b>وإلغاءُ مستندٍ من فترةٍ سابقة يُنقص الشاشة</b> — وكان يبقى فيها فيُدفع ما رُدّ.
    /// </summary>
    [Fact]
    public async Task APriorPeriodCancellation_ReachesTheScreen()
    {
        await SetupAsync();
        var q1Sale = await SellAsync(new DateOnly(2026, 2, 10), 10_000m);
        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, q1Sale.Id, new DateOnly(2026, 5, 15), "admin");

        var screen = await ScreenAsync();

        Assert.Equal(1_500m, screen.CancelledOutputVat);
        Assert.Equal(-1_500m, screen.NetVatPayable);
    }

    /// <summary>
    /// <b>والإقرارُ المطبوعُ يطبع سطراً لكلّ حدٍّ في صافيه.</b> كان صافيه صحيحاً <b>وسطورُه
    /// لا تجمع إليه</b> — في الوثيقة التي يُقدَّم منها الإقرار.
    /// </summary>
    [Fact]
    public async Task ThePrintedReturn_ShowsEveryTermOfItsNet()
    {
        await SetupAsync();
        await ScenarioAsync();
        var filed = await new VatReturnQuery(_db).ExecuteAsync(_companyId, Q2From, Q2To);

        var rows = FinancialReportPdfGenerator.VatReturnRows(filed);
        decimal Row(string name) => rows.Single(r => r.Name == name).Amount;

        Assert.Equal(1_500m, Row("تسوية إلغاء مستندات فترات سابقة (تُطرح)"));
        Assert.Equal(3_000m, Row("ضريبة مخرجات مقيّدة بلا مستند (تُضاف)"));
        Assert.Equal(300m, Row("تسوية إلغاء مشتريات فترات سابقة (تُطرح)"));
        Assert.Equal(2_000m, Row("ضريبة مدخلات مقيّدة بلا مستند (تُضاف)"));

        Assert.Equal(
            Row("ضريبة المخرجات — مبيعات") + Row("ضريبة المخرجات — مستخلصات")
            - Row("ضريبة مرتجعة") - Row("تسوية إلغاء مستندات فترات سابقة (تُطرح)")
            + Row("ضريبة مخرجات مقيّدة بلا مستند (تُضاف)"),
            Row("صافي ضريبة المخرجات"));
        Assert.Equal(
            Row("ضريبة مدخلات قابلة للخصم") - Row("ضريبة مدخلات مُخفّضة")
            - Row("تسوية إلغاء مشتريات فترات سابقة (تُطرح)")
            + Row("ضريبة مدخلات مقيّدة بلا مستند (تُضاف)"),
            Row("صافي ضريبة المدخلات"));
    }

    /// <summary>
    /// <b>شاهدُ ضبط — ربعٌ بلا حركةٍ من الأربع لم يتغيّر:</b> مستنداتٌ وحدَها، والرقمان
    /// كما كانا حرفاً قبل الإصلاح.
    /// </summary>
    [Fact]
    public async Task AReturnWithDocumentsOnly_IsUnchanged()
    {
        await SetupAsync();
        await SellAsync(new DateOnly(2026, 4, 10), 100_000m);

        var screen = await ScreenAsync();

        Assert.Equal(15_000m, screen.NetOutputVat);
        Assert.Equal(15_000m, screen.NetVatPayable);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>ما تستدعيه الشاشةُ حرفاً: <c>IVatReturnQuery</c> من الحاوية.</summary>
    private async Task<VatReturnDto> ScreenAsync() =>
        await _scope.ServiceProvider.GetRequiredService<IVatReturnQuery>()
            .ExecuteAsync(_companyId, Q2From, Q2To);

    private async Task ScenarioAsync()
    {
        await SellAsync(new DateOnly(2026, 4, 10), 100_000m);

        var q1Sale = await SellAsync(new DateOnly(2026, 2, 10), 10_000m);
        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, q1Sale.Id, new DateOnly(2026, 5, 15), "admin");

        await PostSupplyVatByJournalAsync(3_000m, new DateOnly(2026, 5, 20));
        await PostInputVatByJournalAsync(2_000m, new DateOnly(2026, 5, 21));

        var q1Purchase = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 3, 1), "Q1-P", vatDeductible: true);
        q1Purchase.AddLine("خدمة", 1m, 2_000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(q1Purchase);
        await new PurchaseInvoiceCancellationService(_db)
            .CancelAsync(_companyId, q1Purchase.Id, new DateOnly(2026, 6, 1), "admin");
    }

    private async Task<SalesInvoice> SellAsync(DateOnly on, decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, on);
        invoice.AddLine("بضاعة", 1m, amount);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>توريدٌ خاضعٌ رُحّل بقيد — وهو شكلُ ترحيل بيع الأصل الثابت.</summary>
    private async Task PostSupplyVatByJournalAsync(decimal vat, DateOnly on)
    {
        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var revenue = await AccountAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue);
        var output = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatOutput);

        var net = Math.Round(vat / VatCalculator.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
        var entry = JournalEntry.CreateDraft(_companyId, on, "توريدٌ خاضعٌ بقيد");
        entry.AddLine(cash, net + vat, 0m);
        entry.AddLine(revenue, 0m, net);
        entry.AddLine(output, 0m, vat);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task PostInputVatByJournalAsync(decimal vat, DateOnly on)
    {
        var cash = await AccountAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var expense = await AccountAsync("5215");
        var input = await AccountAsync(SaudiChartOfAccounts.SystemCodes.VatInput);

        var net = Math.Round(vat / VatCalculator.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
        var entry = JournalEntry.CreateDraft(_companyId, on, "مشترىً خاضعٌ بقيد");
        entry.AddLine(expense, net, 0m);
        entry.AddLine(input, vat, 0m);
        entry.AddLine(cash, 0m, net + vat);
        await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task<Account> AccountAsync(string code) =>
        await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);

    private async Task SetupAsync()
    {
        _companyId = (await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)).Id;

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورّد", "312233344400003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }
}
