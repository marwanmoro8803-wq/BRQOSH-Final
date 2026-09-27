using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-230 · ط6-٨) <b>استعلامان لهما شاشتان ولا يقيسهما شيء.</b>
///
/// <para><b>وكُشفا بالقياس لا بالقراءة</b>: مسحُ ١٣٣ خدمةً واستعلاماً في البنية التحتية
/// وبحثُ كلٍّ منها في حزم الاختبار — <b>فخرج اثنان بلا ذكرٍ واحد</b>. وكلاهما مربوطٌ
/// بشاشةٍ يفتحها المالك.</para>
///
/// <para><b>وفي كلٍّ منهما عطلٌ حقيقيّ</b> — ولم يكن ليُكتشف، لأنّ ما لا يُقاس لا يُرى
/// حتى يقع عند المستخدم.</para>
/// </summary>
public class UnmeasuredQueryTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public UnmeasuredQueryTests()
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
    /// <b>القياسُ الحاكم: تقريرُ نقطة البيع لا يعرض إلا بيعَ نقطة البيع.</b>
    ///
    /// <para><b>والشاشةُ تَعِد بذلك بنصّها</b>: «ملخص يومي لمعاملات نقاط البيع».
    /// <b>والاستعلامُ كان يقرأ كلَّ فواتير المبيعات</b> — فتدخل مبيعاتُ الآجل للتجّار
    /// في ملخّص الدرج.</para>
    ///
    /// <para><b>وأثرُه أنّ مطابقةَ الصندوق لا تنجح أبداً</b>: أمينُ الصندوق يعدّ ما في
    /// الدرج ويقارنه بالتقرير، <b>والفرقُ فاتورةُ عميلٍ آجلةٍ لم تمرّ بالدرج قطّ</b> —
    /// فيُطارَد فرقٌ لا وجودَ له، أو يُتَّهم أمينُ الصندوق بعجزٍ ليس منه.</para>
    /// </summary>
    [Fact]
    public async Task PosDailySummary_ExcludesInvoicesThatNeverTouchedTheTill()
    {
        await SetupAsync();

        // ‏بيعٌ آجلٌ لتاجر — **لم يمرّ بالدرج**.
        var credit = SalesInvoice.CreateDraft(_companyId, _customerId,
            DateOnly.FromDateTime(DateTime.Today));
        credit.AddLine("بيع آجل", 1m, 5_000m);
        await new SalesInvoicePostingService(_db).PostAsync(credit);

        var rows = await new PosReportQuery(_db).DailySummaryAsync(_companyId);

        Assert.True(rows.Count == 0 || rows.All(r => r.TotalSales == 0m),
            "الملخّصُ عرض بيعاً لم يمرّ بنقطة البيع — فمطابقةُ الدرج لا تنجح أبداً.");
    }

    /// <summary>
    /// <b>وبيعُ نقطة البيع يظهر فيه.</b>
    ///
    /// <para><b>وحارسٌ للحارس</b>: لو صار الاستعلامُ يُخرج فارغاً دائماً لَمرّ الاختبارُ
    /// أعلاه <b>وهو لا يقيس شيئاً</b>.</para>
    /// </summary>
    [Fact]
    public async Task PosDailySummary_StillShowsTillSales()
    {
        await SetupAsync();

        var shift = Domain.Retail.PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        var product = Product.Create(_companyId, "P-POS", "صنف", "حبة",
            ProductType.Service, salePrice: 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await new PosSaleService(_db).SellAsync(_companyId, shift.Id, null,
            [(product.Id, 2m)], true, Domain.Retail.PosPaymentMethod.Cash, "كاشير");

        var rows = await new PosReportQuery(_db).DailySummaryAsync(_companyId);

        Assert.Single(rows);
        Assert.Equal(1, rows[0].Transactions);
    }

    /// <summary>
    /// <b>وشاشةُ الإنتاج تحت التنفيذ لا تسقط بقائمةِ موادٍ محذوفة.</b>
    ///
    /// <para><b>وكان <c>bomMap[o.BomId]</c> بلا حارس</b>: مُرشِّحُ الحذف الناعم يُخفي
    /// قائمةَ المواد، <b>وأمرُ الإنتاج المُطلَق يبقى</b> — فيرمي القاموسُ
    /// <c>KeyNotFoundException</c> <b>وتسقط الشاشةُ كلُّها</b>.</para>
    ///
    /// <para><b>وهي حالةٌ واقعةٌ على قواعدَ قائمة</b>: التعبئةُ التي أُوقفت في <c>م0-١٣</c>
    /// <b>كانت قد جرت على كلّ قاعدةٍ قبل إيقافها</b> — فكلُّ قائمةِ موادٍ أُوقفت قبلها
    /// تحمل تاريخَ حذفٍ إلى الأبد. <b>ومصنعٌ يفتح شاشةَ الإنتاج فلا تفتح</b> لا يعرف
    /// السبب.</para>
    /// </summary>
    [Fact]
    public async Task WipList_SurvivesASoftDeletedBillOfMaterials()
    {
        await SetupAsync();

        var product = Product.Create(_companyId, "FG-1", "منتج تام", "حبة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var bom = BillOfMaterials.Create(_companyId, "BOM-1", "قائمة", product.Id);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 5, 1), 10m);
        order.Release("WO-1", bom);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        // ‏حذفٌ ناعمٌ لقائمة المواد — والأمرُ المُطلَقُ باقٍ.
        _db.Entry(bom).Property(b => b.DeletedAtUtc).CurrentValue = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var rows = await new WipQuery(_db).ListOpenAsync(_companyId);

        // ‏**والأمرُ يبقى مرئياً** — فهو قيدُ الإنتاج فعلاً، وإخفاؤه يُخفي بضاعةً في المصنع.
        Assert.Single(rows);
        Assert.Equal(10m, rows[0].PlannedQuantity);
    }

    /// <summary>
    /// <b>والحالةُ العادية تُعرض كما كانت.</b>
    /// </summary>
    [Fact]
    public async Task WipList_ShowsReleasedOrdersWithTheirBom()
    {
        await SetupAsync();

        var product = Product.Create(_companyId, "FG-2", "منتج", "حبة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var bom = BillOfMaterials.Create(_companyId, "BOM-2", "قائمة ثانية", product.Id);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 5, 1), 7m);
        order.Release("WO-2", bom);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        var rows = await new WipQuery(_db).ListOpenAsync(_companyId);

        Assert.Single(rows);
        Assert.Equal("قائمة ثانية", rows[0].BomName);
        Assert.Equal("منتج", rows[0].ProductName);
    }

    /// <summary>
    /// <b>ولا يظهر في «تحت التنفيذ» إلا ما أُطلق فعلاً.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت هذا</b>: اختباراتي كلُّها بأوامرَ مُطلَقة، <b>فنزعُ
    /// مُرشِّح الحالة لم يكسر شيئاً</b>. وأمرٌ مسودّةٌ لم يبدأ إنتاجُه — <b>وعرضُه تحت
    /// التنفيذ يُظهر في المصنع بضاعةً لم تدخله</b>، فتُخطَّط الطاقةُ على شغلٍ لم يُطلَق.</para>
    /// </summary>
    [Fact]
    public async Task WipList_ShowsOnlyReleasedOrders()
    {
        await SetupAsync();

        var product = Product.Create(_companyId, "FG-3", "منتج ثالث", "حبة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var bom = BillOfMaterials.Create(_companyId, "BOM-3", "قائمة ثالثة", product.Id);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        // ‏مسودّةٌ لم تُطلَق — **ولم يبدأ إنتاجُها**.
        var draft = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 5, 1), 4m);
        _db.WorkOrders.Add(draft);
        await _db.SaveChangesAsync();

        var rows = await new WipQuery(_db).ListOpenAsync(_companyId);

        Assert.Empty(rows);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700121", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "تاجر");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
