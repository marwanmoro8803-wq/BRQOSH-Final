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
/// (جاهزيّةُ الإطلاق · ح-١٦٥ · <c>tax-zatca#12</c>) <b>الإقرارُ يملأ خاناتِ نموذج الهيئة — بمبلغها وتعديلاتها وضريبتها.</b>
///
/// <para><b>ما كان:</b> تفكيكٌ بالفئة (S · Z · E · O) ولا إسنادَ إلى الخانات؛ فالصادراتُ والمحلّيُّ الصفريُّ ومبيعاتُ المواطن
/// في الصحّة والتعليم وعاءٌ صفريٌّ واحد، وعلى المالك أن يفرزها يدويّاً قبل البوّابة — وثلاثُ خاناتٍ مختلفةٌ في النموذج.</para>
///
/// <para>الأرقامُ محسوبةٌ سلفاً: فاتورةٌ صفريّةٌ بأربعة سطورٍ (تصديرُ سلعٍ 400 · تصديرُ خدماتٍ 100 · رعايةٌ صحّيّةٌ لمواطن 300 ·
/// أدويةٌ 200) وخصمِ مستندٍ 100 — فالوعاءُ 900 يُقسم بنسبة السطور: الخانة 2 = 270، والخانة 3 = 180، والخانة 4 = 450.</para>
/// </summary>
public class VatReturnFillsTheOfficialBoxesTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private readonly long _supplierId;
    private readonly long _productId;

    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    public VatReturnFillsTheOfficialBoxesTests()
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

    private async Task<SalesInvoice> SellAsync(Action<SalesInvoice> build)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        build(invoice);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private Task<VatReturnResult> ReturnAsync() => new VatReturnQuery(_db).ExecuteAsync(_companyId, From, To);

    private static VatReturnBoxLine Box(IReadOnlyList<VatReturnBoxLine>? boxes, int box) =>
        Assert.Single(boxes!, b => b.Box == box);

    [Fact]
    public async Task ZeroRatedSales_SplitIntoExportsDomesticAndGovernmentBorne_ByTheirExemptionCode()
    {
        await SellAsync(i =>
        {
            i.AddLine("تصدير سلع", 1m, 400m, _productId, VatCategory.ZeroRated, "VATEX-SA-32");
            i.AddLine("تصدير خدمات", 1m, 100m, _productId, VatCategory.ZeroRated, "VATEX-SA-33");
            i.AddLine("رعاية صحّيّة لمواطن", 1m, 300m, _productId, VatCategory.ZeroRated, "VATEX-SA-HEA");
            i.AddLine("أدوية", 1m, 200m, _productId, VatCategory.ZeroRated, "VATEX-SA-35");
            i.ApplyDocumentDiscount(100m);
        });

        var r = await ReturnAsync();

        Assert.Equal(270m, Box(r.SalesBoxes, VatReturnBox.GovernmentBearsVatSales).Amount);
        Assert.Equal(180m, Box(r.SalesBoxes, VatReturnBox.ZeroRatedDomesticSales).Amount);
        Assert.Equal(450m, Box(r.SalesBoxes, VatReturnBox.Exports).Amount);
        Assert.Equal(0m, Box(r.SalesBoxes, VatReturnBox.StandardRatedSales).Amount);
        Assert.All(r.SalesBoxes!, b => Assert.Equal(0m, b.VatAmount));
    }

    [Fact]
    public async Task CreditNotesFallInTheAdjustmentsColumn_OfTheirOwnBox()
    {
        var invoice = await SellAsync(i => i.AddLine("بند أساسيّ", 1m, 1_000m, _productId));
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 2, 10), "مرتجع جزئيّ");
        note.AddLine("بند أساسيّ", 1m, 400m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var box1 = Box((await ReturnAsync()).SalesBoxes, VatReturnBox.StandardRatedSales);

        Assert.Equal(1_000m, box1.Amount);
        Assert.Equal(-400m, box1.Adjustments);
        Assert.Equal(150m, box1.VatAmount);
        Assert.Equal(-60m, box1.VatAdjustments);
    }

    [Fact]
    public async Task TheBoxesAddUpToTheReturn_AndOutOfScopeHasNoBox()
    {
        await SellAsync(i => i.AddLine("أساسيّ", 1m, 1_000m, _productId));
        await SellAsync(i =>
        {
            i.AddLine("تصدير", 1m, 500m, _productId, VatCategory.ZeroRated, "VATEX-SA-32");
            i.AddLine("إيجار سكنيّ", 1m, 250m, _productId, VatCategory.Exempt, VatExemptionReason.ResidentialRentCode);
            i.AddLine("خارج النطاق", 1m, 70m, _productId, VatCategory.OutOfScope, VatExemptionReason.OutOfScopeCode, "تعويض");
        });

        var r = await ReturnAsync();

        Assert.Equal(VatReturnBox.SalesBoxes, r.SalesBoxes!.Select(b => b.Box));
        Assert.Equal(250m, Box(r.SalesBoxes, VatReturnBox.ExemptSales).Amount);
        Assert.Equal(r.SalesBaseNet - 70m, r.SalesBoxes!.Sum(b => b.Amount + b.Adjustments));
        Assert.Equal(r.NetOutputVat - r.JournalOutputVat, r.SalesBoxes!.Sum(b => b.VatAmount + b.VatAdjustments));
    }

    [Fact]
    public async Task Purchases_FillBoxes7_10_11_AndImportsStayAnnouncedZeros()
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 2, 1), "SUP-1", vatDeductible: true);
        invoice.AddLine("توريد", 1m, 2_000m, _productId);
        invoice.AddLine("أدوية", 1m, 300m, _productId, VatCategory.ZeroRated, "VATEX-SA-35");
        invoice.AddLine("تأمين على الحياة", 1m, 100m, _productId, VatCategory.Exempt, "VATEX-SA-29-7");
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var note = DebitNote.CreateDraft(_companyId, _supplierId, invoice.Id, new DateOnly(2026, 2, 15), "مرتجع");
        note.AddLine("مرتجع", 1m, 500m, _productId);
        await new DebitNotePostingService(_db).PostAsync(note);

        var r = await ReturnAsync();
        var box7 = Box(r.PurchaseBoxes, VatReturnBox.StandardRatedDomesticPurchases);

        Assert.Equal((2_000m, -500m, 300m, -75m), (box7.Amount, box7.Adjustments, box7.VatAmount, box7.VatAdjustments));
        Assert.Equal(300m, Box(r.PurchaseBoxes, VatReturnBox.ZeroRatedPurchases).Amount);
        Assert.Equal(100m, Box(r.PurchaseBoxes, VatReturnBox.ExemptPurchases).Amount);
        Assert.Equal(0m, Box(r.PurchaseBoxes, VatReturnBox.ImportsPaidAtCustoms).Amount);
        Assert.Equal(0m, Box(r.PurchaseBoxes, VatReturnBox.ImportsUnderReverseCharge).Amount);
        Assert.Equal(r.PurchasesBaseNet, r.PurchaseBoxes!.Sum(b => b.Amount + b.Adjustments));
    }

    [Fact]
    public async Task TheBoxesCrossTheApplicationBoundary()
    {
        await SellAsync(i => i.AddLine("تصدير", 1m, 500m, _productId, VatCategory.ZeroRated, "VATEX-SA-32"));

        var dto = VatReturnMapping.From(await ReturnAsync());

        Assert.Equal(500m, Assert.Single(dto.SalesBoxes!, b => b.Box == VatReturnBox.Exports).Amount);
        Assert.Equal(VatReturnBox.PurchaseBoxes, dto.PurchaseBoxes!.Select(b => b.Box));
    }

    [Theory]
    [InlineData(VatCategory.Standard, null, VatReturnBox.StandardRatedSales)]
    [InlineData(VatCategory.ZeroRated, "VATEX-SA-32", VatReturnBox.Exports)]
    [InlineData(VatCategory.ZeroRated, "VATEX-SA-33", VatReturnBox.Exports)]
    [InlineData(VatCategory.ZeroRated, "VATEX-SA-EDU", VatReturnBox.GovernmentBearsVatSales)]
    [InlineData(VatCategory.ZeroRated, "VATEX-SA-HEA", VatReturnBox.GovernmentBearsVatSales)]
    [InlineData(VatCategory.ZeroRated, "VATEX-SA-34-1", VatReturnBox.ZeroRatedDomesticSales)]
    [InlineData(VatCategory.ZeroRated, "VATEX-SA-35", VatReturnBox.ZeroRatedDomesticSales)]
    [InlineData(VatCategory.Exempt, "VATEX-SA-29", VatReturnBox.ExemptSales)]
    public void EverySaleCategoryAndCode_HasItsBox(VatCategory category, string? code, int box) =>
        Assert.Equal(box, VatReturnBox.ForSale(category, code));

    [Fact]
    public void OutOfScope_HasNoBoxOnEitherSide()
    {
        Assert.Null(VatReturnBox.ForSale(VatCategory.OutOfScope, VatExemptionReason.OutOfScopeCode));
        Assert.Null(VatReturnBox.ForPurchase(VatCategory.OutOfScope));
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
