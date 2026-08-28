using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (CEEF-229 · م0-٦) مرتجع المبيعات يُرجع البضاعة فعلاً ويعكس تكلفتها.
///
/// <para><b>العطل الأصليّ:</b> <c>CreditNoteDialog</c> كان يستدعي <c>AddLine</c> قافزاً فوق
/// المعامل <c>productId</c> (يمرّر الفئة الضريبية باسمها)، وصفوف الحوار تُملأ بالنصّ الحرّ.
/// و<c>CreditNotePostingService</c> يشترط <c>line.ProductId is long</c> لإرجاع المخزون وعكس
/// تكلفة المبيعات. فكانت النتيجة أن <b>كل مرتجع مبيعات يُرجع صفر بضاعة ويعكس صفر تكلفة</b>:
/// الإيراد والذمم ينقصان والبضاعة لا تعود — عجز جرد دائم ومجمل ربح مُقلَّل.
/// والمفارقة أن مسار الشراء المقابل كان سليماً: إشعار المدين يمرّر الصنف ويخفّض المخزون فعلاً.</para>
///
/// <para><b>ولماذا لا يُشقّ الإصلاح:</b> تمريرُ الصنف وحده — وهو الإصلاح البديهيّ — يفجّر عطلاً
/// كان صاعقه منزوعاً: الخدمة تتحقّق من <b>قيمة</b> الإشعار ولا تتحقّق من <b>كمياته</b>. فسطرٌ
/// بمئة حبة من صنفٍ بيعت منه حبةٌ واحدة كان سيمرّ ويُدخل مخزوناً وهمياً. لذلك التحقّق الكمّي
/// والتمرير في التزام واحد.</para>
/// </summary>
public class CreditNoteStockReturnTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly long _productId;
    private readonly long _otherProductId;
    private readonly long _customerId;
    private readonly long _supplierId;

    public CreditNoteStockReturnTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة تجارية", "310123456700003", BusinessProfile.General)
            .GetAwaiter().GetResult();
        _companyId = company.Id;

        // المستودع الافتراضي يزرعه DatabaseInitializer — وإنشاء ثانٍ بـisDefault يجعل البيع
        // يقرأ مستودعاً والاستلام يكتب في آخر.
        var wh = _db.Warehouses.Single(w => w.CompanyId == _companyId);
        var product = Product.Create(_companyId, "P-1", "صنف مباع", "قطعة", ProductType.Stockable);
        var other = Product.Create(_companyId, "P-2", "صنف آخر", "قطعة", ProductType.Stockable);
        _db.Products.AddRange(product, other);
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();

        _warehouseId = wh.Id;
        _productId = product.Id;
        _otherProductId = other.Id;
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }

    private int _seedSequence;

    /// <summary>
    /// يُدخل مخزوناً <b>بمسار محاسبيّ حقيقيّ</b> — فاتورة مشتريات مرحّلة، لا حركة مخزنية خام.
    ///
    /// الفارق ليس تجميلياً: <c>InventoryService.PostDocumentMovementAsync</c> يحرّك الكمية
    /// <b>بلا قيد محاسبيّ</b>، فيدخل المخزون الدفترَ المساعد ولا يمسّ حساب 1107 في الأستاذ.
    /// وهذا بالضبط ما كشفته هوية المخزون حين استُعملت التهيئة الخام أول مرة — أداة القياس
    /// عضّت على الاختبار نفسه قبل أن تعضّ على المنتج.
    /// </summary>
    private async Task ReceiveStockAsync(long productId, decimal qty, decimal unitCost)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 1, 1), $"SUP-{++_seedSequence}", true);
        invoice.AddLine("توريد", qty, unitCost, productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<SalesInvoice> SellAsync(long productId, decimal qty, decimal price)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("صنف مباع", qty, price, productId);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<decimal> OnHandAsync(long productId) =>
        (await _db.InventoryBalances
            .Where(b => b.CompanyId == _companyId && b.ProductId == productId)
            .ToListAsync())
        .Sum(b => b.QuantityOnHand);

    // ── الاتجاه الأول: المرتجع يُرجع البضاعة فعلاً ──────────────────────────

    /// <summary>
    /// <b>القياس الأساسيّ.</b> بيع 20 حبة تكلفتها 90، ثم إرجاعها كاملةً:
    /// المخزون يعود إلى 100، وسطر عكس تكلفة المبيعات يساوي <b>1,800</b> بالضبط،
    /// وهوية المخزون تصمد. قبل الإصلاح: المخزون 80 والعكس صفر.
    /// </summary>
    [Fact]
    public async Task PostedReturn_RestoresStock_AndReversesCogs()
    {
        await ReceiveStockAsync(_productId, 100m, 90m);
        var invoice = await SellAsync(_productId, 20m, 150m);

        Assert.Equal(80m, await OnHandAsync(_productId));

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع بضاعة سليمة");
        note.AddLine("صنف مباع", 20m, 150m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        // البضاعة عادت
        Assert.Equal(100m, await OnHandAsync(_productId));

        // وتكلفتها عُكست بالتكلفة الأصلية للبيع لا بمتوسط اليوم
        var entry = await _db.JournalEntries
            .Where(j => j.CompanyId == _companyId && j.SourceDocument!.StartsWith("CreditNote:"))
            .SingleAsync();
        var cogsAccountId = (await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold)).Id;
        var cogsCredit = entry.Lines.Where(l => l.AccountId == cogsAccountId).Sum(l => l.Credit);
        Assert.Equal(1800.00m, cogsCredit);

        // والدفتر المساعد لم يفارق الأستاذ
        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>مرتجع جزئيّ: النصف يعود، والنصف الآخر يبقى قابلاً للإرجاع لاحقاً.</summary>
    [Fact]
    public async Task PartialReturn_RestoresOnlyReturnedQuantity()
    {
        await ReceiveStockAsync(_productId, 50m, 40m);
        var invoice = await SellAsync(_productId, 30m, 100m);

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع جزئي");
        note.AddLine("صنف مباع", 10m, 100m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal(30m, await OnHandAsync(_productId));   // 50 − 30 + 10
        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>سطر خدميّ بلا صنف يبقى مقبولاً — لا يُرجع مخزوناً ولا يُرفض.</summary>
    [Fact]
    public async Task ServiceLineWithoutProduct_IsStillAccepted()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("خدمة تركيب", 1m, 500m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "إلغاء خدمة");
        note.AddLine("خدمة تركيب", 1m, 500m);

        await new CreditNotePostingService(_db).PostAsync(note);   // لا يرمي

        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    // ── الاتجاه الثاني: التحقّق الكمّي يعضّ ─────────────────────────────────

    /// <summary>
    /// إرجاع كمية تتجاوز المباعة يُرفض. هذا هو العطل الذي كان صاعقه منزوعاً — ولولا هذا الحارس
    /// لفجّره تمريرُ الصنف وحده.
    /// </summary>
    [Fact]
    public async Task ReturnExceedingSoldQuantity_IsRejected()
    {
        await ReceiveStockAsync(_productId, 200m, 600m);
        var invoice = await SellAsync(_productId, 1m, 1000m);

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع متضخّم");
        note.AddLine("صنف مباع", 100m, 10m, _productId);   // القيمة 1,000 تمرّ، والكمية لا

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new CreditNotePostingService(_db).PostAsync(note));
        Assert.Contains("تتجاوز المتاح للإرجاع", ex.Message);
    }

    /// <summary>صنف لم يرد في الفاتورة الأصلية يُرفض.</summary>
    [Fact]
    public async Task ReturnOfProductNotOnInvoice_IsRejected()
    {
        await ReceiveStockAsync(_productId, 50m, 40m);
        await ReceiveStockAsync(_otherProductId, 50m, 40m);
        var invoice = await SellAsync(_productId, 10m, 100m);

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع صنف غريب");
        note.AddLine("صنف آخر", 5m, 100m, _otherProductId);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new CreditNotePostingService(_db).PostAsync(note));
        Assert.Contains("لم يرد في الفاتورة الأصلية", ex.Message);
    }

    /// <summary>
    /// التجاوز مقسَّماً على سطرين لنفس الصنف يُرفض أيضاً — الفحص يجمع بالصنف قبل المقارنة،
    /// وإلا كان تقسيمُ الكمية باباً خلفياً يلتفّ على الحارس.
    /// </summary>
    [Fact]
    public async Task ExcessSplitAcrossTwoLines_IsStillRejected()
    {
        await ReceiveStockAsync(_productId, 100m, 50m);
        var invoice = await SellAsync(_productId, 10m, 100m);

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع مقسَّم");
        // القيمة 12 × 40 = 480 + ضريبة = 552 ≤ 1,150 فتمرّ بوابة القيمة؛ والكمية 12 > 10 فتُرفض.
        // لولا فصل السيناريوهين لظنّ القارئ أن بوابة القيمة هي التي عضّت.
        note.AddLine("صنف مباع", 6m, 40m, _productId);
        note.AddLine("صنف مباع", 6m, 40m, _productId);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new CreditNotePostingService(_db).PostAsync(note));
        Assert.Contains("تتجاوز المتاح للإرجاع", ex.Message);
    }

    /// <summary>
    /// مرتجعان متتاليان يستنفدان الكمية: الثاني يمرّ ما دام ضمن المتبقّي، والثالث يُرفض.
    /// يُثبت أن الفحص يخصم <b>ما أُرجع سابقاً</b> لا كمية الفاتورة وحدها.
    /// </summary>
    [Fact]
    public async Task SecondReturn_CountsAgainstWhatWasAlreadyReturned()
    {
        await ReceiveStockAsync(_productId, 100m, 50m);
        var invoice = await SellAsync(_productId, 10m, 100m);

        var first = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع أول");
        first.AddLine("صنف مباع", 6m, 40m, _productId);
        await new CreditNotePostingService(_db).PostAsync(first);

        var second = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 11), "مرتجع ثانٍ");
        second.AddLine("صنف مباع", 5m, 40m, _productId);   // 6 + 5 = 11 > 10 كمّياً، والقيمة ضمن الحدّ

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new CreditNotePostingService(_db).PostAsync(second));
        Assert.Contains("تتجاوز المتاح للإرجاع", ex.Message);

        Assert.Equal(96m, await OnHandAsync(_productId));   // 100 − 10 + 6
        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
