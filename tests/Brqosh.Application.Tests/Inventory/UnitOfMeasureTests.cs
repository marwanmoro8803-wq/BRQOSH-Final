using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-230 · ط1-١) <b>وحداتُ القياس وتحويلُها.</b>
///
/// <para><b>ما كان:</b> <c>Product.Unit</c> نصٌّ حرٌّ لا يُتحقَّق منه ولا يُحوَّل به شيء.
/// فمَن اشترى كرتوناً وباع حبّةً سجّل <c>1+</c> ثمّ <c>24−</c> على الرصيد نفسِه.
/// <b>والمخرجُ الموصى به في الشيفرة نفسِها كان «أدخل الكرتونَ سطراً واحداً بسعره الكامل»</b>
/// — التفافٌ لا حلّ.</para>
///
/// <para><b>وثانيةٌ نظامية:</b> مُولِّد الفوترة كان يُثبّت <c>unitCode="PCE"</c> لكلّ سطر،
/// <b>فبيعُ خمسة كيلوجرامات يُبلَّغ للهيئة خمسَ قطع</b>.</para>
/// </summary>
public class UnitOfMeasureTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;

    public UnitOfMeasureTests()
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

    // ── القالب ──────────────────────────────────────────────────────────────

    /// <summary><b>وكلُّ منشأةٍ تُبذَر بقالب الوحدات</b> — فالتحويلُ متاحٌ من أوّل يوم.</summary>
    [Fact]
    public async Task ANewCompany_IsSeededWithTheUnitCatalog()
    {
        await SetupAsync();

        var units = await _db.UnitsOfMeasure.Where(u => u.CompanyId == _companyId).ToListAsync();
        Assert.Equal(UnitCatalog.Template.Count, units.Count);

        var box = units.Single(u => u.Code == "BOX");
        Assert.Equal("XBX", box.UnCode);
        Assert.Equal("PCE", box.BaseCode);
        Assert.Equal(12m, box.Factor);
    }

    /// <summary>
    /// <b>والمرادفاتُ جسرٌ إلى النصّ الحرّ القديم.</b>
    ///
    /// <para>فكلُّ صنفٍ قائمٍ يحمل وحدتَه نصّاً عربياً — وبغير جسرٍ تفقد الأصنافُ القائمةُ
    /// وحداتِها يومَ الترقية.</para>
    /// </summary>
    [Theory]
    [InlineData("كجم", "KGM")]
    [InlineData("كيلو", "KGM")]
    [InlineData("كيلوجرام", "KGM")]
    [InlineData("قطعة", "PCE")]
    [InlineData("حبّة", "PCE")]
    [InlineData("كرتون", "XBX")]
    [InlineData("لتر", "LTR")]
    [InlineData("م²", "MTK")]
    [InlineData("ساعة", "HUR")]
    [InlineData("شيء لا يُعرف", "PCE")]
    public void FreeTextUnits_MapToUnEceCodes(string text, string expected) =>
        Assert.Equal(expected, UnitCatalog.UnCodeFor(text));

    // ── التحويل ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: يُشترى كرتوناً ويُباع حبّة.</b>
    ///
    /// <para>كرتونان (٢٤ حبّة) يدخلان، ثمّ تُباع عشرُ حبّات — فيبقى ١٤. <b>وبغير التحويل
    /// كان الرصيدُ يصير <c>2 − 10 = −8</c></b>، أو يُرفض البيعُ بحجّة «الرصيد غير كافٍ»
    /// والمستودعُ ملآن.</para>
    /// </summary>
    [Fact]
    public async Task BuyingByBoxAndSellingByPiece_KeepsOneBalance()
    {
        var product = await ProductAsync("قطعة");
        await ReceiveAsync(product, quantity: 2m, unitCost: 120m, unitCode: "BOX");

        Assert.Equal(24m, await OnHandAsync(product.Id));

        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerIdAsync(), new DateOnly(2026, 2, 1));
        invoice.AddLine("حبّة", 10m, 20m, product.Id);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(14m, await OnHandAsync(product.Id));
    }

    /// <summary>
    /// <b>والتكلفةُ تُقسَم بالمعامل نفسِه.</b>
    ///
    /// <para>كرتونٌ بـ120 ريالاً اثنتا عشرة حبّةً بـ10 — <b>وبغير القسمة صارت الحبّةُ
    /// بـ120 والمخزونُ منتفخاً اثنَي عشر ضعفاً</b>، فتنكسر <c>هـ-٣</c>.</para>
    /// </summary>
    [Fact]
    public async Task TheUnitCost_IsDividedByTheSameFactor()
    {
        var product = await ProductAsync("قطعة");
        await ReceiveAsync(product, quantity: 2m, unitCost: 120m, unitCode: "BOX");

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var row = rows.Single(r => r.ProductId == product.Id);

        Assert.Equal(24m, row.Quantity);
        Assert.Equal(10m, row.UnitCost);
        Assert.Equal(240m, row.TotalValue);

        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    /// <summary>
    /// <b>ويُباع بالكرتون من رصيدٍ بالحبّة.</b>
    ///
    /// <para>وهو الاتّجاهُ الآخر، ويجب أن يُقارَن بالرصيد <b>بعد</b> التحويل: بيعُ كرتونٍ
    /// من عشرِ حبّاتٍ يُرفض — <b>وبغير التحويل يمرّ</b> لأنّ «١ أقلّ من ١٠».</para>
    /// </summary>
    [Fact]
    public async Task SellingByBoxFromAPieceBalance_ChecksTheConvertedQuantity()
    {
        var product = await ProductAsync("قطعة");
        await ReceiveAsync(product, quantity: 10m, unitCost: 10m, unitCode: null);

        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerIdAsync(), new DateOnly(2026, 2, 1));
        invoice.AddLine("كرتون", 1m, 300m, product.Id, unitCode: "BOX");

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));

        Assert.Contains("غير كافٍ", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا تحويلَ بين أسرتين.</b>
    ///
    /// <para>الكيلوجرامُ لا يصير قطعةً — <b>وتحويلٌ صامتٌ بينهما يُنتج رصيداً خاطئاً لا
    /// يكشفه شيء</b>، لأنّ الرقمَ صحيحُ الشكل خاطئُ المعنى.</para>
    /// </summary>
    [Fact]
    public async Task ConvertingAcrossFamilies_IsRefused()
    {
        var product = await ProductAsync("قطعة");

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => ReceiveAsync(product, quantity: 5m, unitCost: 10m, unitCode: "KG"));

        Assert.Contains("أسرتين مختلفتين", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وصنفٌ وحدتُه نصٌّ لا يُعرَف يرفض التحويل — ولا يُخمَّن.</b>
    ///
    /// <para>فلا يُعرف إلى أيّ وحدةٍ يُحوَّل، <b>والتخمينُ هنا يُنتج رصيداً خاطئاً صامتاً</b>.
    /// أمّا بلا وحدةِ سطرٍ فيمرّ كما كان — وهو سلوكُ كلّ ما مضى.</para>
    /// </summary>
    [Fact]
    public async Task AnUnknownProductUnit_RefusesConversionButNotPlainEntry()
    {
        var product = await ProductAsync("وحدةٌ مخترَعة");

        await ReceiveAsync(product, quantity: 7m, unitCost: 10m, unitCode: null);
        Assert.Equal(7m, await OnHandAsync(product.Id));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => ReceiveAsync(product, quantity: 1m, unitCost: 10m, unitCode: "BOX"));

        Assert.Contains("اربطِ الصنفَ بوحدةٍ", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والمستندُ الإلكترونيّ يحمل الوحدةَ الحقيقية.</b>
    ///
    /// <para>وكان يُثبّت <c>PCE</c> لكلّ سطرٍ في النظام — <b>فبيعُ خمسة كيلوجرامات
    /// يُبلَّغ للهيئة خمسَ قطع</b>، تصريحٌ مخالفٌ للواقع يمرّ على كلّ تصديقٍ بنيويّ لأنّ
    /// المخطّط يفحص وجودَ الرمز لا صدقَه.</para>
    /// </summary>
    [Fact]
    public void TheUblLine_CarriesTheRealUnitCode()
    {
        var lines = new List<SalesInvoiceLine> { new("دقيق", 5m, 12m) };

        var xml = System.Xml.Linq.XDocument.Parse(
            System.Text.Encoding.UTF8.GetString(ZatcaUblInvoiceBuilder.BuildXml(
                new ZatcaUblInvoiceBuilder.InvoiceData(
                    "INV-1", Guid.NewGuid(), new DateOnly(2026, 5, 1), new TimeOnly(10, 0),
                    "منشأة", "310123456700003", "عميل", null,
                    60m, 9m, 69m, lines, "1", new string('A', 44), "QR",
                    LineUnitCodes: ["KGM"]))).TrimStart('﻿'));

        var cbc = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        var cac = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");

        var quantity = xml.Root!.Element(cac + "InvoiceLine")!.Element(cbc + "InvoicedQuantity")!;
        Assert.Equal("KGM", quantity.Attribute("unitCode")!.Value);
    }

    /// <summary><b>والوحدةُ الأصلُ معاملُها واحدٌ بالضرورة</b> — وإلا أفسدت أسرتَها كلَّها.</summary>
    [Fact]
    public void ABaseUnit_MustHaveFactorOne() =>
        Assert.Throws<AccountingException>(() =>
            UnitOfMeasure.Create(1L, "PCE", "قطعة", "PCE", baseCode: null, factor: 12m));

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Product> ProductAsync(string unit)
    {
        await SetupAsync();
        var product = Product.Create(_companyId, $"P-{unit}", "صنف", unit,
            ProductType.Stockable, salePrice: 20m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    private async Task ReceiveAsync(Product product, decimal quantity, decimal unitCost, string? unitCode)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.CompanyId == _companyId);
        if (supplier is null)
        {
            supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync();
        }

        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", quantity, unitCost, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();

        var receipt = GoodsReceipt.CreateDraft(_companyId, supplier.Id, order.Id,
            _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("بضاعة", quantity, quantity, unitCost, product.Id,
            batchNumber: null, expiryDate: null, unitCode: unitCode);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task<decimal> OnHandAsync(long productId) =>
        (await new StockBalanceQuery(_db).ExecuteAsync(_companyId))
        .Where(x => x.ProductId == productId).Sum(x => x.Quantity);

    private async Task<long> CustomerIdAsync()
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CompanyId == _companyId);
        if (customer is null)
        {
            customer = Customer.Create(_companyId, "عميل", "311111111100003");
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        return customer.Id;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الوحدات", "310123456700066", BusinessProfile.Retail);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId)).Id;
    }
}
