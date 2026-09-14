using System.Reflection;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Workflow;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (م-٥ · ن-٥٢) <b>هويّةُ السطر الضريبيّةُ تعبر التحويلَ من مستندٍ إلى مستند.</b>
///
/// <para><b>ما كان قبلها:</b> شاشةُ أمر الشراء تعرض عمودَ الفئة الضريبيّة
/// (<c>PurchaseOrderDialog.xaml:60</c> يربط <c>VatCategoryOptions.All</c>، و<c>VatCategoryUiTests</c>
/// تُثبّته بنداً)، فيختار المستخدم «معفاة» ويحفظ. <b>ثمّ يُحوَّل الأمرُ إلى فاتورة، فيُعاد
/// تصنيفُ السطر «أساسيّة» صامتاً</b>: <c>PurchaseOrderService.cs:60</c> كان يستدعي
/// <c>invoice.AddLine(Description, Quantity, UnitPrice, ProductId)</c> — أربعةُ وسائطَ من سبعة —
/// بينما <c>PurchaseInvoice.AddLine</c> يقبل الثلاثيَّ الضريبيّ كاملاً.</para>
///
/// <para><b>والضررُ نظاميٌّ لا تجميليّ:</b> ضريبةُ المدخلات القابلةُ للخصم تُبالَغ، و«المشترياتُ
/// بالفئة» في الإقرار الضريبيّ تنهار إلى وعاءٍ واحدٍ أساسيّ — <b>فيختفي وعاءُ المعفى</b>.
/// وهو حرفيّاً السيناريو الذي ادّعته ن-٥١ («تقريرٌ يصمت عن الأسطر المعفاة») — <b>لكن على
/// مسارٍ آخر</b>: ن-٥١ كانت ساكنةً لأنّ عرضَ السعر وطلبَ الشراء لا يُعبّران عن فئةٍ أصلاً،
/// وهذه حيّةٌ لأنّ أمرَ الشراء يُعبّر ثمّ يُنسى.</para>
///
/// <para><b>والاصطلاحُ الصحيحُ موجودٌ في المستودع:</b> <c>RecurringInvoiceService.cs:123</c>
/// يُمرّر الثلاثيَّ كاملاً. فأربعةُ مواضعَ خالفت اصطلاحاً قائماً، لا اصطلاحاً مفقوداً.</para>
///
/// <para><b>ولماذا يُشترط التوقيعُ مع التمرير في بندٍ واحد:</b> سجلُّ <c>SalesInvoiceLine</c>
/// يحمل مبدأً مكتوباً في تعليق <c>UnitCode</c>: «ولا تُتاح إلا حيث تُحوَّل … فالمُصرِّفُ يضمن
/// ألّا توجد وحدةٌ في مسارٍ لا يُحوّلها، بلا حارسٍ يُنسى». <b>فإضافةُ الوسيط إلى عرض السعر
/// وطلب الشراء بلا تمريرِه في تحويلهما تخرق مبدأَ السجلّ نفسِه</b> — ولذلك يُشترط هنا
/// الأمران معاً.</para>
/// </summary>
public class TaxIdentitySurvivesConversionTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    private const string ExemptCode = "VATEX-SA-29";
    private const string ExemptReason = "خدمة مالية معفاة";

    public TaxIdentitySurvivesConversionTests()
    {
        TestActor.Accountant();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task<Company> CompanyAsync() =>
        await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "300000000000001", Brqosh.Domain.Modules.BusinessProfile.Manufacturing);

    /// <summary>
    /// الهويّةُ الضريبيّةُ للسطر — <b>وهي ما يجب أن يعبر التحويلَ بلا نقصان</b>.
    /// </summary>
    private static (VatCategory Category, string? Code, string? Reason) TaxIdentity(SalesInvoiceLine line) =>
        (line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason);

    private static string Describe(IEnumerable<SalesInvoiceLine> lines) =>
        string.Join(" | ", lines.Select(l =>
            $"{l.Description}={l.VatCategory}/رمز={l.TaxExemptionReasonCode ?? "<لا شيء>"}"
            + $"/نصّ={l.TaxExemptionReason ?? "<لا شيء>"}"));

    /// <summary>
    /// <b>يُقارَن المصدرُ بالهدف لا بحرفٍ كتبتُه في الاختبار.</b> فـ<c>SalesInvoice.AddLine</c>
    /// و<c>SalesOrder.AddLine</c> <b>يستبدلان</b> نصَّ السبب بنصّ السجلّ النظاميّ عمداً
    /// («ونصُّ السبب من القائمة لا من المستخدم … وبهذا لا ينحرف النصُّ عن رمزه»)، فتثبيتُ
    /// حرفٍ في الاختبار يُسقطه على سلوكٍ صحيح. <b>والثابتُ الحقيقيُّ أنّ السطرَ المحوَّل يساوي
    /// أصلَه</b> — وهو أقوى، إذ يشمل الترتيبَ وكلَّ سطرٍ لا الأوّلَ وحدَه.
    /// </summary>
    private static void AssertConversionKept(
        IReadOnlyList<SalesInvoiceLine> source, IReadOnlyList<SalesInvoiceLine> converted, string what)
    {
        // ‏شاهدُ ضبطٍ ضدّ الفراغ: مقارنةُ قائمتَين فارغتَين تنجح بلا معنى.
        Assert.NotEmpty(source);
        Assert.True(source.Any(l => l.VatCategory != VatCategory.Standard),
            $"‏المصدرُ نفسُه بلا سطرٍ غيرِ أساسيّ — فالمقارنةُ تنجح بلا شيءٍ تقيسه: {Describe(source)}");

        Assert.True(source.Select(TaxIdentity).SequenceEqual(converted.Select(TaxIdentity)),
            $"‏{what}: التحويلُ غيّر الهويّةَ الضريبيّة.\n  المصدر: {Describe(source)}\n  الهدف:  {Describe(converted)}");
    }

    // ───────────────────────── أمرُ الشراء ⟵ فاتورةُ الشراء (حيّ) ─────────────────────────

    [Fact]
    public async Task PurchaseOrder_to_invoice_keeps_the_tax_identity()
    {
        var company = await CompanyAsync();
        var supplier = Supplier.Create(company.Id, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1));
        order.AddLine("خدمة مالية معفاة", 1m, 1000m, null, VatCategory.Exempt, ExemptCode, ExemptReason);
        order.AddLine("مواد خام", 1m, 1000m);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        // ‏شاهدُ ضبط: الأمرُ نفسُه حفظ الإعفاءَ فعلاً — فما يُقاس بعدُ هو التحويلُ لا الإدخال.
        var storedOrder = await _db.PurchaseOrders.Include(o => o.Lines)
            .SingleAsync(o => o.Id == order.Id);
        Assert.Equal(VatCategory.Exempt, storedOrder.Lines[0].VatCategory);
        Assert.Equal(ExemptCode, storedOrder.Lines[0].TaxExemptionReasonCode);
        Assert.Equal(1000m * 0.15m, storedOrder.VatAmount);

        var invoice = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            company.Id, order.Id, "SUP-INV-99", new DateOnly(2026, 3, 5), vatDeductible: true);

        var stored = await _db.PurchaseInvoices.Include(i => i.Lines)
            .SingleAsync(i => i.Id == invoice.Id);

        AssertConversionKept(storedOrder.Lines, stored.Lines, "أمرُ الشراء ⟵ فاتورةُ الشراء");

        Assert.True(storedOrder.VatAmount == stored.VatAmount,
            $"‏ضريبةُ الأمر {storedOrder.VatAmount} والفاتورةُ {stored.VatAmount} — "
            + $"وضريبةُ المدخلات القابلةُ للخصم تتبع الفاتورة: {Describe(stored.Lines)}");
    }

    [Fact]
    public async Task The_vat_return_still_shows_the_exempt_bucket_after_conversion()
    {
        var company = await CompanyAsync();
        var supplier = Supplier.Create(company.Id, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1));
        order.AddLine("خدمة مالية معفاة", 1m, 1000m, null, VatCategory.Exempt, ExemptCode, ExemptReason);
        order.AddLine("مواد خام", 1m, 1000m);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            company.Id, order.Id, "SUP-INV-99", new DateOnly(2026, 3, 5), vatDeductible: true);

        var result = await new VatReturnQuery(_db).ExecuteAsync(
            company.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // ‏شاهدُ ضبط: الإقرارُ رأى مشترياتٍ أصلاً — فالفشلُ التالي عن الفئة لا عن الفراغ.
        Assert.NotEmpty(result.Purchases);

        var buckets = string.Join(" · ", result.Purchases.Select(
            b => $"{b.Category}: وعاء={b.TaxableAmount} ضريبة={b.VatAmount}"));

        Assert.True(result.Purchases.Any(b => b.Category == VatCategory.Exempt),
            $"‏وعاءُ المعفى اختفى من «المشتريات بالفئة» في الإقرار الضريبيّ: {buckets}");
    }

    // ───────────────────────── أمرُ البيع ⟵ فاتورةُ المبيعات ─────────────────────────

    [Fact]
    public async Task SalesOrder_to_invoice_keeps_the_tax_identity()
    {
        var company = await CompanyAsync();
        var customer = Customer.Create(company.Id, "عميل", "300000000000002");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var order = SalesOrder.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 3, 1));
        order.AddLine("خدمة مالية معفاة", 1m, 1000m, null, VatCategory.Exempt, ExemptCode, ExemptReason);
        order.AddLine("بضاعة", 1m, 1000m);
        await new SalesOrderService(_db).ConfirmAsync(order);

        var storedOrder = await _db.SalesOrders.Include(o => o.Lines)
            .SingleAsync(o => o.Id == order.Id);
        Assert.Equal(VatCategory.Exempt, storedOrder.Lines[0].VatCategory);

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(company.Id, order.Id);
        var stored = await _db.SalesInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoice.Id);

        AssertConversionKept(storedOrder.Lines, stored.Lines, "أمرُ البيع ⟵ فاتورةُ المبيعات");

        Assert.True(storedOrder.VatAmount == stored.VatAmount,
            $"‏ضريبةُ الأمر {storedOrder.VatAmount} والفاتورةُ {stored.VatAmount}: {Describe(stored.Lines)}");
    }

    // ───────── المساران اللذان صارا حيَّين بهذا التغيير نفسِه ─────────

    [Fact]
    public async Task Quotation_to_sales_order_keeps_the_tax_identity()
    {
        var company = await CompanyAsync();
        var customer = Customer.Create(company.Id, "عميل", "300000000000002");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        // ‏**نصُّ السبب يُبذر من سجلّ الهيئة نفسِه** لا من حرفٍ أكتبه: `SalesOrder.AddLine`
        // ‏يستبدل النصَّ بنصّ السجلّ عمداً، فبذرُ غيرِه يجعل الاختبارَ يقيس **التقييسَ**
        // ‏لا الضياع — وهما مختلفان، وواحدٌ منهما سلوكٌ صحيح.
        var canonical = VatExemptionReason.Find(ExemptCode)!.EnglishText;

        var quote = SalesQuotation.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 3, 1));
        quote.AddLine("خدمة مالية معفاة", 1m, 1000m, null, VatCategory.Exempt, ExemptCode, canonical);
        quote.AddLine("بضاعة", 1m, 1000m);
        _db.SalesQuotations.Add(quote);
        await _db.SaveChangesAsync();

        quote.MarkSent("Q-2026-1");
        await _db.SaveChangesAsync();

        var storedQuote = await _db.SalesQuotations.Include(q => q.Lines)
            .SingleAsync(q => q.Id == quote.Id);

        var order = await new SalesOrderService(_db).CreateFromQuotationAsync(company.Id, quote.Id);
        var storedOrder = await _db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);

        AssertConversionKept(storedQuote.Lines, storedOrder.Lines, "عرضُ السعر ⟵ أمرُ البيع");
        Assert.Equal(storedQuote.VatAmount, storedOrder.VatAmount);
    }

    [Fact]
    public async Task PurchaseRequest_to_purchase_order_keeps_the_tax_identity()
    {
        var company = await CompanyAsync();
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(company.Id, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var request = PurchaseRequest.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1), "معدات");
        request.AddLine("خدمة مالية معفاة", 1m, 1000m, null, VatCategory.Exempt, ExemptCode, ExemptReason);
        request.AddLine("طابعة", 1m, 1000m);
        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        PurchaseRequestWorkflowService Svc() => new(_db, new WorkflowEngine(_db));

        SecurityContext.CurrentUsername = "employee";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().SubmitAsync(request);

        SecurityContext.CurrentUsername = "manager";
        SecurityContext.CurrentRole = UserRole.Accountant;
        await Svc().ApproveAsync(request.Id, "موافقة مدير");

        SecurityContext.CurrentUsername = "finance";
        SecurityContext.CurrentRole = UserRole.Administrator;
        await Svc().ApproveAsync(request.Id, "موافقة مالية");

        var storedRequest = await _db.PurchaseRequests.Include(r => r.Lines)
            .SingleAsync(r => r.Id == request.Id);
        Assert.Equal(PurchaseRequestStatus.Approved, storedRequest.Status);

        var po = await Svc().ConvertToPurchaseOrderAsync(request.Id);
        var storedPo = await _db.PurchaseOrders.Include(o => o.Lines).SingleAsync(o => o.Id == po.Id);

        AssertConversionKept(storedRequest.Lines, storedPo.Lines, "طلبُ الشراء ⟵ أمرُ الشراء");
    }

    // ───────────────────────── التوقيعاتُ نفسُها ─────────────────────────

    /// <summary>
    /// <b>الحدُّ الذي تحرسه هذه الحالة:</b> المستنداتُ التسعةُ التي تشترك في
    /// <c>SalesInvoiceLine</c> يجب أن تستطيع كلُّها التعبيرَ عن فئةٍ ضريبيّة. <b>وعرضُ السعر
    /// وطلبُ الشراء كانا وحدَهما عاجزَين</b> — وهما بعينهما الجدولان اللذان كان عمودُهما عدداً
    /// صحيحاً في ن-٥١. <b>فالعجزُ والعمودُ وجهان لسببٍ واحد</b>، لا مصادفةٌ متجاورة.
    /// </summary>
    [Theory]
    [InlineData(typeof(SalesInvoice))]
    [InlineData(typeof(SalesOrder))]
    [InlineData(typeof(SalesQuotation))]
    [InlineData(typeof(CreditNote))]
    [InlineData(typeof(RecurringInvoicePlan))]
    [InlineData(typeof(PurchaseInvoice))]
    [InlineData(typeof(PurchaseOrder))]
    [InlineData(typeof(PurchaseRequest))]
    [InlineData(typeof(DebitNote))]
    public void Every_owner_of_the_shared_line_can_express_a_vat_category(Type document)
    {
        var addLine = document.GetMethod("AddLine", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"‏لا AddLine عامٌّ على {document.Name}.");

        var parameters = addLine.GetParameters();

        // ‏شاهدُ ضبط: التقنيةُ ترى وسائطَ هذا التوقيع فعلاً قبل أن تُنكر واحداً منها.
        Assert.Contains(parameters, p => p.ParameterType == typeof(string) && p.Name == "description");

        Assert.True(parameters.Any(p => p.ParameterType == typeof(VatCategory)),
            $"‏{document.Name}.AddLine لا يقبل فئةً ضريبيّة، فكلُّ سطرٍ فيه «أساسيّة» قسراً — "
            + $"والعمودُ موجودٌ في جدوله مع ذلك. التوقيعُ الحاليّ: "
            + $"({string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
    }
}
