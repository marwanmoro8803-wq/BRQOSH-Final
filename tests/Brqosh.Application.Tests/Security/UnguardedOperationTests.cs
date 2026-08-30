using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-229 · م2-١) <b>الجدران المفقودة، لا الأبواب المُقفَلة.</b>
///
/// <para>حملة الصلاحيات بدأت بسؤالٍ ضيّق: أين تُفرض الصلاحيات وكيف تُقاس؟ فأنتجت خريطةً كاملة
/// لسبعين موضعاً — <b>وصمتت عن العمليات التي لا تفرض شيئاً أصلاً</b>. والسؤال المعاكس أثمن:
/// أيّ عمليةٍ حسّاسة سائبة بينما <b>نظيرتها المباشرة محروسة</b>؟ فتناظرُ الخدمتين هو الدليل:
/// حين تُقفل إحدى شقيقتين في الملف نفسه وتُترك الأخرى، فالغياب سهوٌ لا قرار.</para>
///
/// <para>هذه الاختبارات تقيس الثلاث التي أُغلقت. ولم تكن لتُكتب لولا نزعُ التجاوز: ما دام كل
/// اختبارٍ يعمل بلا فاعل، فالفرق بين «محروس» و«سائب» غير مرئيّ أصلاً.</para>
/// </summary>
public class UnguardedOperationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public UnguardedOperationTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();
    }

    // ── رفض مستند في دورة الاعتماد ─────────────────────────────────────────

    private async Task<long> SubmittedRequestAsync()
    {
        var supplier = Supplier.Create(_companyId, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var request = PurchaseRequest.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 7, 9), "معدات");
        request.AddLine("طابعة", 2m, 1500m);
        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        TestActor.Accountant();
        await new PurchaseRequestWorkflowService(_db, new WorkflowEngine(_db))
            .SubmitAsync(request);

        return await _db.WorkflowInstances
            .Where(w => w.CompanyId == _companyId)
            .Select(w => w.Id).SingleAsync();
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> كانت الموافقة محروسة والرفضُ سائباً في <b>الملف نفسه</b> —
    /// فأيّ «مُطّلع» يستطيع قتل أمر شراءٍ أو سند صرفٍ نهائياً. وهو حرمانُ خدمةٍ ماليّ: تعطيل
    /// دورة الشراء والصرف كلّها بيد مستخدمٍ لا يملك إلا القراءة.
    /// </summary>
    [Fact]
    public async Task Viewer_CannotRejectADocumentAwaitingApproval()
    {
        var instanceId = await SubmittedRequestAsync();

        TestActor.Viewer();
        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new WorkflowEngine(_db).RejectAsync(instanceId, "viewer", "لا أوافق"));

        // ورسالة دورة الاعتماد تسمّي الخطوة المطلوبة لا «الصلاحية» عموماً — وهي أدقّ.
        Assert.Contains("لا يملك موافقة", ex.Message);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> ومَن يملك الموافقة يملك الرفض — فالإصلاح ساوى بينهما ولم يُقفل
    /// الرفض على الجميع. لولا هذه لكان حارسٌ يرفض الكلّ يُمرّر الاختبار السابق.
    /// </summary>
    [Fact]
    public async Task WhoeverMayApprove_MayAlsoReject()
    {
        var instanceId = await SubmittedRequestAsync();

        TestActor.Accountant();   // الدور نفسه الذي يوافق في خطوة المدير
        var instance = await new WorkflowEngine(_db).RejectAsync(instanceId, "accountant", "مرفوض");

        Assert.NotNull(instance);
    }

    // ── إعادة تعيين كلمة المرور ────────────────────────────────────────────

    /// <summary>
    /// كانت الدالّة العامّة الوحيدة في صنفها بلا حارس، وأخواتها الثلاث تفرض صلاحية المدير.
    /// وهي تُعيد تعيين كلمة مرور <b>أيّ</b> مستخدم — بمن فيهم مدير النظام — بلا كلمة مرورٍ
    /// حالية ولا رمز تحقّق: بدائيّةُ تصعيد امتيازٍ صافية لمن بلغها.
    /// </summary>
    [Theory]
    [InlineData(Domain.Security.UserRole.Viewer)]
    [InlineData(Domain.Security.UserRole.Cashier)]
    [InlineData(Domain.Security.UserRole.Accountant)]
    public async Task NonAdministrator_CannotResetAnotherUsersPassword(Domain.Security.UserRole role)
    {
        SecurityContext.CurrentRole = role;

        await Assert.ThrowsAsync<AccountingException>(
            () => new UserManagementService(_db).ResetPasswordAsync(_companyId, "admin", "Str0ng!Passw0rd#2026"));
    }

    // ── إطلاق أمر التشغيل ──────────────────────────────────────────────────

    /// <summary>
    /// جارتاها في الملف نفسه محروستان، وهي سائبة — رغم أنها <b>تستهلك رقماً من متسلسلة
    /// المستندات</b>، وفجوةُ الترقيم أثرٌ لا يُردم بإلغاء.
    /// </summary>
    [Fact]
    public async Task Viewer_CannotReleaseAWorkOrder_AndConsumeADocumentNumber()
    {
        var product = Domain.Inventory.Product.Create(
            _companyId, "FG-1", "منتج تام", "قطعة", Domain.Inventory.ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var bom = BillOfMaterials.Create(_companyId, "BOM-1", "قائمة مواد", product.Id);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 7, 9), 10m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        TestActor.Viewer();
        await Assert.ThrowsAsync<AccountingException>(
            () => new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id));

        // ولا يُستهلك رقم: الأمر ما زال مسوّدة بلا رقم.
        _db.ChangeTracker.Clear();
        var reloaded = await _db.WorkOrders.SingleAsync(w => w.Id == order.Id);
        Assert.True(string.IsNullOrWhiteSpace(reloaded.OrderNumber));
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
