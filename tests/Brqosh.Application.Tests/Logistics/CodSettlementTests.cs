using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Logistics;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Logistics;

/// <summary>
/// (CEEF-230 · ط3-٤) <b>اللوجستيات — والدفعُ عند الاستلام أخطرُ ما فيها.</b>
///
/// <para><b>ما كان:</b> ملفُّ «اللوجستيات» في شاشة التأسيس <b>ولا شحنةَ في النظام ولا
/// بوليصة</b>. فالشركةُ تُدير بوالصَها في جدولٍ خارجيّ <b>وتُقيّد ما تحصّله كلَّه
/// إيراداً</b>.</para>
///
/// <para><b>والأثرُ عشرون ضعفاً:</b> شركةُ توصيلٍ تُحصّل مليونَ ريالٍ شهرياً نيابةً عن
/// التجّار <b>وأجرتُها خمسون ألفاً</b>. فقيدُ المليون إيراداً يُظهر شركةً بحجمٍ ليس لها،
/// <b>وتُدفع عنه زكاةٌ وضريبةٌ على مالٍ يُردّ</b>، وتُوزَّع أرباحٌ من أمانةٍ في اليد.</para>
///
/// <para><b>وهويّةُ هـ-١٣:</b> رصيدُ <c>2114</c> = المحصَّلُ − المُورَّد، <b>ولا يكون
/// سالباً أبداً</b>.</para>
/// </summary>
public class CodSettlementTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _shipperId;

    public CodSettlementTests()
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
    /// <b>القياسُ الحاكم: المحصَّلُ التزامٌ، ولا يمسّ الإيرادَ بريال.</b>
    ///
    /// <para>عشرُ بوالصَ بألفٍ لكلٍّ ⟵ <b>عشرةُ آلافٍ التزاماً</b> على الشركة للتاجر،
    /// <b>وصفرٌ إيراداً</b>. وقيدُها إيراداً يُظهر شركةً بعشرة أضعاف حجمها.</para>
    /// </summary>
    [Fact]
    public async Task CollectedCash_IsALiabilityAndNeverRevenue()
    {
        await SetupAsync();
        var ids = await DeliveredAsync(("WB-1", 1_000m), ("WB-2", 1_000m), ("WB-3", 1_000m));

        await new CodSettlementService(_db).CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));

        Assert.Equal(-3_000m, await BalanceAsync(CodSettlementService.CodPayableCode));
        Assert.Equal(3_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.OtherIncome));
    }

    /// <summary>
    /// <b>والتوريدُ يُغلق الالتزامَ بمقداره — فيعود الرصيدُ صفراً.</b>
    ///
    /// <para>وهي هويّةُ <c>هـ-١٣</c>: المحصَّلُ − المُورَّد.</para>
    /// </summary>
    [Fact]
    public async Task RemittingToTheMerchant_ClosesTheLiability()
    {
        await SetupAsync();
        var ids = await DeliveredAsync(("WB-4", 2_500m), ("WB-5", 1_500m));
        var service = new CodSettlementService(_db);

        await service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));
        var result = await service.RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 10));

        Assert.Equal(2, result.Shipments);
        Assert.Equal(4_000m, result.CodRemitted);
        Assert.Equal(0m, await BalanceAsync(CodSettlementService.CodPayableCode));
        Assert.Equal(0m, await service.HeldForShipperAsync(_companyId, _shipperId));
    }

    /// <summary>
    /// <b>ولا يُورَّد ما لم يُحصَّل.</b>
    ///
    /// <para>وتوريدُه <b>يُخرج الالتزامَ مديناً</b> — أصلٌ موهومٌ في الميزانية لا يقابله
    /// حقّ، <b>والشركةُ دفعت من مالها وهي تظنّها أمانة</b>. وهو ما يمنعه <c>هـ-١٣</c>.</para>
    /// </summary>
    [Fact]
    public async Task NothingIsRemittedBeforeItIsCollected()
    {
        await SetupAsync();
        var shipment = Shipment.Create(_companyId, "WB-6", _shipperId, "مستلم", "جدة",
            new DateOnly(2026, 3, 1), freightCharge: 25m, codAmount: 900m);
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new CodSettlementService(_db).RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 10)));

        Assert.Contains("لا مبالغَ محصَّلةً", error.Message, StringComparison.Ordinal);
        Assert.Equal(0m, await BalanceAsync(CodSettlementService.CodPayableCode));
    }

    /// <summary>
    /// <b>والنطاقُ يرفضه كذلك — لا الخدمةُ وحدَها.</b>
    ///
    /// <para><b>وهذا القياسُ أخرجته طفرةٌ نجت</b>: ترشيحُ الخدمة (<c>CodCollected</c>)
    /// كان يحجب حارسَ النطاق فلا تصله طفرةٌ قطّ. <b>وحارسٌ لا يصله قياسٌ ليس حارساً بل
    /// نيّة</b> — ويومَ يُستدعى النطاقُ من مسارٍ آخر لا يحميه شيء.</para>
    /// </summary>
    [Fact]
    public void TheDomainItself_RefusesToRemitWhatWasNotCollected()
    {
        var shipment = Shipment.Create(1L, "WB-D1", 1L, "مستلم", "الرياض",
            new DateOnly(2026, 3, 1), freightCharge: 25m, codAmount: 900m);

        var error = Assert.Throws<AccountingException>(shipment.MarkCodRemitted);
        Assert.Contains("لم يُحصَّل", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ويرفض الثانيةَ كذلك</b> — وبالقياس نفسِه.
    ///
    /// <para><b>(جاهزيّةُ الإطلاق · ح-١٢) وتهيئةُ هذه الحالة كانت تعتمد على العطل نفسِه</b>:
    /// كانت تُورِّد فورَ التسليم <b>بلا قيدِ تحصيلٍ إطلاقاً</b> — وهو ما صار مرفوضاً، لأنّه
    /// يُمدِّن <c>2114</c> بما لم يُدَن به. <b>والمقصدُ باقٍ ولم يُمسّ</b> (لا توريدَ مرّتين)،
    /// وإنّما زالت المقدّمةُ الباطلة: يُقيَّد التحصيلُ أوّلاً كما يقع في المنتَج.</para>
    /// </summary>
    [Fact]
    public void TheDomainItself_RefusesASecondRemittance()
    {
        var shipment = Shipment.Create(1L, "WB-D2", 1L, "مستلم", "الرياض",
            new DateOnly(2026, 3, 1), freightCharge: 25m, codAmount: 900m);
        shipment.Deliver(new DateOnly(2026, 3, 3), "مستلم");
        shipment.MarkCodCollectionPosted();
        shipment.MarkCodRemitted();

        var error = Assert.Throws<AccountingException>(shipment.MarkCodRemitted);
        Assert.Contains("سلفاً", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا يُورَّد مرّتين</b> — والثانيةُ خروجُ مالٍ بلا سبب، وتترك الالتزامَ مديناً.
    /// </summary>
    [Fact]
    public async Task TheSameCollection_IsNotRemittedTwice()
    {
        await SetupAsync();
        var ids = await DeliveredAsync(("WB-7", 700m));
        var service = new CodSettlementService(_db);

        await service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));
        await service.RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 10));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => service.RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 11)));

        // ‏**والرفضُ يُقاس بنصّه**: الترشيحُ في الخدمة يجب أن يستبعد المورَّدَ سلفاً
        // ‏**فلا يصل إلى النطاق أصلاً**. ولولا النصّ لمرَّ نزعُ الترشيح خضراءَ — يرفضه
        // ‏النطاقُ برسالةٍ أخرى، **والدفعةُ حينها تسقط كلُّها لأجل بوليصةٍ وُرِّدت**.
        Assert.Contains("لا مبالغَ محصَّلةً", error.Message, StringComparison.Ordinal);
        Assert.Equal(0m, await BalanceAsync(CodSettlementService.CodPayableCode));
    }

    /// <summary>
    /// <b>وشحنةٌ لم تُسلَّم لا يُحصَّل مالُها.</b>
    ///
    /// <para><b>والتسليمُ هو ما يُحصّل</b>. وتحصيلُ ما في الطريق يُنشئ التزاماً للتاجر
    /// بمالٍ لم يدخل الصندوق — <b>ثمّ يُورَّد إليه من مال الشركة</b>.</para>
    /// </summary>
    [Fact]
    public async Task AShipmentStillInTransit_HasNothingToCollect()
    {
        await SetupAsync();
        var shipment = Shipment.Create(_companyId, "WB-8", _shipperId, "مستلم", "الدمام",
            new DateOnly(2026, 3, 1), freightCharge: 25m, codAmount: 400m);
        shipment.Dispatch();
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new CodSettlementService(_db).CollectAsync(
                _companyId, [shipment.Id], new DateOnly(2026, 3, 5)));

        Assert.Contains("WB-8", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشحنةٌ بلا «دفعٍ عند الاستلام» تُسلَّم ولا يُقبض عنها شيء.</b>
    ///
    /// <para><b>وافتراضُ التحصيل مع كلّ تسليم</b> يُنشئ التزاماً بمالٍ لم يدخل — وهو
    /// أخطرُ من نقصِ تحصيل، لأنّه يُخرج مالاً.</para>
    /// </summary>
    [Fact]
    public void DeliveringAShipmentWithNoCod_CollectsNothing()
    {
        var shipment = Shipment.Create(1L, "WB-9", 1L, "مستلم", "الرياض",
            new DateOnly(2026, 3, 1), freightCharge: 25m, codAmount: 0m);

        shipment.Deliver(new DateOnly(2026, 3, 3), "مستلم");

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.False(shipment.CodCollected);
    }

    /// <summary>
    /// <b>وشحنةٌ مرتجعةٌ لا تحصيلَ لها — وأجرتُها تبقى على التاجر.</b>
    ///
    /// <para>الشركةُ نقلت ورجعت، والخدمةُ قُدِّمت. <b>وإسقاطُ الأجرة يُعدم إيراداً استُحقّ</b>.</para>
    /// </summary>
    [Fact]
    public void AReturnedShipment_CollectsNothingButKeepsItsFreight()
    {
        var shipment = Shipment.Create(1L, "WB-10", 1L, "مستلم", "أبها",
            new DateOnly(2026, 3, 1), freightCharge: 30m, codAmount: 600m);
        shipment.Dispatch();

        shipment.Return(new DateOnly(2026, 3, 6));

        Assert.Equal(ShipmentStatus.Returned, shipment.Status);
        Assert.False(shipment.CodCollected);
        Assert.Equal(30m, shipment.FreightCharge);

        // ‏**والحمايةُ في حارس الحالة لا في تصفيرِ العلَم**: `Return` لا تُقبل إلا من
        // ‏«منشأة» أو «في الطريق»، ولم يُحصَّل فيهما شيء. **وقد كان التصفيرُ مكتوباً
        // ‏فنجَت طفرةُ نزعِه** — فحُذف، لأنّ سطراً لا تقتله طفرةٌ لا يحرس شيئاً.
    }

    /// <summary><b>وشحنةٌ سُلّمت لا تُردّ</b> — والإرجاعُ بوليصةٌ جديدةٌ لا تعديلُ حالة.</summary>
    [Fact]
    public void ADeliveredShipment_CannotBeTurnedIntoAReturn()
    {
        var shipment = Shipment.Create(1L, "WB-11", 1L, "مستلم", "تبوك",
            new DateOnly(2026, 3, 1), freightCharge: 30m, codAmount: 600m);
        shipment.Deliver(new DateOnly(2026, 3, 4), "مستلم");

        Assert.Throws<AccountingException>(() => shipment.Return(new DateOnly(2026, 3, 6)));
        Assert.True(shipment.CodCollected);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-٤٨ · <c>sector-modules#8</c>) <b>التوريدُ للتاجر يخرج من حساب البنك
    /// نفسِه — لا من الأب <c>1102</c>.</b> والشاشةُ تستدعيه بلا حساب، <b>فكان كلُّ توريدٍ يُقيَّد
    /// على الأب</b> والكشفُ في الابن — فرقٌ في التسوية البنكيّة لا يُغلق.
    /// </summary>
    [Fact]
    public async Task Remittance_LeavesTheBankAccount_NotItsParent()
    {
        await SetupAsync();
        var bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "B1", "البنك الأول", "مصرف الرياض", "1234567890",
            "SA0380000000608010167519", isDefault: true);
        var bankGl = (await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId)).Code;

        var ids = await DeliveredAsync(("WB-B1", 700m), ("WB-B2", 300m));
        var service = new CodSettlementService(_db);
        await service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));
        await service.RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 10));

        Assert.Equal(-1_000m, await BalanceAsync(bankGl));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<List<long>> DeliveredAsync(params (string Waybill, decimal Cod)[] shipments)
    {
        var ids = new List<long>();
        foreach (var (waybill, cod) in shipments)
        {
            var shipment = Shipment.Create(_companyId, waybill, _shipperId, "مستلم", "الرياض",
                new DateOnly(2026, 3, 1), freightCharge: 25m, codAmount: cod);
            shipment.Dispatch();
            shipment.Deliver(new DateOnly(2026, 3, 4), "مستلم");
            _db.Shipments.Add(shipment);
            await _db.SaveChangesAsync();
            ids.Add(shipment.Id);
        }

        return ids;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة توصيل", "310123456700033", BusinessProfile.Logistics);
        _companyId = company.Id;

        var shipper = Customer.Create(_companyId, "متجر إلكترونيّ", "311111111100003");
        _db.Customers.Add(shipper);
        await _db.SaveChangesAsync();
        _shipperId = shipper.Id;
    }
}
