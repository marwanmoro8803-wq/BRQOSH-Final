using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Retail;

/// <summary>
/// (CEEF-229 · م2-١) <b>نقطة البيع بدور مُشغّلها الحقيقيّ.</b>
///
/// <para><b>العطل الذي كشفه إعطاءُ الاختبارات فاعلاً:</b> الإرسال التلقائيّ لمستند ZATCA كان
/// يستدعي <c>ZatcaSubmissionService.SubmitAsync</c> بحارس <c>EnsureCanPostAccounting</c> —
/// وذاك <b>يرفض أمين الصندوق</b>. وأمين الصندوق هو مُشغّل نقطة البيع فعلاً. فكانت كل بيعةٍ
/// يُجريها تُنتج مستنداً يُرفض إرساله، ويبتلع <c>ZatcaAutoSubmissionHelper</c> الرفضَ مع أخطاء
/// الشبكة في <c>catch (Exception)</c> صامت: البيعة تنجح، والإيصال يُطبع، والفاتورة لا تصل
/// الهيئة أبداً — وهو عين ما تسمّيه الخطة «صفر فواتير لدى الهيئة مقابل آلاف في الدفاتر»،
/// بالغاً إليه من بابٍ آخر: <b>دور المُشغّل</b>.</para>
///
/// <para><b>ولم يكشفه اختبارٌ قطّ</b> لأن <c>BRQOSH_TEST=1</c> كان يُطفئ طبقة الصلاحيات،
/// فكانت اختبارات نقطة البيع تعمل <b>بلا فاعل على الإطلاق</b> — ولم تُشغَّل نقطةُ البيع بدور
/// أمين صندوق ولا مرّة واحدة في تاريخ المستودع. ظهر العطل في اللحظة التي أُعطي فيها الاختبار
/// فاعله الحقيقيّ، لا قبلها.</para>
/// </summary>
public class CashierPosAuthorizationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _productId;
    private readonly long _shiftId;

    public CashierPosAuthorizationTests()
    {
        TestActor.Administrator();   // التهيئة عملٌ إداريّ — والبيع وحده يجري بدور أمين الصندوق
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700099", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();

        // (CEEF-229 · م2-١٣أ) هويةُ بائعٍ مكتملة — بلا هذا تُصدر منشأةٌ ناقصةٌ مستنداتٍ ترفضها الهيئة.
        ZatcaReadySeller.Complete(_db.Companies.Single(c => c.Id == _companyId));
        _db.SaveChanges();

        var zatca = _db.ZatcaSettings.Single(s => s.CompanyId == _companyId);
        zatca.Update(true, ZatcaEnvironment.Simulation, "",
            ZatcaReadySeller.Certificate.CertPath, ZatcaReadySeller.Certificate.KeyPath, "");
        _db.SaveChanges();

        var product = Product.Create(_companyId, "P1", "منتج", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        _db.SaveChanges();
        _productId = product.Id;

        var warehouse = _db.Warehouses.Single(w => w.CompanyId == _companyId);
        var opening = StockMovement.CreateDraft(
            _companyId, DateOnly.FromDateTime(DateTime.Today), StockMovementType.Opening,
            warehouse.Id, "رصيد افتتاحي");
        opening.AddLine(_productId, 50m, 40m);
        new StockMovementPostingService(_db).PostAsync(opening).GetAwaiter().GetResult();

        var shift = PosShift.Open(_companyId, "كاشير", 100m);
        _db.PosShifts.Add(shift);
        _db.SaveChanges();
        _shiftId = shift.Id;
    }

    private async Task<PosSaleResult> SellAsync() =>
        await new PosSaleService(_db).SellAsync(
            _companyId, _shiftId, null, [(_productId, 2m)], true, PosPaymentMethod.Cash, "كاشير");

    /// <summary>
    /// <b>القياس المركزيّ.</b> أمين الصندوق يبيع، ومستندُ فاتورته يُرسَل — لا يبقى معلّقاً.
    /// هذه هي الاختبارة التي كانت تسقط قبل الإصلاح.
    /// </summary>
    [Fact]
    public async Task CashierSale_ProducesASubmittedDocument()
    {
        TestActor.Cashier();

        var result = await SellAsync();

        Assert.NotNull(result.ZatcaDocument);
        Assert.Equal(ZatcaComplianceStatus.SimulatedLocally, result.ZatcaDocument!.ComplianceStatus);
        Assert.False(result.ZatcaDocument.ComplianceStatus.IsAcknowledgedByAuthority());
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> النتيجة نفسها لأمين الصندوق وللمحاسب. لو عاد الإرسال يتعلّق بدور
    /// البائع لاختلفت الحالتان — وهو بالضبط شكل العطل الذي أُصلح.
    /// </summary>
    [Fact]
    public async Task CashierAndAccountant_ReachTheSameComplianceStatus()
    {
        TestActor.Accountant();
        var byAccountant = await SellAsync();

        TestActor.Cashier();
        var byCashier = await SellAsync();

        Assert.Equal(byAccountant.ZatcaDocument!.ComplianceStatus,
                     byCashier.ZatcaDocument!.ComplianceStatus);
    }

    /// <summary>
    /// ومع ذلك <b>لا يتوسّع إذن أمين الصندوق</b>: الإرسال اليدويّ لمستندٍ بعينه يبقى قراراً
    /// محاسبياً مستقلاً ويُرفض. الإصلاح فصل «الأثر التابع لبيعةٍ مأذونة» عن «عمليةٍ يبدؤها
    /// المستخدم» — ولم يُخفّض الحارس.
    /// </summary>
    [Fact]
    public async Task Cashier_StillCannotSubmitADocumentManually()
    {
        TestActor.Cashier();
        var result = await SellAsync();

        await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(
            () => new Infrastructure.Zatca.ZatcaSubmissionService(_db)
                .SubmitAsync(result.ZatcaDocument!.Id));
    }

    /// <summary>
    /// والمُطّلع لا يبيع أصلاً — فالبيعة نفسها مرفوضة قبل أن يُطرح سؤال الفاتورة.
    /// </summary>
    [Fact]
    public async Task Viewer_CannotSellAtAll()
    {
        TestActor.Viewer();

        var ex = await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(SellAsync);
        Assert.Contains("صلاحية", ex.Message);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
