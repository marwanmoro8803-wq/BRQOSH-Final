using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-229 · A-15) الإقرار الضريبيّ مفكَّك حسب الفئة.
///
/// <para><b>العطل:</b> <c>VatReturnResult</c> كان يُخرج مجاميع كلّية فقط، رغم أن الفئة الضريبية
/// <b>موجودة على مستوى السطر</b> ويحسبها <c>LineDocumentTotalsCalculator.ComputeSubtotals</c>
/// أصلاً في مولّد ZATCA — فالبيانات متوفّرة والتقرير يهدرها.</para>
///
/// <para><b>الأثر بالأرقام:</b> ربعٌ فيه مبيعات محلية 1,000,000 (ضريبة 150,000) وصادرات صفرية
/// 400,000 وإيجار معفى 100,000. المجموع الكلّي يقول وعاءً 1,500,000 مقابل ضريبة 150,000 — أي
/// <b>10%</b>، نسبةٌ لا وجود لها في أيّ نظام ضريبيّ، فيقع تعارضٌ آليّ فوريّ على بوابة الهيئة،
/// وتختفي 500,000 ريال من الإفصاح.</para>
///
/// <para><b>حدٌّ مُصرَّح به:</b> التفكيك بالفئة فقط — <b>ولا تُسنَد أرقام صناديق النموذج الرسميّ</b>،
/// فأرقامها تحتاج مراجعة النموذج نصّاً (م4-٤). إسنادُ رقم صندوقٍ بالتخمين أسوأ من عدم إسناده.</para>
/// </summary>
public class VatReturnCategoryBreakdownTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private readonly long _supplierId;
    private readonly long _productId;

    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    public VatReturnCategoryBreakdownTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Service);
        _db.Products.Add(product);
        _db.SaveChanges();

        _customerId = customer.Id;
        _supplierId = supplier.Id;
        _productId = product.Id;
    }

    private async Task SellAsync(params (decimal Amount, VatCategory Category)[] lines)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        foreach (var (amount, category) in lines)
            // ‏(م1-١ب/الشقّ الثاني) الفئةُ غيرُ الأساسية تحمل سببها — وإلا رفضها بناءُ السطر.
            invoice.AddLine("بند", 1m, amount, _productId, category,
                category switch
                {
                    VatCategory.ZeroRated => "VATEX-SA-32",
                    VatCategory.Exempt => "VATEX-SA-30",
                    VatCategory.OutOfScope => VatExemptionReason.OutOfScopeCode,
                    _ => null,
                },
                category == VatCategory.OutOfScope ? "سبب اختباريّ" : null);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<VatReturnResult> ReturnAsync() =>
        await new VatReturnQuery(_db).ExecuteAsync(_companyId, From, To);

    private static VatReturnBucket Bucket(VatReturnResult r, VatCategory c) =>
        r.Sales.SingleOrDefault(b => b.Category == c)
        ?? new VatReturnBucket(c, 0m, 0m);

    /// <summary>
    /// <b>القياس الأساسيّ — السيناريو المُبلَّغ حرفياً.</b> ثلاث فئات في ربع واحد:
    /// كلٌّ تظهر في وعائها بمبلغها وضريبتها، ومجموع الأوعية يساوي وعاء الإقرار الكلّي.
    /// </summary>
    [Fact]
    public async Task ThreeCategoriesInOneQuarter_AppearInSeparateBuckets()
    {
        await SellAsync((1_000_000m, VatCategory.Standard));
        await SellAsync((400_000m, VatCategory.ZeroRated));
        await SellAsync((100_000m, VatCategory.Exempt));

        var result = await ReturnAsync();

        Assert.Equal(3, result.Sales.Count);

        var standard = Bucket(result, VatCategory.Standard);
        Assert.Equal(1_000_000m, standard.TaxableAmount);
        Assert.Equal(150_000m, standard.VatAmount);

        var zero = Bucket(result, VatCategory.ZeroRated);
        Assert.Equal(400_000m, zero.TaxableAmount);
        Assert.Equal(0m, zero.VatAmount);

        var exempt = Bucket(result, VatCategory.Exempt);
        Assert.Equal(100_000m, exempt.TaxableAmount);
        Assert.Equal(0m, exempt.VatAmount);

        // الأوعية تجمع إلى الإجمالي — لا يُفقَد ريال ولا يُحتسب مرّتين.
        Assert.Equal(result.SalesSubtotal, result.Sales.Sum(b => b.TaxableAmount));
        Assert.Equal(result.OutputVat, result.Sales.Sum(b => b.VatAmount));
    }

    /// <summary>
    /// <b>وهذا ما كان يُنتج النسبة الكاذبة:</b> المجموع الكلّي يقول 10% بينما الوعاء الخاضع
    /// نسبتُه 15% فعلاً. التفكيك يُظهر الحقيقة.
    /// </summary>
    [Fact]
    public async Task BlendedRate_IsMisleading_WhileBucketRateIsTrue()
    {
        await SellAsync((1_000m, VatCategory.Standard), (500m, VatCategory.ZeroRated));

        var result = await ReturnAsync();

        Assert.Equal(1_500m, result.SalesSubtotal);
        Assert.Equal(150m, result.OutputVat);
        Assert.Equal(0.10m, Math.Round(result.OutputVat / result.SalesSubtotal, 2));   // النسبة الكاذبة

        var standard = Bucket(result, VatCategory.Standard);
        Assert.Equal(0.15m, Math.Round(standard.VatAmount / standard.TaxableAmount, 2));   // الحقيقة
    }

    /// <summary>الإشعار الدائن يُطرح من <b>وعائه</b> لا من وعاءٍ آخر ولا يُفرَد بوعاء خاصّ.</summary>
    [Fact]
    public async Task CreditNote_IsNettedAgainstItsOwnBucket()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("بند أساسيّ", 1m, 1_000m, _productId);
        invoice.AddLine("صادرات", 1m, 500m, _productId, VatCategory.ZeroRated, "VATEX-SA-32");
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 10), "مرتجع جزئيّ");
        note.AddLine("بند أساسيّ", 1m, 400m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var result = await ReturnAsync();

        Assert.Equal(600m, Bucket(result, VatCategory.Standard).TaxableAmount);    // 1,000 − 400
        Assert.Equal(90m, Bucket(result, VatCategory.Standard).VatAmount);
        Assert.Equal(500m, Bucket(result, VatCategory.ZeroRated).TaxableAmount);   // لم يُمَسّ
    }

    /// <summary>المشتريات تُفكَّك كذلك، والإشعار المدين يُطرح من وعائه.</summary>
    [Fact]
    public async Task Purchases_AreBrokenDownAndNettedByDebitNotes()
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 2, 1), "SUP-1", vatDeductible: true);
        invoice.AddLine("توريد", 1m, 2_000m, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var note = DebitNote.CreateDraft(
            _companyId, _supplierId, invoice.Id, new DateOnly(2026, 2, 15), "مرتجع");
        note.AddLine("مرتجع", 1m, 500m, _productId);
        await new DebitNotePostingService(_db).PostAsync(note);

        var result = await ReturnAsync();
        var standard = result.Purchases.Single(b => b.Category == VatCategory.Standard);

        Assert.Equal(1_500m, standard.TaxableAmount);
        Assert.Equal(225m, standard.VatAmount);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> فترةٌ بلا حركة تُخرج أوعية فارغة لا أوعيةً بأصفار — فالقائمة
    /// الفارغة تعني «لا شيء» لا «كل الفئات صفر».
    /// </summary>
    [Fact]
    public async Task EmptyPeriod_YieldsNoBuckets()
    {
        var result = await ReturnAsync();

        Assert.Empty(result.Sales);
        Assert.Empty(result.Purchases);
    }

    /// <summary>
    /// وفاتورةٌ كلّها أساسية تُخرج وعاءً واحداً — الإصلاح لا يُفتّت ما لا يحتاج تفتيتاً،
    /// ولا يُزيح أرقام إقرارٍ قائم.
    /// </summary>
    [Fact]
    public async Task SingleCategoryPeriod_YieldsOneBucketMatchingTheTotals()
    {
        await SellAsync((5_000m, VatCategory.Standard));

        var result = await ReturnAsync();
        var only = Assert.Single(result.Sales);

        Assert.Equal(VatCategory.Standard, only.Category);
        Assert.Equal(result.SalesSubtotal, only.TaxableAmount);
        Assert.Equal(result.OutputVat, only.VatAmount);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
