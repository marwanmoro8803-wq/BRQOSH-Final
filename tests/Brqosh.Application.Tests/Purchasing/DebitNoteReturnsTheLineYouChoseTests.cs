using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٧) <b>الإشعارُ المدين كان يُرجع أوّلَ سطرٍ في الفاتورة مهما
/// اختار المستخدم.</b>
///
/// <para><c>DebitNoteDialog</c> كان يعرض <b>حقلَ وصفٍ حرّاً</b> ويأخذ المعرَّفَ من
/// <c>Lines.FirstOrDefault()</c>. <b>فمن يُرجع الصنفَ الثالث يُنقص مخزونَ الأوّل</b> —
/// <b>والكمّيّةُ والقيمةُ صحيحتان فلا يشكو الميزانُ ولا المورّد</b>، ويظهر العطلُ في
/// جردٍ بعد شهور: صنفٌ ناقصٌ وآخرُ زائدٌ بالعدد نفسِه.</para>
///
/// <para><b>وثلاثةُ عيوبٍ في سطرٍ واحد</b>: المنتَجُ الخطأ · <b>والثلاثيُّ الضريبيُّ لا
/// يعبر</b> (فمرتجعُ توريدٍ معفًى يُعاد تصنيفُه أساسيّاً فتُستردّ ضريبةُ مدخلاتٍ لم
/// تُدفع) · <b>ولا فحصَ كمّيّةٍ للصنف</b> (حدُّ المستند بالقيمة أعمى عن الأصناف).</para>
///
/// <para><b>وأُخرج المنطقُ من الشاشة كي يُقاس</b> — وهو نمطُ «الطرفان مقيسان والوسطُ
/// مُستنتَج» المسجَّل في CEEF-229.</para>
/// </summary>
public class DebitNoteReturnsTheLineYouChoseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _warehouseId;
    private long _firstId;
    private long _thirdId;

    public DebitNoteReturnsTheLineYouChoseTests()
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

    // ── المنتَج ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم — ويُقاس على المخزون لا على حقلٍ في صفّ:</b> إرجاعُ السطر
    /// الثالث <b>يُنقص الصنفَ الثالث</b> ويترك الأوّلَ كما هو.
    ///
    /// <para><b>وكان يُنقص الأوّلَ</b> — والعددُ نفسُه، فلا يظهر إلّا في الجرد.</para>
    /// </summary>
    [Fact]
    public async Task ReturningTheThirdLine_ReducesTheThirdProduct()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع تالف",
            [new RequestedReturn(LineIndex: 2, Quantity: 3m)]);
        await new DebitNotePostingService(_db).PostAsync(note);

        var inv = new InventoryService(_db);
        Assert.Equal(10m, await inv.GetOnHandAsync(_companyId, _firstId, _warehouseId));
        Assert.Equal(7m, await inv.GetOnHandAsync(_companyId, _thirdId, _warehouseId));
    }

    /// <summary>
    /// <b>وسطرُ الإشعار يحمل معرَّفَ الصنف المختار</b> — ويُقاس على الصفّ المحفوظ نفسِه.
    /// </summary>
    [Fact]
    public async Task TheNoteLine_CarriesTheChosenProduct()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع",
            [new RequestedReturn(LineIndex: 2, Quantity: 3m)]);

        Assert.Equal(_thirdId, note.Lines.Single().ProductId);
    }

    // ── الثلاثيُّ الضريبيّ ──────────────────────────────────────────────────

    /// <summary>
    /// <b>والثلاثيُّ الضريبيُّ يعبر</b> (م-٥ · ن-٥٢): فئةُ السطر ورمزُ إعفائه وسببُه.
    ///
    /// <para><b>وبدونه يُعاد تصنيفُ مرتجعِ توريدٍ معفًى أساسيّاً</b> — <b>فتُستردّ
    /// ضريبةُ مدخلاتٍ لم تُدفع أصلاً</b>، وهو تحريفٌ في الإقرار لا خطأُ عرض.</para>
    /// </summary>
    [Fact]
    public async Task TheTaxTriad_CrossesFromTheInvoiceLine()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع",
            [new RequestedReturn(LineIndex: 1, Quantity: 1m)]);

        var line = note.Lines.Single();
        Assert.Equal(VatCategory.Exempt, line.VatCategory);
        Assert.Equal("VATEX-SA-29", line.TaxExemptionReasonCode);
        Assert.Equal("خدمة مالية", line.TaxExemptionReason);
        Assert.Equal(0m, note.VatAmount);
    }

    // ── الكمّيّة ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ولا يُرجَع أكثرُ ممّا اشتُري من الصنف</b> — والحدُّ القائمُ بالقيمة
    /// <b>أعمى عن الأصناف</b>: مئةٌ من صنفٍ رخيصٍ تمرّ داخل فاتورةٍ كبيرة.
    /// </summary>
    [Fact]
    public async Task ReturningMoreThanWasBought_IsRefused()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new DebitNoteDraftBuilder(_db).BuildAsync(
                _companyId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع",
                [new RequestedReturn(LineIndex: 2, Quantity: 999m)]));

        Assert.Contains("المتبقّي", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والمُرجَعُ سابقاً يُحسب</b>: ثلاثٌ ثمّ ثمانٍ من عشرٍ ⟵ تُرفض الثانية.
    /// </summary>
    [Fact]
    public async Task WhatWasAlreadyReturned_CountsAgainstTheRemainder()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();
        var builder = new DebitNoteDraftBuilder(_db);

        var first = await builder.BuildAsync(_companyId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع أوّل", [new RequestedReturn(2, 3m)]);
        await new DebitNotePostingService(_db).PostAsync(first);

        await Assert.ThrowsAsync<AccountingException>(() => builder.BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 11), "مرتجع ثانٍ",
            [new RequestedReturn(2, 8m)]));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — المتبقّي يُحصى لكلّ سطرٍ على حدة:</b> بعد إرجاع ثلاثٍ من
    /// الثالث، <b>يبقى الأوّلُ كاملاً</b>. <b>ولولاه لجاز أن يخصم الفحصُ من الجميع.</b>
    /// </summary>
    [Fact]
    public async Task TheRemainder_IsCountedPerLine()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();
        var builder = new DebitNoteDraftBuilder(_db);

        var note = await builder.BuildAsync(_companyId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع", [new RequestedReturn(2, 3m)]);
        await new DebitNotePostingService(_db).PostAsync(note);

        var lines = await builder.ReturnableLinesAsync(_companyId, invoice.Id);
        Assert.Equal(10m, lines[0].Returnable);
        Assert.Equal(7m, lines[2].Returnable);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — رصيدُ المورّد ينقص بقيمة المُرجَع لا أكثر:</b> ثلاثٌ
    /// بعشرين ومعها ضريبتُها. <b>فالإصلاحُ مسّ الصنفَ لا المبالغ.</b>
    /// </summary>
    [Fact]
    public async Task TheSupplierBalance_DropsByWhatWasReturned()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();
        var before = await BalanceAsync();

        var note = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع",
            [new RequestedReturn(2, 3m)]);
        await new DebitNotePostingService(_db).PostAsync(note);

        Assert.Equal(before - 69m, await BalanceAsync());   // ‏٣×٢٠ = ٦٠ + ١٥٪ = ٦٩
        LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> BalanceAsync() =>
        (await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId)).Balance;

    /// <summary>
    /// فاتورةٌ بثلاثة سطور: <b>الأوّلُ صنفٌ أساسيّ، والثاني خدمةٌ معفاة، والثالثُ صنفٌ
    /// آخر</b> — <b>وترتيبُها مقصود</b>: العطلُ كان يُنسب كلَّ مرتجعٍ إلى الأوّل.
    /// </summary>
    private async Task<PurchaseInvoice> PostInvoiceAsync()
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 2, 1), "SUP-1", vatDeductible: true);

        invoice.AddLine("صنفٌ أوّل", 10m, 10m, _firstId);
        invoice.AddLine("خدمة مالية معفاة", 1m, 500m, null,
            VatCategory.Exempt, "VATEX-SA-29", "خدمة مالية");
        invoice.AddLine("صنفٌ ثالث", 10m, 20m, _thirdId);

        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المرتجعات", "310123456700141", BusinessProfile.General);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        var first = Product.Create(_companyId, "P-1", "صنفٌ أوّل", "قطعة", ProductType.Stockable);
        var third = Product.Create(_companyId, "P-3", "صنفٌ ثالث", "قطعة", ProductType.Stockable);
        _db.Products.AddRange(first, third);
        await _db.SaveChangesAsync();

        _supplierId = supplier.Id;
        _firstId = first.Id;
        _thirdId = third.Id;
        _warehouseId = (await _db.Warehouses
            .SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
    }
}
