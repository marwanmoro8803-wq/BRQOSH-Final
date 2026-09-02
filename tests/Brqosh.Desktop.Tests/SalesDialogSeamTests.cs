using System.Windows.Threading;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٢٥) <b>الوصلة: ما تحمله الشبكة يصل إلى الأستاذ.</b>
///
/// <para><b>الفجوة التي يسدّها هذا الملف</b> — وقد صرّحتُ بها في تقريرين متتاليين: إصلاحُ
/// عمود اختيار الصنف (م2-٢٠) كان <b>مُثبَتاً طرفَين ومُستنتَجاً في الوصلة</b>. الطرف الأماميّ
/// مقيسٌ بحارس <c>DocumentLineProductColumnTests</c> (العمود موجودٌ ومربوطٌ بـ<c>ProductId</c>)،
/// والطرف الخلفيّ مقيسٌ باختبارات التطبيق (تُمرّر <c>ProductId</c> برمجياً فتتحرّك الكمية
/// وتُقيَّد التكلفة). <b>وما بينهما كان استنتاجاً</b>: هل ما يكتبه العمود في الصفّ يصل فعلاً
/// إلى الكيان؟</para>
///
/// <para>وهي بالضبط الحلقة التي انكسرت: <c>ProductId</c> كان يُقرأ عند البناء ولا يكتبه أحد،
/// فمرّ <c>null</c> دائماً — <b>وكلا الطرفين كان سليماً</b>. فلا يكفي قياسُ الطرفين.</para>
///
/// <para><b>والقياس هنا حقيقيّ لا محاكاة:</b> يُنشئ الحوار على خيط STA، ويملأ صفوفه كما يملؤها
/// المستخدم، ثم يستدعي <b>بانيَ المستند نفسه</b> الذي يستدعيه زرُّ الحفظ، ويُرحّل الناتج
/// بالخدمة الحقيقية على قاعدة بياناتٍ حيّة — ثم يقيس المخزون والأستاذ.</para>
/// </summary>
public class SalesDialogSeamTests : IDisposable
{
    private static readonly Dispatcher UiDispatcher = StartUiThread();

    private readonly SqliteConnection _keepAlive;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private readonly long _productId;
    private readonly long _warehouseId;

    /// <summary>
    /// (CEEF-229 · م2-٦) خيطُ الواجهة مشتركٌ الآن — انظر <see cref="WpfTestHost"/>.
    /// وكان كلُّ صنفٍ يُنشئ خيطاً وتطبيقاً، وWPF لا يسمح إلا بتطبيقٍ واحدٍ في العملية،
    /// فمن سبق ملكه — وضبطُ خصائصه من خيطٍ آخر يُسقط مُضيفَ الاختبارات كلَّه.
    /// </summary>
    private static Dispatcher StartUiThread() => WpfTestHost.Dispatcher;

    public SalesDialogSeamTests()
    {
        // قاعدةٌ في الذاكرة مشتركة: كل سياقٍ جديد يرى البيانات نفسها ما دام هذا الاتصال مفتوحاً.
        _keepAlive = new SqliteConnection("Data Source=seam;Mode=Memory;Cache=Shared");
        _keepAlive.Open();

        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=seam;Mode=Memory;Cache=Shared");
        _db.Database.EnsureCreated();

        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700099", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;

        var customer = Customer.Create(_companyId, "عميل أجل", "311111111100003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P-1", "بضاعة", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        _db.SaveChanges();
        _customerId = customer.Id;
        _productId = product.Id;

        _warehouseId = _db.Warehouses.Single(w => w.CompanyId == _companyId).Id;

        // مخزونٌ افتتاحيّ: 50 حبة بتكلفة 60.
        var opening = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 5), StockMovementType.Opening, _warehouseId, "افتتاحيّ");
        opening.AddLine(_productId, 50m, 60m);
        new StockMovementPostingService(_db).PostAsync(opening).GetAwaiter().GetResult();
    }

    /// <summary>يملأ صفوف الحوار كما يفعل المستخدم، ثم يبني المستند ببانيه نفسه.</summary>
    private SalesInvoice BuildThroughTheDialog(long? productId, decimal qty, decimal price) =>
        UiDispatcher.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            dialog.RowsForTests.Add(new SalesInvoiceDialog.LineRow
            {
                ProductId = productId,     // ما يكتبه عمود الاختيار
                Description = "بضاعة",
                Quantity = qty,
                UnitPrice = price,
            });

            return dialog.BuildInvoiceFromRows(_companyId, _customerId, new DateOnly(2026, 2, 1));
        });

    /// <summary>
    /// (CEEF-231 · ط1-٢ب) <b>والخصمُ المكتوبُ في الشبكة يصل إلى الوعاء الضريبيّ.</b>
    ///
    /// <para><b>ما كان:</b> محرّكُ الخصم مبنيٌّ منذ <c>ط1-٢</c> بخمسة اختبارات —
    /// <b>ولا حقلَ في أيّ شاشةٍ يصل إليه</b>. فيُدخل البائعُ السعرَ مخصوماً بيده،
    /// <b>فتختفي النسبةُ من المستند الإلكترونيّ</b> (<c>AllowanceCharge</c>) فلا تعرفها
    /// الهيئة، <b>وتختفي من كلّ تقريرِ ربحيّة</b> — فيبدو الصنفُ مبيعاً بسعرٍ أقلَّ لا
    /// مخصوماً.</para>
    ///
    /// <para><b>والقياسُ بمثالٍ لا ينقسم بلا كسر</b>: عشرةٌ بمئةٍ وخصمُ ١٠٪ ⟵ صافٍ ٩٠٠،
    /// <b>وضريبةٌ ١٣٥ لا ١٥٠</b>. والفرقُ خمسةَ عشرَ ريالاً تُصرَّح للهيئة عن مالٍ لم
    /// يُقبَض.</para>
    /// </summary>
    [Fact]
    public void ALineDiscountTypedInTheGrid_ReachesTheTaxableBase()
    {
        var invoice = UiDispatcher.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            dialog.RowsForTests.Add(new SalesInvoiceDialog.LineRow
            {
                ProductId = _productId,
                Description = "بضاعة",
                Quantity = 10m,
                UnitPrice = 100m,
                DiscountPercent = 10m,
            });

            return dialog.BuildInvoiceFromRows(_companyId, _customerId, new DateOnly(2026, 2, 1));
        });

        Assert.Equal(10m, invoice.Lines.Single().DiscountPercent);
        Assert.Equal(900m, invoice.Subtotal);
        Assert.Equal(135m, invoice.VatAmount);
    }

    /// <summary>
    /// <b>القياس المركزيّ — السلسلة كاملةً.</b> صفٌّ في الشبكة يحمل صنفاً ⟵ مستندٌ يحمله ⟵
    /// ترحيلٌ يُنقص المخزون ويُقيّد التكلفة. هذه هي الوصلة التي كانت مقطوعة.
    /// </summary>
    [Fact]
    public async Task AGridRowCarryingAProduct_ReachesTheLedgerAndMovesStock()
    {
        var invoice = BuildThroughTheDialog(_productId, 10m, 100m);

        Assert.Equal(_productId, invoice.Lines.Single().ProductId);

        var inv = new InventoryService(_db);
        var before = await inv.GetOnHandAsync(_companyId, _productId, _warehouseId);

        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(before - 10m, await inv.GetOnHandAsync(_companyId, _productId, _warehouseId));

        // وتكلفة البضاعة المباعة قُيّدت: 10 × 60.
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountCode == "5101" && l.Debit == 600m);
    }

    /// <summary>
    /// <b>وهذا هو العطل القديم مُعاداً عمداً:</b> صفٌّ بلا صنف — كما كان يُنتجه الحوار قبل
    /// إضافة العمود — يُرحَّل بلا حركة مخزون وبلا تكلفة، ومجملُ الربح يساوي الإيراد كاملاً.
    /// أُبقي مقيساً كي يُرى الفرق، لا كي يُقبل.
    /// </summary>
    [Fact]
    public async Task AGridRowWithoutAProduct_MovesNothing_AndOverstatesProfit()
    {
        var invoice = BuildThroughTheDialog(productId: null, 10m, 100m);

        var inv = new InventoryService(_db);
        var before = await inv.GetOnHandAsync(_companyId, _productId, _warehouseId);

        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(before, await inv.GetOnHandAsync(_companyId, _productId, _warehouseId));

        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == "5101");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> الصفوف الفارغة تُتخطّى — فالباني لا يُنتج سطوراً من لا شيء،
    /// ولا يُحسب مرورُ الاختبار الأول على شبكةٍ فارغة.
    /// </summary>
    [Fact]
    public void EmptyRows_ProduceNoLines()
    {
        var invoice = UiDispatcher.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            dialog.RowsForTests.Add(new SalesInvoiceDialog.LineRow());
            return dialog.BuildInvoiceFromRows(_companyId, _customerId, new DateOnly(2026, 2, 1));
        });

        Assert.Empty(invoice.Lines);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }
}
