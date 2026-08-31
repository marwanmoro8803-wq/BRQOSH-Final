using Brqosh.Domain.Accounting;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (CEEF-229 · م1-٢٠ / I-8) <b>فرقُ سعر الشراء — واستحقاقٌ يُتابَع بالقيمة فيُخطئ الكمية.</b>
///
/// <para><b>الهويةُ الحاكمة هنا — سمِّها هـ-٨:</b> إذا فُوتِر <b>كلُّ</b> ما استُلم على أمر
/// شراء، فحسابُ «بضاعة مستلمة غير مفوترة» (2112) <b>صفرٌ بالضرورة</b>. فالاستحقاقُ جسرٌ
/// بين لحظتين، وبقاؤه بعد عبور الطرفين يعني <b>التزاماً وهمياً في الميزانية لا يُقفله شيء</b>.</para>
///
/// <para><b>وما كان يقع:</b> الاستحقاقُ يُتابَع <b>بالقيمة</b>:
/// <c>relief = min(قيمة سطر الفاتورة، القيمة المتبقّية)</c>. فحين ينخفض السعرُ عمّا استُلم به،
/// تُحرَّر قيمةُ الفاتورة وحدها ويبقى الفرقُ في 2112 <b>إلى الأبد</b> — ولا يُقيَّد له فرقُ
/// سعرٍ لأنّ <c>priceVariance</c> موجبٌ فقط حين يتجاوز السطرُ المتبقّي.</para>
///
/// <para><b>وحدودُ الأثر مقيسة، لا مُقدَّرة.</b> أوّلُ صياغةٍ لهذا القياس استعملت فرقاً
/// بـ٢٠٪ فسقطت بـ<c>فشل المطابقة الثلاثية: سعر «مواد» ينحرف 20.0% عن أمر الشراء (تسامح 2%)</c>
/// — <b>فثمّة حارسٌ قائمٌ يرفض الانحرافَ الكبير أصلاً</b>. فالعطلُ محصورٌ داخل التسامح
/// (٢٪ افتراضاً، <b>وهو معاملٌ قابلٌ للرفع</b>)، ويظهر بريالٍ أو ريالين على كلّ أمر شراء.</para>
///
/// <para><b>وصِغَرُه لا يُبطله:</b> هو التزامٌ في الميزانية <b>لا يُقفله شيءٌ أبداً</b>،
/// فيتراكم أمرَ شراءٍ بعد آخر، ولا يُنبّه عليه تقريرٌ لأنّ كلّ قيدٍ متوازن.</para>
///
/// <para><b>وحالةُ الانخفاض هي الأشيع</b>: خصمُ كمية، أو حسمُ تسويةٍ عند الفوترة، أو سعرُ
/// عرضٍ أفضلُ ممّا سُجّل في الإذن.</para>
///
/// <para><b>ولا تكشفه أيُّ هويةٍ قائمة:</b> هـ-٣ صادقة (المخزونُ لم يُمَسّ مع وجود إذن)،
/// وهـ-٢ صادقة (ذمّةُ المورد بقيمة الفاتورة)، والقيدُ متوازن. <b>العطلُ سطرٌ زائدٌ في
/// الميزانية لا خللٌ في ميزان المراجعة.</b></para>
/// </summary>
public class PurchasePriceVarianceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public PurchasePriceVarianceTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياس المركزيّ — السعرُ ينخفض.</b> استُلمت 10 بـ10 (استحقاق 100)، وفُوتِرت 10 بـ9.9
    /// — انحرافُ ١٪، داخل التسامح. فالاستحقاقُ يُقفَل كاملاً (100)، والفرقُ <b>ريالٌ واحد
    /// لصالح المنشأة</b> يُقيَّد دائناً في 5108.
    /// </summary>
    [Fact]
    public async Task WhenTheInvoicedPriceIsLower_TheAccrualStillClosesAndTheGainIsBooked()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        var order = await ReceiveAsync(companyId, supplierId, productId, warehouseId, 10m, 10m);

        Assert.Equal(-100m, await LedgerAsync(companyId, "2112"));

        await InvoiceAsync(companyId, supplierId, order, productId, 10m, 9.9m);

        Assert.True(0m == await LedgerAsync(companyId, "2112"),
            $"بقي في 2112 مبلغُ {await LedgerAsync(companyId, "2112")} بعد فوترة كلّ ما استُلم — "
            + "التزامٌ وهميٌّ في الميزانية لا يُقفله شيءٌ أبداً.");

        Assert.True(-1m == await LedgerAsync(companyId, "5108"),
            $"5108 يقول {await LedgerAsync(companyId, "5108")} والصواب −1 — "
            + "فالفرقُ لصالح المنشأة لم يُقيَّد.");

        await AssertAccrualIdentityAsync(companyId);
    }

    /// <summary>
    /// <b>والسعرُ يرتفع</b> — وهو الاتّجاه الذي كان يعمل. يُقاس كي لا يُكسر بالإصلاح:
    /// استُلمت 10 بـ10، فُوتِرت 10 بـ10.1 ⟵ الاستحقاق يُقفَل، وريالٌ مديناً في 5108.
    /// </summary>
    [Fact]
    public async Task WhenTheInvoicedPriceIsHigher_TheExcessIsExpensed()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        var order = await ReceiveAsync(companyId, supplierId, productId, warehouseId, 10m, 10m);

        await InvoiceAsync(companyId, supplierId, order, productId, 10m, 10.1m);

        Assert.Equal(0m, await LedgerAsync(companyId, "2112"));
        Assert.Equal(1m, await LedgerAsync(companyId, "5108"));

        await AssertAccrualIdentityAsync(companyId);
    }

    /// <summary>
    /// <b>الفوترةُ الجزئية — والاستحقاقُ يجب أن يُستهلَك بالكمية لا بالقيمة.</b>
    ///
    /// <para>استُلمت 10 بـ10 (استحقاق 100). ثمّ فاتورةٌ أولى بـ4 بسعر <b>9.9</b>، فتستهلك
    /// <b>أربعَ قطع</b> — أيْ 40 من الاستحقاق — ويُقيَّد فرقُ 0.40 لصالح المنشأة. والمتبقّي
    /// <b>60</b> لا 60.40.</para>
    ///
    /// <para>ثمّ فاتورةٌ ثانية بـ6 بسعر 10 تُقفل الباقي تماماً ⟵ <b>2112 صفر</b>.</para>
    ///
    /// <para><b>وهنا يظهر خطأُ المتابعة بالقيمة مضاعفاً</b>: يطرح ما دفعته الفاتورة لا ما
    /// استهلكته من البضاعة، فيتراكم الانحرافُ فاتورةً بعد فاتورة.</para>
    /// </summary>
    [Fact]
    public async Task PartialInvoicesConsumeTheAccrualByQuantity_NotByValue()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        var order = await ReceiveAsync(companyId, supplierId, productId, warehouseId, 10m, 10m);

        await InvoiceAsync(companyId, supplierId, order, productId, 4m, 9.9m);

        Assert.True(-60m == await LedgerAsync(companyId, "2112"),
            $"بعد فوترة 4 من 10 صار 2112 يساوي {await LedgerAsync(companyId, "2112")} "
            + "والصواب −60. فالاستحقاقُ استُهلك بقيمة ما دُفع لا بكمّية ما فُوتِر.");

        await InvoiceAsync(companyId, supplierId, order, productId, 6m, 10m);

        Assert.True(0m == await LedgerAsync(companyId, "2112"),
            $"بقي في 2112 مبلغُ {await LedgerAsync(companyId, "2112")} بعد فوترة العشرة كلِّها.");

        Assert.Equal(-0.4m, await LedgerAsync(companyId, "5108"));

        await AssertAccrualIdentityAsync(companyId);
    }

    /// <summary>
    /// <b>وفاتورةٌ تتجاوز ما استُلم تُرفض أصلاً — وهو ما كشفه القياس لا القراءة.</b>
    ///
    /// <para>أوّلُ صياغةٍ لهذا الاختبار افترضت أنّ الفوترةَ تمرّ ويُحرَّر ما استُحقّ وحده،
    /// فسقطت بـ<c>كمية فاتورة «مواد» (10.0000) تتجاوز المستلم (4.0000)</c>. <b>فالمطابقةُ
    /// الثلاثية تحرس البابَ قبل أن يبلغه الاستحقاق</b>، وهو حارسٌ قائمٌ لم أعلم به.</para>
    ///
    /// <para>فصار الاختبارُ يحرس <b>الحارس</b>: إسقاطُ فحص الكمية في `ThreeWayMatchService`
    /// يجعل هذا يمرّ صامتاً، ومعه ينفتح بابُ تحريرِ استحقاقٍ لبضاعةٍ لم تصل.</para>
    /// </summary>
    [Fact]
    public async Task InvoicingMoreThanReceived_IsRefusedByTheThreeWayMatch()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupAsync();
        var order = await ReceiveAsync(companyId, supplierId, productId, warehouseId,
            orderedQuantity: 10m, receivedQuantity: 4m, unitCost: 10m);

        var refusal = await Assert.ThrowsAsync<AccountingException>(
            () => InvoiceAsync(companyId, supplierId, order, productId, 10m, 10m));

        Assert.Contains("تتجاوز المستلم", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(-40m, await LedgerAsync(companyId, "2112"));

        await AssertAccrualIdentityAsync(companyId);
    }

    /// <summary>هـ-٨ مع الهويات الرصيدية الثلاث — فالإصلاحُ لا يُقاس بحسابٍ واحد.</summary>
    private async Task AssertAccrualIdentityAsync(long companyId)
    {
        LedgerIdentities.Assert(
            await LedgerIdentities.GoodsReceivedNotInvoicedAsync(_db, companyId));
        await LedgerIdentities.AssertAllAsync(_db, companyId);
    }

    private async Task<(long CompanyId, long SupplierId, long ProductId, long WarehouseId)> SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الفروق", "310123456700055", BusinessProfile.Retail);

        var supplier = Supplier.Create(company.Id, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(company.Id, "P-PPV", "صنف مخزني", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var warehouse = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        return (company.Id, supplier.Id, product.Id, warehouse.Id);
    }

    private Task<PurchaseOrder> ReceiveAsync(
        long companyId, long supplierId, long productId, long warehouseId,
        decimal quantity, decimal unitCost) =>
        ReceiveAsync(companyId, supplierId, productId, warehouseId, quantity, quantity, unitCost);

    private async Task<PurchaseOrder> ReceiveAsync(
        long companyId, long supplierId, long productId, long warehouseId,
        decimal orderedQuantity, decimal receivedQuantity, decimal unitCost)
    {
        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", orderedQuantity, unitCost, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(
            companyId, supplierId, order.Id, warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("مواد", orderedQuantity, receivedQuantity, unitCost, productId);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        return order;
    }

    private async Task InvoiceAsync(long companyId, long supplierId, PurchaseOrder order,
        long productId, decimal quantity, decimal unitPrice)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            companyId, supplierId, new DateOnly(2026, 8, 5),
            $"SUP-{quantity:0}-{unitPrice:0}", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("مواد", quantity, unitPrice, productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>رصيدٌ مُوقَّع من ميزان المراجعة: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(long companyId, string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
