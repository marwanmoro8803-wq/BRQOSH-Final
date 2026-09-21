using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣١) <b>الإشعارُ المدين يُسجّل استهلاكَ طبقاته تحت مفتاحٍ فارغٍ
/// مشترَكٍ بين كلّ الإشعارات — ثمّ يُلغى بحركةٍ مضادّةٍ بدل عكسِ الأصل.</b>
///
/// <para><b>وهو صنفُ ح-٣٠ بعينه، وقد سمّاه التدقيقُ كذلك</b> — غير أنّه هنا أشدّ.</para>
///
/// <list type="number">
/// <item><b>المفتاحُ يُبنى قبل أن يُولَد الرقم.</b> <c>ConsumeForIssueAsync</c> يُنادى
/// بـ<c>$"DebitNote:{note.NoteNumber}"</c> <b>قبل</b> سطرِ توليد الرقم بأربعةَ عشرَ سطراً،
/// و<c>NoteNumber</c> نوعُه <c>string?</c> لا يُسنَد إلّا داخل <c>DebitNote.Post</c>.
/// <b>فالاستيفاءُ يُصيّر العدمَ فراغاً ولا يرمي</b>، فيخرج المفتاح <c>DebitNote:</c> —
/// <b>واحداً لكلّ إشعارٍ تُصدره المنشأةُ ما بقيت</b>.</item>
///
/// <item><b>والحركةُ المخزنيّةُ تُرحَّل تحت <c>DebitNote:{الرقم المولَّد}</c></b> — أي
/// مفتاحٌ آخر. <b>فدفترُ الاستهلاك والحركةُ لا يلتقيان</b>.</item>
///
/// <item><b>والإلغاءُ يُرحّل وارِداً مقابلاً</b> بدل <c>ReverseDocumentMovementAsync</c> —
/// <b>فيُنشئ طبقةً جديدةً في ذيل طابور الوارد-أوّلاً</b> بدل أن يُرمّم الطبقةَ التي
/// أُخذ منها. <b>فينشقّ مجموعُ الطبقات عن الرصيد، وتخرج البيعةُ التالية بتكلفةٍ ليست
/// تكلفتَها.</b></item>
/// </list>
///
/// <para><b>وخطرُ المفتاح المشترَك يتجاوز الإلغاء</b>: لو رُمِّم بـ<c>DebitNote:</c> لَرُدَّت
/// طبقاتُ <b>كلّ</b> إشعارٍ سبق — فالمفتاحُ الفارغ ليس «مفتاحاً ناقصاً» بل <b>مفتاحُ
/// مستندٍ آخر</b>.</para>
/// </summary>
public class DebitNoteLayersCarryItsOwnKeyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _productId;
    private long _warehouseId;

    public DebitNoteLayersCarryItsOwnKeyTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── المفتاحُ نفسُه ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الأحدّ:</b> سجلُّ الاستهلاك يحمل <b>رقمَ الإشعار</b> — لا <c>DebitNote:</c>
    /// عارياً. <b>وهذا يُقاس على الصفّ المحفوظ نفسِه</b>، لا على أثرٍ غيرِ مباشر.
    /// </summary>
    [Fact]
    public async Task TheConsumptionRecord_CarriesTheNoteNumber_NotAnEmptyKey()
    {
        await SetupAsync();
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        var keys = await _db.InventoryCostConsumptions
            .Where(c => c.CompanyId == _companyId)
            .Select(c => c.SourceDocument)
            .Distinct()
            .ToListAsync();

        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.Equal($"DebitNote:{note.NoteNumber}", k));
    }

    /// <summary>
    /// <b>وإشعاران لا يتقاسمان مفتاحاً واحداً</b> — وهو ما يفرّق هذا البندَ عن ح-٣٠:
    /// <b>هناك مفتاحان مختلفان لأمرٍ واحد، وهنا مفتاحٌ واحدٌ لكلّ المستندات.</b>
    ///
    /// <para><b>فلو رُمِّم بهذا المفتاح لَرُدَّت طبقاتُ إشعارٍ آخر</b> — والضررُ حينئذٍ
    /// مخزونٌ يُخلق من العدم، لا مخزونٌ لا يعود.</para>
    /// </summary>
    [Fact]
    public async Task TwoDebitNotes_DoNotShareOneConsumptionKey()
    {
        await SetupAsync();
        var invoice = await PurchaseAsync(20m, 10m, "SUP-1");
        var first = await DebitNoteAsync(4m, 10m, invoice);
        var second = await DebitNoteAsync(3m, 10m, invoice);

        var keys = await _db.InventoryCostConsumptions
            .Where(c => c.CompanyId == _companyId)
            .Select(c => c.SourceDocument)
            .Distinct()
            .ToListAsync();

        Assert.Equal(2, keys.Count);
        Assert.NotEqual(first.NoteNumber, second.NoteNumber);
    }

    // ── الهويّةُ الحاكمة ────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> بعد الإلغاء <b>مجموعُ الطبقات يطابق الرصيد</b>.
    /// </summary>
    [Fact]
    public async Task AfterCancelling_TheCostLayersAgreeWithTheBalance()
    {
        await SetupAsync();
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        await CancelAsync(note);

        await AssertLayersMatchBalanceAsync();
    }

    /// <summary>
    /// <b>والطبقةُ المأخوذُ منها تعود بعينها</b> — لا طبقةٌ ثالثةٌ في ذيل الطابور.
    ///
    /// <para><b>وطبقتان بسعرين هما موضعُ البرهان</b>: عشرٌ بعشرة ثمّ عشرٌ بعشرين. والإشعارُ
    /// يأخذ أربعاً من <b>الأقدم</b>، <b>فردُّها يُعيدها إلى الأقدم</b> لا يُنشئ طبقةً
    /// بتاريخ الإلغاء تُؤجَّل خلف الأغلى.</para>
    /// </summary>
    [Fact]
    public async Task TheConsumedLayer_IsRestored_NotRecreated()
    {
        await SetupAsync();
        await PurchaseAsync(10m, 10m, "SUP-1");
        await PurchaseAsync(10m, 20m, "SUP-2");
        var note = await DebitNoteAsync(4m, 10m);

        await CancelAsync(note);

        var layers = await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.RemainingQuantity > 0)
            .OrderBy(l => l.Id)
            .ToListAsync();

        Assert.Equal(2, layers.Count);
        Assert.Equal(10m, layers[0].RemainingQuantity);
        Assert.Equal(10m, layers[0].UnitCost);
        Assert.Equal(10m, layers[1].RemainingQuantity);
        Assert.Equal(20m, layers[1].UnitCost);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — الرصيدُ يعود كما كان.</b> <b>فالإصلاحُ صحّح التكلفةَ ولم يكسر
    /// الكمّية</b> — وهي التي كانت صحيحةً أصلاً، <b>وبها نجا العطل</b>.
    /// </summary>
    [Fact]
    public async Task TheQuantities_StillReturnToWhereTheyStarted()
    {
        await SetupAsync();
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        await CancelAsync(note);

        Assert.Equal(10m, await new InventoryService(_db)
            .GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — المتوسّطُ المرجّح يعود إلى ما بدأ منه.</b>
    /// <b>ولا طبقاتِ هنا أصلاً</b>، فالقياسُ على التكلفة نفسِها.
    /// </summary>
    [Fact]
    public async Task UnderWeightedAverage_TheUnitCostReturnsToWhereItStarted()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        await CancelAsync(note);

        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.Equal(10m, product.WeightedAverageCost);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — هويّةُ الموردين تبقى سليمةً عبر الإلغاء.</b>
    /// <b>ولولاه لجاز أن يكون «الإصلاح» قد مسّ طرفَ القيد.</b>
    /// </summary>
    [Fact]
    public async Task ThePayablesIdentity_SurvivesTheCancellation()
    {
        await SetupAsync();
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        await CancelAsync(note);

        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>
    /// (ح-٣١ب) <b>وإشعارٌ رُحّل <i>قبل</i> هذا الإصلاح لا يُترك رصيداً بلا
    /// تكلفةٍ تحملُه.</b>
    ///
    /// <para><b>وهذا تبعةُ الإصلاح لا بندٌ مستقلّ</b>: بعد توحيد المفتاح
    /// صار <c>RestoreConsumedLayersAsync</c> <b>يبحث بمفتاحٍ لا تحملُه الصفوفُ
    /// القديمة</b> — <b>والترميمُ بالمفتاح القديم ممتنع</b> لأنّه مشترَكٌ
    /// بين كلّ الإشعارات، فيردُّ طبقاتَ مستندٍ آخر.</para>
    ///
    /// <para><b>وقُيس قبل علاجِه: الطبقات ٦ والرصيد ١٠</b> — <b>وهو ارتدادٌ
    /// عن سلوكٍ قائم</b>، إذ كانت الحركةُ المضادّةُ تُبقي الهويّةَ قائمةً.
    /// <b>فلا يُشحَن إصلاحٌ يكسر ما لم يكن مكسوراً.</b></para>
    /// </summary>
    [Fact]
    public async Task ANoteFromBeforeTheFix_StillLeavesLayersMatchingTheBalance()
    {
        await SetupAsync();
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        // ‏محاكاةُ ما كان يُكتب قبل ح-٣١ — **المفتاحُ الفارغُ نفسُه**.
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE inventory_cost_consumptions SET SourceDocument = 'DebitNote:'");
        _db.ChangeTracker.Clear();

        await CancelAsync(note);

        await AssertLayersMatchBalanceAsync();
    }

    /// <summary>
    /// <b>وشاهدُ ضبطِ الشاهد — البديلُ لا يُوقِع طبقاتٍ في المتوسّط المرجّح.</b>
    ///
    /// <para><c>RestoreConsumedLayersAsync</c> <b>تعود <c>false</c> دائماً في المتوسّط</b>
    /// — إذ لا تُكتب سجلّاتُ استهلاكٍ أصلاً. <b>فلو كان البديلُ غيرَ
    /// مميِّز لأنشأ طبقاتٍ على منشأةٍ لا طبقاتِ لها</b> — وهو عطلٌ
    /// يخلقُه العلاجُ نفسُه.</para>
    /// </summary>
    [Fact]
    public async Task UnderWeightedAverage_TheFallback_CreatesNoLayers()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        await PurchaseAsync(10m, 10m, "SUP-1");
        var note = await DebitNoteAsync(4m, 10m);

        await CancelAsync(note);

        Assert.Empty(await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId).ToListAsync());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task AssertLayersMatchBalanceAsync()
    {
        var layered = await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == _productId)
            .SumAsync(l => l.RemainingQuantity);

        var onHand = await new InventoryService(_db)
            .GetOnHandAsync(_companyId, _productId, _warehouseId);

        Assert.True(layered == onHand,
            $"‏الطبقات {layered} والرصيد {onHand} — انشقّ مجموعُ الطبقات عن الرصيد، "
            + "فكلُّ صرفٍ لاحقٍ يخرج بتكلفةٍ خاطئة.");
    }

    private async Task SetupAsync(InventoryCostingMethod method = InventoryCostingMethod.Fifo)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;

        // ‏**الطبقاتُ لا تُنشأ إلّا في الوارد-أوّلاً** — والافتراضُ متوسّطٌ مرجّح،
        // ‏فقياسُ الطبقات على منشأةٍ بالمتوسّط يقيس العدمَ ويمرّ خضراءَ (درسُ ح-٣٠).
        company.SetCostingMethod(method);
        await _db.SaveChangesAsync();

        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _supplierId = supplier.Id;
        _productId = product.Id;
        _warehouseId = (await _db.Warehouses
            .SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
    }

    private async Task<PurchaseInvoice> PurchaseAsync(decimal qty, decimal unitPrice, string reference)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 1, 10), reference, vatDeductible: true);
        invoice.AddLine("توريد", qty, unitPrice, _productId);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<DebitNote> DebitNoteAsync(
        decimal qty, decimal unitPrice, PurchaseInvoice? invoice = null)
    {
        invoice ??= await _db.PurchaseInvoices
            .OrderBy(i => i.Id)
            .FirstAsync(i => i.CompanyId == _companyId);

        var note = DebitNote.CreateDraft(
            _companyId, _supplierId, invoice.Id, new DateOnly(2026, 1, 20), "مرتجع تالف");
        note.AddLine("مرتجع", qty, unitPrice, _productId);
        return await new DebitNotePostingService(_db).PostAsync(note);
    }

    private Task CancelAsync(DebitNote note) =>
        new DebitNoteCancellationService(_db)
            .CancelAsync(_companyId, note.Id, new DateOnly(2026, 1, 25), "admin");
}
