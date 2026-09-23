using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤١ · <c>tax-zatca#12</c>) <b>أوعيةُ الإقرار بالفئة تُحسب لكلّ
/// مستندٍ بخصمه وسعره — ثمّ تُجمع.</b>
///
/// <para><b>ما كان:</b> <c>Aggregate</c> يصبّ <b>سطورَ المستندات كلِّها خاماً</b> في
/// <c>ComputeSubtotals</c> واحدة — <b>بلا خصمِ مستندٍ وبعملة المستند</b> — والإجماليّاتُ
/// فوقها بالريال بعد الخصم. <b>فلا يمكن أن تتطابق.</b> وقاس التدقيق: خصمُ ١٠٠ على ١٬٠٠٠
/// ⟵ الوعاءُ ١٬٠٠٠/١٥٠ والفاتورةُ ٩٠٠/١٣٥؛ <b>وفاتورةُ USD بـ3.75 ⟵ الوعاءُ ١٬٠٠٠/١٥٠
/// والفاتورةُ ٣٬٧٥٠/٥٦٢٫٥٠</b>. <b>والمصدِّرُ — وهو عينُ حالة الصفريّ بعملةٍ أجنبيّة —
/// يرى أوعيتَه بالعملة الخطأ.</b></para>
///
/// <para><b>وما غاب عن الأوعية كلّيّاً</b>: المستخلصاتُ وتسويةُ إلغاء الفترات السابقة —
/// وكلاهما في الصافي. <b>فمقاولٌ دخلُه كلُّه مستخلصاتٌ يرى أوعيةً فارغة.</b></para>
///
/// <para><b>والهويّةُ المقيسة</b>: مجموعُ ضريبة الأوعية + ما قُيّد بلا مستند (ولا فئةَ له)
/// == صافي المخرجات. <b>والطرفان من مصدرين</b>: الأوعيةُ من السطور، والصافي من إجماليّات
/// المستندات المخزونة.</para>
/// </summary>
public class VatReturnBucketsReconcileTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private readonly long _supplierId;

    private static readonly DateOnly Q2From = new(2026, 4, 1);
    private static readonly DateOnly Q2To = new(2026, 6, 30);
    private static readonly DateOnly Inside = new(2026, 5, 1);

    public VatReturnBucketsReconcileTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورّد", "312233344400003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── المُقاس في التدقيق ─────────────────────────────────────────────────

    /// <summary><b>Probe C حرفاً:</b> خصمُ مستندٍ ١٠٠ على ١٬٠٠٠ ⟵ الوعاءُ ٩٠٠/١٣٥ لا ١٬٠٠٠/١٥٠.</summary>
    [Fact]
    public async Task ADocumentDiscount_ReachesTheBucket()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        invoice.AddLine("بضاعة", 1m, 1_000m);
        invoice.ApplyDocumentDiscount(100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var standard = Bucket((await ReturnAsync()).Sales, VatCategory.Standard);

        Assert.Equal(900m, standard.TaxableAmount);
        Assert.Equal(135m, standard.VatAmount);
    }

    /// <summary><b>Probe D حرفاً:</b> USD 1,000 بسعر 3.75 ⟵ الوعاءُ ٣٬٧٥٠/٥٦٢٫٥٠ بالريال.</summary>
    [Fact]
    public async Task AForeignSale_IsBucketedInRiyals()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        invoice.AddLine("بضاعة", 1m, 1_000m);
        invoice.SetCurrency("USD", 3.75m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var standard = Bucket((await ReturnAsync()).Sales, VatCategory.Standard);

        Assert.Equal(3_750m, standard.TaxableAmount);
        Assert.Equal(562.50m, standard.VatAmount);
    }

    /// <summary>
    /// <b>والمصدِّر — الحالةُ التي سمّاها التدقيق:</b> صادراتٌ صفريّةٌ بالدولار تُعرض بالريال.
    /// </summary>
    [Fact]
    public async Task AForeignZeroRatedExport_IsBucketedInRiyals()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        invoice.AddLine("تصدير", 1m, 2_000m, null, VatCategory.ZeroRated, "VATEX-SA-32");
        invoice.SetCurrency("USD", 3.75m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var zero = Bucket((await ReturnAsync()).Sales, VatCategory.ZeroRated);

        Assert.Equal(7_500m, zero.TaxableAmount);
        Assert.Equal(0m, zero.VatAmount);
    }

    /// <summary><b>والمشتريات كذلك</b> — فاتورةُ شراءٍ بالدولار وإشعارُها المدين (ح-٣٩).</summary>
    [Fact]
    public async Task AForeignPurchaseAndItsReturn_AreBucketedInRiyals()
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, Inside, "USD-P", vatDeductible: true);
        invoice.AddLine("خدمة", 2m, 1_000m);
        invoice.SetCurrency("USD", 3.75m);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 5, 10), "مرتجع",
            [new RequestedReturn(0, 1m)]);
        await new DebitNotePostingService(_db).PostAsync(note);

        var result = await ReturnAsync();
        var standard = Bucket(result.Purchases, VatCategory.Standard);

        Assert.Equal(3_750m, standard.TaxableAmount);       // ‏7,500 − 3,750
        Assert.Equal(562.50m, standard.VatAmount);
        Assert.Equal(result.NetDeductibleInputVat, result.Purchases.Sum(b => b.VatAmount));
    }

    // ── ما غاب عن الأوعية ──────────────────────────────────────────────────

    /// <summary>
    /// <b>المستخلصُ توريدٌ خاضعٌ بالنسبة الأساسية</b> — وكان خارج الأوعية كلّيّاً.
    /// </summary>
    [Fact]
    public async Task AProgressBilling_IsInTheStandardBucket()
    {
        await PostProgressBillingAsync(50_000m);

        var result = await ReturnAsync();
        var standard = Bucket(result.Sales, VatCategory.Standard);

        Assert.Equal(50_000m, standard.TaxableAmount);
        Assert.Equal(7_500m, standard.VatAmount);
    }

    /// <summary>
    /// <b>وإلغاءُ مستندٍ من فترةٍ سابقةٍ يُنقص وعاءَه في فترة الإلغاء</b> — كما يُنقص الصافي.
    /// </summary>
    [Fact]
    public async Task APriorPeriodCancellation_ReducesItsBucket()
    {
        await SellAsync(Inside, 100_000m);
        var q1 = await SellAsync(new DateOnly(2026, 2, 10), 10_000m);
        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, q1.Id, new DateOnly(2026, 5, 15), "admin");

        var standard = Bucket((await ReturnAsync()).Sales, VatCategory.Standard);

        Assert.Equal(90_000m, standard.TaxableAmount);
        Assert.Equal(13_500m, standard.VatAmount);
    }

    // ── التقريب ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>فلسُ التحويل يقع في وعاءٍ واحدٍ لا يضيع.</b>
    ///
    /// <para>ين بسعر <c>0.025</c>، سطران ١٠٠٫١٠ أساسيٌّ وصفريّ: كلُّ وعاءٍ
    /// <c>100.10 × 0.025 = 2.5025 ⟵ 2.50</c>، <b>والمستندُ <c>200.20 × 0.025 = 5.005 ⟵ 5.01</c></b>.
    /// فتحويلُ الأوعية منفردةً يُسقط فلساً عن الدفاتر. <b>والفلسُ يُسنَد إلى أكبر وعاء</b>
    /// كي تساوي الأوعيةُ ما في الأستاذ حرفاً.</para>
    /// </summary>
    [Fact]
    public async Task TheConversionCent_LandsInOneBucket_AndIsNotLost()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        invoice.AddLine("بضاعة", 1m, 100.10m);
        invoice.AddLine("تصدير", 1m, 100.10m, null, VatCategory.ZeroRated, "VATEX-SA-32");
        invoice.SetCurrency("JPY", 0.025m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        Assert.Equal(5.01m, invoice.Subtotal);

        var result = await ReturnAsync();

        Assert.Equal(5.01m, result.Sales.Sum(b => b.TaxableAmount));
        Assert.Equal(invoice.VatAmount, result.Sales.Sum(b => b.VatAmount));
        // ‏**والضريبةُ لا تُسنَد إلى الصفريّ** — فلسٌ ضريبيٌّ على توريدٍ صفريٍّ تردّه الهيئة.
        Assert.Equal(0m, Bucket(result.Sales, VatCategory.ZeroRated).VatAmount);
    }

    // ── الهويّة ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>الهويّةُ الحاكمة على ربعٍ فيه كلُّ شيء:</b> ضريبةُ الأوعية + ما قُيّد بلا مستند ==
    /// صافي المخرجات. <b>والطرفان من مصدرين</b> — السطورُ وإجماليّاتُ المستندات المخزونة.
    /// </summary>
    [Fact]
    public async Task TheBuckets_ReconcileToTheNet()
    {
        var discounted = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        discounted.AddLine("بضاعة", 1m, 1_000m);
        discounted.AddLine("تصدير", 1m, 500m, null, VatCategory.ZeroRated, "VATEX-SA-32");
        discounted.ApplyDocumentDiscount(150m);
        await new SalesInvoicePostingService(_db).PostAsync(discounted);

        var foreign = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        foreign.AddLine("بضاعة", 1m, 1_000m);
        foreign.SetCurrency("USD", 3.75m);
        await new SalesInvoicePostingService(_db).PostAsync(foreign);

        await PostProgressBillingAsync(20_000m);

        var q1 = await SellAsync(new DateOnly(2026, 2, 10), 4_000m);
        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, q1.Id, new DateOnly(2026, 5, 15), "admin");

        var result = await ReturnAsync();
        var expectedBase = discounted.Subtotal + foreign.Subtotal + 20_000m - q1.Subtotal;

        Assert.Equal(result.NetOutputVat - result.JournalOutputVat, result.Sales.Sum(b => b.VatAmount));
        Assert.Equal(expectedBase, result.Sales.Sum(b => b.TaxableAmount));
        Assert.Equal(expectedBase, result.SalesBaseNet);
    }

    /// <summary>
    /// <b>والمطبوعُ يحمل الفئات</b> — وكان بلا تفكيكٍ أصلاً، <b>وهو الوثيقةُ التي يُقدَّم منها
    /// الإقرار</b>. ضريبةُ الفئات و«بلا فئة» تُعيد صافيَ المخرجات، والأوعيةُ تُعيد صافيَ الوعاء.
    /// </summary>
    [Fact]
    public async Task ThePrintedReturn_CarriesTheCategories()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Inside);
        invoice.AddLine("بضاعة", 1m, 1_000m);
        invoice.AddLine("تصدير", 1m, 500m, null, VatCategory.ZeroRated, "VATEX-SA-32");
        invoice.SetCurrency("USD", 3.75m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await ReturnAsync();
        var section = FinancialReportPdfGenerator.VatReturnRows(result)
            .Where(r => r.Section == "مبيعات حسب الفئة").ToList();

        var baseIndex = section.FindIndex(r => r.Name == "صافي وعاء المبيعات (من المستندات)");
        Assert.True(baseIndex > 0);
        Assert.Equal(result.NetOutputVat, section.Take(baseIndex).Sum(r => r.Amount));
        Assert.Equal(5_625m, section[baseIndex].Amount);                 // ‏(1,000 + 500) × 3.75
        Assert.Equal(section[baseIndex].Amount, section.Skip(baseIndex + 1).Sum(r => r.Amount));
        Assert.Contains(section, r => r.Code == "Z" && r.Amount == 1_875m);
    }

    // ── شاهدُ ضبط ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وإقرارٌ بالريال بلا خصمٍ لم يتغيّر حرفاً</b> — وهو ما كان يعمل وحدَه قبل الإصلاح،
    /// <b>ولولاه لجاز أن يكون الإصلاحُ مسّ كلَّ إقرارٍ قائم</b>.
    /// </summary>
    [Fact]
    public async Task APlainRiyalReturn_IsUnchanged()
    {
        await SellAsync(Inside, 1_000m);

        var only = Assert.Single((await ReturnAsync()).Sales);

        Assert.Equal(VatCategory.Standard, only.Category);
        Assert.Equal(1_000m, only.TaxableAmount);
        Assert.Equal(150m, only.VatAmount);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<VatReturnResult> ReturnAsync() =>
        await new VatReturnQuery(_db).ExecuteAsync(_companyId, Q2From, Q2To);

    private static VatReturnBucket Bucket(IReadOnlyList<VatReturnBucket> buckets, VatCategory c) =>
        buckets.SingleOrDefault(b => b.Category == c) ?? new VatReturnBucket(c, 0m, 0m);

    private async Task<SalesInvoice> SellAsync(DateOnly on, decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, on);
        invoice.AddLine("بضاعة", 1m, amount);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task PostProgressBillingAsync(decimal currentWork)
    {
        var project = ConstructionProject.Create(_companyId, $"PRJ-{currentWork:0}", "مشروع", _customerId,
            contractValue: 5_000_000m, retentionPercent: 0m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            _companyId, project.Id, _customerId, Inside, "مستخلص", currentWork, 0m);
        await new ProgressBillingPostingService(_db).PostAsync(billing);
    }
}
