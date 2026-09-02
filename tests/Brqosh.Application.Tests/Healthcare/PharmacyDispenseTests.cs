using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Healthcare;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Healthcare;

/// <summary>
/// (CEEF-231 · ط3-٢ج) <b>الدواءُ المصروف يخرج من المستودع.</b>
///
/// <para><b>ما كان:</b> <c>VisitLine.ProductId</c> حقلٌ يُكتب ويُخزَّن <b>ولا يقرؤه أحدٌ
/// في المستودع كلِّه</b>. فالمستوصفُ يصرف علبةَ دواءٍ بمئتين، <b>والدواءُ في المخزن لا
/// ينقص</b>: تكلفةُ المبيعات صفر، والربحُ مئةٌ بالمئة، <b>ثمّ يُقيَّد الفرقُ «عجزَ جرد»
/// آخرَ السنة</b> وهو صرفٌ صحيحٌ بلا مصروف. <b>وقوائمُ سنةٍ كاملةٍ تُقدَّم للزكاة بربحٍ
/// لا وجودَ له.</b></para>
///
/// <para><b>وهويّةُ هـ-١٨:</b> بعد فوترةِ زيارةٍ تحمل <c>ن</c> من دواء، ينقص رصيدُه
/// <c>ن</c> بالضبط <b>وتُقيَّد تكلفتُه</b> — لا أقلّ ولا أكثر.</para>
/// </summary>
public class PharmacyDispenseTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _patientId;
    private long _payerId;
    private int _seq;

    public PharmacyDispenseTests()
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

    // ── هـ-١٨: ما صُرف نقص ──────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: فوترةُ زيارةٍ تُنقص الدواءَ وتُقيّد تكلفتَه.</b>
    ///
    /// <para><b>وكلا الطرفين يُقاس</b>: الرصيدُ ينقص <b>وتكلفةُ المبيعات تُدين</b>.
    /// فصرفٌ بلا قيدٍ يُنقص المخزونَ في الجرد ولا يُنقصه في الميزانية، <b>وقيدٌ بلا
    /// صرفٍ يعكس ذلك</b> — وكلاهما يترك فرقاً يُقيَّد «عجزَ جرد» آخرَ السنة.</para>
    /// </summary>
    [Fact]
    public async Task BillingAVisit_IssuesTheDispensedDrugAndBooksItsCost()
    {
        var product = await StockedDrugAsync(tracking: InventoryTracking.None);
        await ReceiveAsync(product, qty: 100m, unitCost: 12m, batchNumber: null, expiry: null);

        var cogsBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold);

        var visit = await VisitAsync(policyId: null,
            ("كشف", 1m, 200m, null, null),
            ("دواء", 4m, 50m, product.Id, null));

        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        Assert.Equal(96m, await OnHandAsync(product.Id));

        // ‏٤ × ١٢ = ٤٨ تكلفةً، لا ٤ × ٥٠ سعرَ بيع.
        Assert.Equal(cogsBefore + 48m,
            await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));
    }

    /// <summary>
    /// <b>وزيارةٌ بلا دواءٍ لا تُنشئ حركةَ مخزونٍ ولا قيدَ تكلفة.</b>
    ///
    /// <para><b>وحركةٌ فارغةٌ في كلّ كشفٍ تُغرِق سجلَّ المخزون</b> — والصمتُ عند لا عملٍ
    /// قياسٌ لا تفصيل، وهو الدرسُ نفسُه من تشغيلةِ ليالي الفندق.</para>
    /// </summary>
    [Fact]
    public async Task AVisitWithNoDrugs_MovesNoStock()
    {
        await SetupAsync();
        var before = await _db.StockMovements.CountAsync();

        var visit = await VisitAsync(policyId: null, ("كشف", 1m, 200m, null, null));
        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        Assert.Equal(before, await _db.StockMovements.CountAsync());
    }

    /// <summary>
    /// <b>ويُصرف مرّةً واحدةً وإن خرجت فاتورتان.</b>
    ///
    /// <para><b>وهذا هو القرارُ المعماريّ مقيساً</b>: المبلغُ يُقسَم على فاتورتين بنسبة
    /// التغطية، <b>والكمّيةُ لا تُقسَم</b>. فتعليقُ الصرف بالفواتير يُخرج الدواءَ مرّتين —
    /// <b>والمستودعُ ينقص ضعفَ ما صُرف</b> فتظهر أصنافٌ بأرصدةٍ سالبةٍ لا تُفسَّر.</para>
    /// </summary>
    [Fact]
    public async Task ACoveredVisit_IssuesTheDrugOnce_ThoughTwoInvoicesAreRaised()
    {
        var product = await StockedDrugAsync(tracking: InventoryTracking.None);
        await ReceiveAsync(product, qty: 100m, unitCost: 12m, batchNumber: null, expiry: null);

        var policy = await PolicyAsync(coveragePercent: 80m);

        var visit = await VisitAsync(policy.Id,
            ("كشف", 1m, 200m, null, null),
            ("دواء", 10m, 50m, product.Id, null));

        var result = await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        Assert.NotNull(result.PatientInvoiceId);
        Assert.NotNull(result.PayerInvoiceId);
        Assert.Equal(90m, await OnHandAsync(product.Id));
    }

    // ── الدفعةُ والصلاحية ───────────────────────────────────────────────────

    /// <summary>
    /// <b>ودواءٌ متتبَّعٌ يُصرف من دفعتِه بعينها.</b>
    /// </summary>
    [Fact]
    public async Task ATrackedDrug_IsDrawnFromItsNamedBatch()
    {
        var product = await StockedDrugAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 40m, 12m, "LOT-A", new DateOnly(2027, 1, 1));
        await ReceiveAsync(product, 60m, 12m, "LOT-B", new DateOnly(2028, 1, 1));

        var visit = await VisitAsync(policyId: null, ("دواء", 15m, 50m, product.Id, "LOT-B"));
        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        Assert.Equal(40m, (await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-A")).Quantity);
        Assert.Equal(45m, (await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-B")).Quantity);
    }

    /// <summary>
    /// <b>ودواءٌ متتبَّعٌ بلا رقمِ دفعةٍ يُرفض باسمه.</b>
    ///
    /// <para><b>والصمتُ أخطرُ من الرفض</b>: دواءٌ يخرج بلا هويّةٍ يخرج بلا صلاحية —
    /// <b>ولا يُستدعى إن سُحب من السوق</b>.</para>
    /// </summary>
    [Fact]
    public async Task ATrackedDrugWithoutABatch_IsRefusedByName()
    {
        var product = await StockedDrugAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 40m, 12m, "LOT-A", new DateOnly(2027, 1, 1));

        var visit = await VisitAsync(policyId: null, ("دواء", 5m, 50m, product.Id, null));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new VisitBillingService(_db).BillAsync(_companyId, visit.Id));

        Assert.Contains("هويّة", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ودفعةٌ منتهيةُ الصلاحية لا تُصرف للمريض.</b>
    /// </summary>
    [Fact]
    public async Task AnExpiredBatch_IsNotDispensed()
    {
        var product = await StockedDrugAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 40m, 12m, "LOT-OLD", new DateOnly(2026, 2, 1));

        var visit = await VisitAsync(policyId: null, ("دواء", 5m, 50m, product.Id, "LOT-OLD"));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new VisitBillingService(_db).BillAsync(_companyId, visit.Id));

        Assert.Contains("منتهيةُ الصلاحية", error.Message, StringComparison.Ordinal);
    }

    // ── الرفضُ المسمّى ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>وصنفٌ خدميٌّ لا يُصرف من المخزون — ويُرفض باسمه.</b>
    ///
    /// <para><b>وقبولٌ صامتٌ يُنشئ حركةً بلا أثر</b> ويُوهم بأنّ الصرفَ جرى.</para>
    /// </summary>
    [Fact]
    public async Task LinkingAServiceProduct_IsRefusedByName()
    {
        await SetupAsync();
        var service = Product.Create(_companyId, "SRV-1", "استشارة", "جلسة", ProductType.Service, 200m);
        _db.Products.Add(service);
        await _db.SaveChangesAsync();

        var visit = await VisitAsync(policyId: null, ("استشارة", 1m, 200m, service.Id, null));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new VisitBillingService(_db).BillAsync(_companyId, visit.Id));

        Assert.Contains("استشارة", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ورصيدٌ لا يكفي يمنع الفوترة كلَّها — ولا يُفوتَر دواءٌ لم يخرج.</b>
    ///
    /// <para><b>والمعاملةُ واحدة</b>: فاتورةٌ تُرحَّل ثمّ يتعثّر الصرف تترك مريضاً
    /// مُطالَباً بدواءٍ لم يُسلَّم، <b>وزيارةً مقفلةً لا تُفتح</b>.</para>
    /// </summary>
    [Fact]
    public async Task InsufficientStock_RollsBackTheWholeBilling()
    {
        var product = await StockedDrugAsync(InventoryTracking.None);
        await ReceiveAsync(product, 3m, 12m, null, null);

        var visit = await VisitAsync(policyId: null, ("دواء", 10m, 50m, product.Id, null));

        await Assert.ThrowsAsync<AccountingException>(
            () => new VisitBillingService(_db).BillAsync(_companyId, visit.Id));

        var stored = await _db.Visits.SingleAsync(v => v.Id == visit.Id);
        Assert.Equal(VisitStatus.Open, stored.Status);
        Assert.Empty(await _db.SalesInvoices.Where(i => i.CompanyId == _companyId).ToListAsync());
    }

    /// <summary>
    /// <b>ورقمُ دفعةٍ على سطرٍ بلا صنفٍ يُرفض في النطاق.</b>
    ///
    /// <para>الدفعةُ هويّةُ صنفٍ في مستودع — <b>وسطرُ خدمةٍ يحملها يُخزَّن بياناً لا
    /// يقرؤه أحد</b>، ثمّ يُقرأ يوماً فيُصرَف به.</para>
    /// </summary>
    [Fact]
    public async Task ABatchNumberWithoutAProduct_IsRefused()
    {
        await SetupAsync();
        var visit = Visit.Create(_companyId, _patientId, "V-X", new DateOnly(2026, 3, 1), "د. أحمد");

        var error = Assert.Throws<AccountingException>(
            () => visit.AddLine("كشف", 1m, 200m, batchNumber: "LOT-A"));

        Assert.Contains("بلا صنف", error.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> OnHandAsync(long productId) =>
        await _db.InventoryBalances
            .Where(b => b.CompanyId == _companyId && b.ProductId == productId)
            .SumAsync(b => b.QuantityOnHand);

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<Product> StockedDrugAsync(InventoryTracking tracking)
    {
        await SetupAsync();
        var product = Product.Create(_companyId, $"DRG-{++_seq}", $"دواء {_seq}", "علبة",
            ProductType.Stockable, salePrice: 50m);
        if (tracking != InventoryTracking.None)
            product.SetTracking(tracking, currentOnHand: 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    private async Task ReceiveAsync(Product product, decimal qty, decimal unitCost,
        string? batchNumber, DateOnly? expiry)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.CompanyId == _companyId);
        if (supplier is null)
        {
            supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync();
        }

        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 1, 1));
        order.AddLine("دواء", qty, unitCost, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();

        var receipt = GoodsReceipt.CreateDraft(_companyId, supplier.Id, order.Id,
            _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("دواء", qty, qty, unitCost, product.Id, batchNumber, expiry);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task<InsurancePolicy> PolicyAsync(decimal coveragePercent)
    {
        var policy = InsurancePolicy.Create(_companyId, _patientId, _payerId, $"POL-{++_seq}",
            "M-1", coveragePercent, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        _db.InsurancePolicies.Add(policy);
        await _db.SaveChangesAsync();
        return policy;
    }

    private async Task<Visit> VisitAsync(long? policyId,
        params (string Description, decimal Qty, decimal Price, long? ProductId, string? Batch)[] lines)
    {
        var visit = Visit.Create(_companyId, _patientId, $"V-{++_seq}", new DateOnly(2026, 3, 1),
            "د. أحمد", policyId);

        foreach (var (description, qty, price, productId, batch) in lines)
            visit.AddLine(description, qty, price, isCovered: true, productId: productId,
                batchNumber: batch);

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();
        return visit;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مستوصف القياس", "310123456700099", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId)).Id;

        var patientCustomer = Customer.Create(_companyId, "مريض", "");
        var payerCustomer = Customer.Create(_companyId, "شركة تأمين", "311111111100003");
        _db.Customers.AddRange(patientCustomer, payerCustomer);
        await _db.SaveChangesAsync();

        var patient = Patient.Create(_companyId, "MRN-1", "مريض", "1088888888",
            new DateOnly(1990, 1, 1), PatientGender.Male, patientCustomer.Id);
        var payer = InsurancePayer.Create(_companyId, "BUPA", "بوبا العربية", payerCustomer.Id);
        _db.Patients.Add(patient);
        _db.InsurancePayers.Add(payer);
        await _db.SaveChangesAsync();

        _patientId = patient.Id;
        _payerId = payer.Id;
    }
}
