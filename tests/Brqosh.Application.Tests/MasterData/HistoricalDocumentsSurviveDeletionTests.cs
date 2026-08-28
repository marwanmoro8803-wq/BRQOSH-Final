using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.MasterData;

/// <summary>
/// (CEEF-229 · م2-١٨) <b>المستند التاريخيّ لا يموت بموت بياناته الأساسية.</b>
///
/// <para>البيانات الأساسية تُحذف حذفاً ناعماً، ومرشّح الاستعلام العامّ يُخفي المحذوف عن
/// <c>db.Products</c> و<c>db.Customers</c>. وهذا صحيحٌ للقوائم والاختيار — <b>وخاطئٌ تماماً
/// للمستندات المُرحَّلة</b>: الفاتورة صدرت يوم كان العميل موجوداً، والقانون يُلزم بإصدار نسخةٍ
/// منها بعد سنوات.</para>
///
/// <para><b>وثلاث نتائج مقيسة لقراءة المرشَّح في مسارٍ تاريخيّ:</b></para>
/// <para>(١) <b>الطباعة تسقط:</b> <c>SingleAsync</c> على عميلٍ محذوف يرمي — فتصير فاتورةٌ
/// ضريبية سعودية <b>غير قابلة للطباعة أبداً</b>، والرسالة إنجليزية داخل واجهةٍ عربية.</para>
/// <para>(٢) <b>الإشعار الدائن يتخطّى السطر صامتاً:</b> القاموس لا يحوي الصنف، فيُتخطّى بلا
/// خطأ — تُعكس الذمم والضريبة، <b>ولا تعود البضاعة ولا تُعكس تكلفتها</b>. العميل استردّ ماله،
/// والبضاعة على الرفّ، والنظام لا يعلم. لا يكشفه إلا جردٌ بعد شهور.</para>
/// <para>(٣) <b>إلغاء الفاتورة يستحيل:</b> مسار التكلفة يرمي، فترتدّ المعاملة وتبقى الفاتورة
/// مرحّلة إلى الأبد.</para>
///
/// <para>وقد أُغلق البابُ الأشيع في م2-١٧ (الإيقاف لم يعد حذفاً)، لكنّ الحذف الناعم الحقيقيّ
/// باقٍ — فهذه الاختبارات تحذف <b>صراحةً</b> لتقيس الهشاشة نفسها لا عرَضها.</para>
/// </summary>
public class HistoricalDocumentsSurviveDeletionTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private readonly long _productId;

    public HistoricalDocumentsSurviveDeletionTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();

        var customer = Customer.Create(_companyId, "عميل قديم", "311111111100003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        _db.SaveChanges();
        _customerId = customer.Id;
        _productId = product.Id;

        var warehouse = _db.Warehouses.Single(w => w.CompanyId == _companyId);
        var opening = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 5), StockMovementType.Opening, warehouse.Id, "افتتاحيّ");
        opening.AddLine(_productId, 100m, 60m);
        new StockMovementPostingService(_db).PostAsync(opening).GetAwaiter().GetResult();
    }

    private async Task<SalesInvoice> PostedInvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("صنف", 10m, 100m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    /// <summary>حذفٌ ناعم صريح — لا عبر الإيقاف، فالإيقاف لم يعد يحذف (م2-١٧).</summary>
    private async Task SoftDeleteAsync(string table, long id)
    {
        // اسم الجدول ثابتٌ في نصّ الاختبار والمعرّف رقم — لا مدخل مستخدم هنا.
        var sql = $"UPDATE {table} SET DeletedAtUtc = '2026-02-10 00:00:00' WHERE Id = {id}";
        await _db.Database.ExecuteSqlRawAsync(sql);
        _db.ChangeTracker.Clear();
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> فاتورةٌ ضريبية يجب أن تبقى قابلة للطباعة بعد حذف عميلها —
    /// وهي وثيقةٌ يُلزم النظام السعوديّ بالاحتفاظ بها وإصدار نسخٍ منها.
    /// </summary>
    [Fact]
    public async Task ATaxInvoice_StaysPrintable_AfterItsCustomerIsDeleted()
    {
        var invoice = await PostedInvoiceAsync();
        await SoftDeleteAsync("customers", _customerId);

        var result = await new DocumentPrintService(
                _db,
                new Infrastructure.Security.AuditService(_db),
                new Infrastructure.Treasury.ExchangeRateService(_db))
            .ExportSalesInvoiceAsync(_companyId, invoice.Id, "admin");

        Assert.True(result.PdfBytes.Length > 0);
    }

    /// <summary>
    /// <b>وأخطرها صمتاً:</b> مرتجعٌ لصنفٍ محذوف يجب أن يُرجع البضاعة ويعكس تكلفتها — أو
    /// يُرفض صراحةً. أمّا أن يُرحَّل ناقصاً بلا كلمة فذاك فرقٌ بين الأستاذ والمخزون لا يُكتشف.
    /// </summary>
    [Fact]
    public async Task ACreditNote_ReturnsStock_EvenWhenTheProductWasDeleted()
    {
        var invoice = await PostedInvoiceAsync();
        var warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId)).Id;
        var before = await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, warehouseId);

        await SoftDeleteAsync("products", _productId);

        var note = CreditNote.CreateDraft(
            _companyId, _customerId, invoice.Id, new DateOnly(2026, 2, 15), "مرتجع");
        note.AddLine("صنف", 4m, 100m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var after = await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, warehouseId);
        Assert.Equal(before + 4m, after);
    }

    /// <summary>
    /// وإلغاء فاتورةٍ مرحّلة يبقى ممكناً — فلا تبقى الفاتورة مرحّلةً إلى الأبد لسببٍ لا يفهمه
    /// المالك ولا يستطيع إصلاحه.
    /// </summary>
    [Fact]
    public async Task APostedInvoice_StaysCancellable_AfterItsProductIsDeleted()
    {
        var invoice = await PostedInvoiceAsync();
        await SoftDeleteAsync("products", _productId);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 2, 20), "admin");

        var reloaded = await _db.SalesInvoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(SalesInvoiceStatus.Cancelled, reloaded.Status);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> المرشّح ما زال يعمل حيث يجب — قوائمُ الاختيار لا تعرض المحذوف.
    /// فالإصلاح فتح المسارات التاريخية ولم يُلغِ الحذف الناعم.
    /// </summary>
    [Fact]
    public async Task DeletedMasterData_StaysHiddenFromPickLists()
    {
        await SoftDeleteAsync("products", _productId);
        await SoftDeleteAsync("customers", _customerId);

        Assert.Empty(await _db.Products.Where(p => p.Id == _productId).ToListAsync());
        Assert.Empty(await _db.Customers.Where(c => c.Id == _customerId).ToListAsync());
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
