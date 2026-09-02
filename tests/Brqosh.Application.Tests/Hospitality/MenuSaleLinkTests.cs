using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Hospitality;

/// <summary>
/// (CEEF-230 · ط3-٣ج) <b>ربطُ الوصفة بالبيع — إغلاقُ صنفِ العطل لا حالتِه.</b>
///
/// <para><b>ما كان:</b> الوصفةُ مبنيّةٌ في <c>ط3-٣</c> والصرفُ <b>زرٌّ يُضغط</b> في شاشة
/// القائمة. <b>فمَن نسي لم يصرف</b> — والمخزونُ يبقى كما اشتُري، والربحُ مئةٌ بالمئة،
/// ويظهر الفرقُ عجزَ جردٍ آخرَ السنة. <b>والزرُّ يُغلق الفجوةَ لمَن يتذكّره، ولا يُغلق
/// صنفَ العطل</b>: النسيانُ ليس خطأً نادراً في مطعمٍ يبيع مئةَ طبقٍ في الساعة — <b>هو
/// الحالةُ الغالبة</b>.</para>
///
/// <para><b>وما صار:</b> الطبقُ يُربط بصنفِ بيعٍ <b>خدميّ</b>، وبيعُ ذلك الصنف — <b>من
/// نقطة البيع أو من فاتورةٍ عادية، فكلاهما يمرّ بمسار الترحيل نفسِه</b> — يُفجّر الوصفةَ
/// ويصرف مكوّناتِها. <b>ولا يعتمد على ذاكرةِ أحد.</b></para>
///
/// <para><b>ولمَ خدميّاً:</b> الطبقُ لا يُخزَّن، مكوّناتُه تُخزَّن. <b>وصنفٌ مخزنيٌّ
/// يُخصم مرّتين</b>: مرّةً بذاته في مسار الترحيل، ومرّةً بمكوّناته في تفجير الوصفة.</para>
/// </summary>
public class MenuSaleLinkTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _customerId;
    private long _breadId;
    private long _meatId;

    public MenuSaleLinkTests()
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
    /// <b>القياسُ الحاكم: بيعُ الطبق بفاتورةٍ يصرف وصفتَه — بلا زرٍّ يُضغط.</b>
    ///
    /// <para>وهذا هو الفرقُ كلُّه: <c>ط3-٣</c> أثبتت أنّ <b>الصرفَ</b> يصرف، وهذه تُثبت
    /// أنّ <b>البيعَ</b> يصرف.</para>
    /// </summary>
    [Fact]
    public async Task SellingTheLinkedProduct_ConsumesTheRecipe_WithoutAnyManualIssue()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-BURG", "برجر");
        await MenuAsync("BURG", "برجر", dishProductId, (_breadId, 1m), (_meatId, 0.15m));

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("برجر", 10m, 30m, productId: dishProductId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        // ‏هـ-١١ عبر مسار البيع: عشرُ حصصٍ ⇒ عشرةُ أرغفةٍ وكيلوٌ ونصفٌ لحماً.
        Assert.Equal(90m, await OnHandAsync(_breadId));
        Assert.Equal(48.5m, await OnHandAsync(_meatId));
    }

    /// <summary>
    /// <b>وتكلفةُ المبيعات تُقيَّد بالبيع لا بالجرد.</b>
    ///
    /// <para>‏١٠ أرغفةٍ × ٢ + ١.٥ كجم × ٤٠ = ٨٠. <b>وبلا ربطٍ كانت صفراً</b> — والربحُ
    /// المعلَن ٣٠٠ بدل ٢٢٠.</para>
    /// </summary>
    [Fact]
    public async Task SellingTheLinkedProduct_BooksCostOfSales()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-BURG", "برجر");
        await MenuAsync("BURG", "برجر", dishProductId, (_breadId, 1m), (_meatId, 0.15m));

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("برجر", 10m, 30m, productId: dishProductId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(80m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));
    }

    /// <summary>
    /// <b>وما لا وصفةَ له يمرّ صامتاً — والصمتُ هنا صواب.</b>
    ///
    /// <para>الفاتورةُ الواحدة تحمل طبقاً وسلعةً عادية. <b>ورفضُ ما ليس طبقاً يمنع البيعَ
    /// كلَّه</b> — وهو أسوأُ من عدم الصرف. فالسلعةُ العادية تُصرف بمسارها المخزنيّ المعتاد،
    /// والطبقُ بوصفته، <b>وكلٌّ منهما مرّةً واحدة</b>.</para>
    /// </summary>
    [Fact]
    public async Task OrdinaryGoodsOnTheSameInvoice_AreNotRefusedAndNotDoubleIssued()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-BURG", "برجر");
        await MenuAsync("BURG", "برجر", dishProductId, (_breadId, 1m));
        var sodaId = await StockAsync("SODA", "مشروب", quantity: 40m, unitCost: 3m);

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("برجر", 5m, 30m, productId: dishProductId);
        invoice.AddLine("مشروب", 4m, 6m, productId: sodaId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(95m, await OnHandAsync(_breadId));   // ‏الطبقُ صرف وصفتَه
        Assert.Equal(36m, await OnHandAsync(sodaId));     // ‏والسلعةُ صُرفت مرّةً واحدة
    }

    /// <summary>
    /// <b>والطبقُ الخدميّ لا يُخصم بذاته.</b>
    ///
    /// <para>وهذا ما يمنع القيدَ المزدوج: مسارُ الترحيل يصرف المخزنيَّ فقط، <b>فالطبقُ
    /// لا يدخله</b>، ولا يظهر له رصيدٌ سالبٌ في مستودعٍ لم يدخله قطّ.</para>
    /// </summary>
    [Fact]
    public async Task TheDishProductItself_IsNeverStockIssued()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-BURG", "برجر");
        await MenuAsync("BURG", "برجر", dishProductId, (_breadId, 1m));

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("برجر", 7m, 30m, productId: dishProductId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(0m, await OnHandAsync(dishProductId));
        Assert.False(await _db.StockMovements
            .AnyAsync(m => m.CompanyId == _companyId
                           && m.Lines.Any(l => l.ProductId == dishProductId)));
    }

    /// <summary>
    /// <b>وربطُ وصفةٍ بصنفٍ مخزنيٍّ يُرفض باسمه.</b>
    ///
    /// <para><b>ولو قُبل لخُصمت التكلفةُ مرّتين</b>: الصنفُ بذاته في مسار الترحيل،
    /// ومكوّناتُه في تفجير الوصفة — <b>ولا شيءَ في القوائم يقول أيُّهما الزائد</b>.</para>
    /// </summary>
    [Fact]
    public async Task LinkingAStockableProduct_IsRefusedByName()
    {
        await SetupAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new KitchenIssueService(_db)
                .EnsureSaleProductLinkableAsync(_companyId, _breadId));

        Assert.Contains("صنفٌ مخزنيّ", error.Message);
    }

    /// <summary>
    /// <b>وطبقٌ مربوطٌ بلا وصفةٍ يُوقف البيع — ولا يمرّ صامتاً.</b>
    ///
    /// <para>الربطُ فعلٌ مقصود: <b>مَن ربط أعلن أنّ لهذا الصنف وصفة</b>. فإن أُفرغت
    /// الوصفةُ بعد الربط، <b>البيعُ بلا تكلفةٍ هو عينُ العطل</b> — ورفضُه بصوتٍ أرخصُ من
    /// سنةٍ من قوائمَ كاذبة.</para>
    /// </summary>
    [Fact]
    public async Task LinkedDishWithAnEmptyRecipe_RefusesTheSaleLoudly()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-EMPTY", "طبق بلا وصفة");

        var item = MenuItem.Create(_companyId, "EMPTY", "طبق بلا وصفة", MenuSection.Main, 30m);
        item.LinkSaleProduct(dishProductId);
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("طبق بلا وصفة", 3m, 30m, productId: dishProductId);

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new SalesInvoicePostingService(_db).PostAsync(invoice));

        Assert.Contains("بلا وصفة", error.Message);
    }

    /// <summary>
    /// <b>وطبقٌ مُعطَّلٌ لا يصرف.</b>
    ///
    /// <para>وإيقافُ صنفٍ في القائمة قرارٌ تشغيليّ — <b>ولو بقي يصرف بعد إيقافه لصار
    /// المخزونُ ينقص لطبقٍ رُفع من القائمة</b>.</para>
    /// </summary>
    [Fact]
    public async Task DeactivatedMenuItem_DoesNotConsume()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-OFF", "طبق موقوف");
        var item = await MenuAsync("OFF", "طبق موقوف", dishProductId, (_breadId, 1m));
        item.Deactivate();
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("طبق موقوف", 5m, 30m, productId: dishProductId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(100m, await OnHandAsync(_breadId));
    }

    /// <summary>
    /// <b>وسطران للطبق نفسِه يُجمعان.</b>
    ///
    /// <para>الفاتورةُ قد تحمل «برجر ٣» و«برجر ٢» سطرين منفصلين. <b>وصرفُ أحدِهما فقط
    /// يُنقص المصروفَ عن الحقيقة</b> — والفرقُ يظهر جرداً لا خطأً.</para>
    /// </summary>
    [Fact]
    public async Task TwoLinesOfTheSameDish_ConsumeTheirSum()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-BURG", "برجر");
        await MenuAsync("BURG", "برجر", dishProductId, (_breadId, 1m));

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("برجر", 3m, 30m, productId: dishProductId);
        invoice.AddLine("برجر", 2m, 30m, productId: dishProductId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(95m, await OnHandAsync(_breadId));
    }

    /// <summary>
    /// <b>وفاتورةٌ بلا أطباقٍ لا تُقيّد صرفَ مطبخٍ فارغاً.</b>
    ///
    /// <para><b>وقيدٌ بصفرٍ ليس عديمَ الأثر</b>: يُلوّث دفترَ اليومية بحركاتٍ لا معنى لها،
    /// ويُوهم المراجعَ أنّ مطبخاً عمل حيث لم يعمل.</para>
    /// </summary>
    [Fact]
    public async Task InvoiceWithNoLinkedDish_PostsNoKitchenIssue()
    {
        await SetupAsync();
        var sodaId = await StockAsync("SODA", "مشروب", quantity: 40m, unitCost: 3m);

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("مشروب", 4m, 6m, productId: sodaId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.False(await _db.JournalEntries
            .AnyAsync(e => e.CompanyId == _companyId && e.Description == "تكلفة مكوّنات المطبخ"));
    }

    /// <summary>
    /// <b>ونقطةُ البيع تصرف كذلك — وهي القناةُ الحقيقية في المطعم.</b>
    ///
    /// <para>القناتان تلتقيان عند <c>PostCoreAsync</c>، <b>والربطُ معلَّقٌ هناك لا في
    /// إحداهما</b>: فلا قناةَ تُنسى، ولا يُكرَّر الرابطُ مرّتين فيختلفان. <b>ولو عُلِّق في
    /// <c>PosSaleService</c> وحدَه لصرفت نقطةُ البيع وسكتت الفاتورة</b> — والفرقُ بينهما
    /// في المطعم الواحد يومٌ من الطلبات الخارجية.</para>
    /// </summary>
    [Fact]
    public async Task SellingThroughThePointOfSale_ConsumesTheRecipeToo()
    {
        await SetupAsync();
        var dishProductId = await ServiceProductAsync("DISH-BURG", "برجر");
        await MenuAsync("BURG", "برجر", dishProductId, (_breadId, 1m), (_meatId, 0.15m));

        var shift = Brqosh.Domain.Retail.PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        await new Infrastructure.Retail.PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(dishProductId, 4m)], true,
            Brqosh.Domain.Retail.PosPaymentMethod.Cash, "كاشير");

        Assert.Equal(96m, await OnHandAsync(_breadId));
        Assert.Equal(49.4m, await OnHandAsync(_meatId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> OnHandAsync(long productId) =>
        await new InventoryService(_db).GetOnHandAsync(_companyId, productId, _warehouseId);

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new Infrastructure.Reporting.TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<MenuItem> MenuAsync(string code, string name, long saleProductId,
        params (long ProductId, decimal Quantity)[] components)
    {
        var item = MenuItem.Create(_companyId, code, name, MenuSection.Main, 30m);
        foreach (var (productId, quantity) in components)
            item.AddComponent(productId, quantity);

        item.LinkSaleProduct(saleProductId);
        await new KitchenIssueService(_db).EnsureSaleProductLinkableAsync(_companyId, saleProductId);

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<long> ServiceProductAsync(string code, string name)
    {
        // ‏وسعرُ البيع هنا لأنّ نقطةَ البيع تُسعّر من الصنف لا من سطرٍ يُكتب.
        var product = Product.Create(_companyId, code, name, "حبة", ProductType.Service,
            salePrice: 30m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مطعم", "310123456700055", BusinessProfile.Hospitality);
        _companyId = company.Id;

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        _warehouseId = warehouse.Id;

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        _breadId = await StockAsync("BREAD", "خبز", quantity: 100m, unitCost: 2m);
        _meatId = await StockAsync("MEAT", "لحم", quantity: 50m, unitCost: 40m);
    }

    private async Task<long> StockAsync(string code, string name, decimal quantity, decimal unitCost)
    {
        var product = Product.Create(_companyId, code, name, "حبة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await new InventoryService(_db).PostDocumentMovementAsync(
            _companyId, _warehouseId, StockMovementType.Receipt, new DateOnly(2026, 1, 1),
            "رصيد افتتاحيّ", $"Opening:{code}", [(product.Id, quantity, unitCost, null, (DateOnly?)null)]);
        await _db.SaveChangesAsync();

        return product.Id;
    }
}
