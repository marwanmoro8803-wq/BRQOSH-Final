using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Logistics;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Logistics;

/// <summary>
/// (CEEF-232 · ل-١) <b>أجرةُ الشحن — إيرادُ القطاع كلِّه، ولم تكن تبلغ الأستاذ أبداً.</b>
///
/// <para><b>ما كان:</b> <c>Shipment.FreightCharge</c> يُقرأ في موضعين لا ثالثَ لهما —
/// <b>عرضٌ على الشاشة</b> و<b>حسابُ ربحيّة الخطّ</b> — <b>ولا خدمةَ ترحيلٍ تمسّه ولا
/// فاتورةَ تحمله</b>.</para>
///
/// <para><b>والأثرُ أنّ القطاع يظهر خاسراً بكامل أجرته:</b> تكاليفُ الرحلات تُرحَّل على
/// <c>5206</c> <b>والإيرادُ خارجَ الدفاتر</b> — <b>وتقريرُ ربحيّة الخطّ يطرح مصروفاً
/// مُرحَّلاً من إيرادٍ غيرِ مُرحَّل</b>، فلا يُطابق قائمةَ الدخل أبداً.</para>
///
/// <para><b>وهويّةُ ل-١:</b> بعد الفوترة: <c>إيرادُ الأستاذ = مجموعُ أجور الشحنات
/// المفوترة</c>، <b>وذمّةُ التاجر تزيد بها وبضريبتها</b> — في الأستاذ وفي الدفتر
/// المساعد معاً. <b>وكلُّ شحنةٍ تُفوتَر مرّةً واحدة.</b></para>
/// </summary>
public class ShipmentFreightBillingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _shipperId;

    private static readonly DateOnly Ship = new(2026, 3, 1);
    private static readonly DateOnly BillOn = new(2026, 3, 31);

    public ShipmentFreightBillingTests()
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
    /// <b>القياسُ الحاكم: الأجرةُ تبلغ الأستاذ إيراداً — وذمّةَ التاجر.</b>
    ///
    /// <para>شحنتان بأجرةِ ١٠٠ و٢٠٠ ⟵ <b>إيرادٌ ٣٠٠</b> وضريبةٌ ٤٥ <b>وذمّةٌ ٣٤٥</b>.
    /// <b>وكان الأستاذُ يقول صفراً في الثلاثة.</b></para>
    /// </summary>
    [Fact]
    public async Task DeliveredFreight_ReachesRevenue_AndTheShippersAccount()
    {
        await SetupAsync();
        await DeliveredShipmentAsync("WB-1", 100m);
        await DeliveredShipmentAsync("WB-2", 200m);

        var result = await new ShipmentBillingService(_db)
            .BillShipperAsync(_companyId, _shipperId, BillOn);

        Assert.Equal(2, result.Shipments);
        Assert.Equal(300m, result.Freight);
        Assert.NotNull(result.InvoiceId);

        Assert.Equal(-300m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
        Assert.Equal(-45m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
        Assert.Equal(345m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));

        // ‏**وهويّةُ هـ-١**: الفاتورةُ مستندٌ، فالدفترُ المساعد يراها بلا عملٍ إضافيّ.
        Assert.Equal(345m, await SubledgerAsync());
    }

    /// <summary>
    /// <b>ولا تُفوتَر الأجرةُ مرّتين.</b>
    ///
    /// <para><b>وهو أخطرُ ما في «فوترةِ ما لم يُفوتَر»</b>: تشغيلةٌ ثانيةٌ في آخر الشهر
    /// <b>تُضاعف إيرادَ القطاع</b> وتُطالِب التاجرَ بأجرةٍ دفعها — <b>والحارسُ هو الربطُ
    /// نفسُه</b>، لا ذاكرةُ المستخدم.</para>
    /// </summary>
    [Fact]
    public async Task RunningTheBillingTwice_DoesNotBillTheSameShipmentAgain()
    {
        await SetupAsync();
        await DeliveredShipmentAsync("WB-1", 100m);

        var service = new ShipmentBillingService(_db);
        var first = await service.BillShipperAsync(_companyId, _shipperId, BillOn);
        var second = await service.BillShipperAsync(_companyId, _shipperId, BillOn);

        Assert.Equal(1, first.Shipments);
        Assert.Equal(0, second.Shipments);
        Assert.Null(second.InvoiceId);

        Assert.Equal(-100m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
    }

    /// <summary>
    /// <b>والمرتجعةُ تُفوتَر — والملغاةُ لا.</b>
    ///
    /// <para><b>والنطاقُ يقولها صراحةً</b> (<c>AReturnedShipment_CollectsNothingButKeepsItsFreight</c>):
    /// <b>الناقلُ نقل، والإرجاعُ لا يُسقط أجرته</b>. <b>وشحنةٌ أُلغيت قبل الانطلاق لا
    /// أجرةَ لها</b> — وفوترتُها مطالبةٌ بخدمةٍ لم تُؤدَّ.</para>
    /// </summary>
    [Fact]
    public async Task AReturnedShipment_IsBilled_AndACancelledOneIsNot()
    {
        await SetupAsync();

        var returned = await ShipmentAsync("WB-R", 80m);
        returned.Dispatch();
        returned.Return(new DateOnly(2026, 3, 5));

        var cancelled = await ShipmentAsync("WB-C", 90m);
        cancelled.Cancel();
        await _db.SaveChangesAsync();

        var result = await new ShipmentBillingService(_db)
            .BillShipperAsync(_companyId, _shipperId, BillOn);

        Assert.Equal(1, result.Shipments);
        Assert.Equal(80m, result.Freight);
    }

    /// <summary>
    /// <b>وشحنةٌ في الطريق لا تُفوتَر — فالخدمةُ لم تُؤدَّ بعد.</b>
    ///
    /// <para><b>وفوترتُها اعترافٌ بإيرادٍ قبل أدائه</b>: ربعٌ يُقفل على أجورِ شحناتٍ
    /// ما زالت في الطريق، <b>وبعضُها يُلغى</b>.</para>
    /// </summary>
    [Fact]
    public async Task AShipmentInTransit_IsNotBilled()
    {
        await SetupAsync();
        var moving = await ShipmentAsync("WB-T", 70m);
        moving.Dispatch();
        await _db.SaveChangesAsync();

        var result = await new ShipmentBillingService(_db)
            .BillShipperAsync(_companyId, _shipperId, BillOn);

        Assert.Equal(0, result.Shipments);
        Assert.Null(result.InvoiceId);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Shipment> ShipmentAsync(string waybill, decimal freight)
    {
        var shipment = Shipment.Create(_companyId, waybill, _shipperId, "مستلم", "جدة",
            Ship, freightCharge: freight, codAmount: 0m);
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();
        return shipment;
    }

    private async Task<Shipment> DeliveredShipmentAsync(string waybill, decimal freight)
    {
        var shipment = await ShipmentAsync(waybill, freight);
        shipment.Dispatch();
        shipment.Deliver(new DateOnly(2026, 3, 3), "المستلم");
        await _db.SaveChangesAsync();
        return shipment;
    }

    private async Task<decimal> SubledgerAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _shipperId)).Balance;

    private async Task<decimal> BalanceAsync(string code)
    {
        var accountId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == code)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync();
        if (accountId is null) return 0m;

        return await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == accountId)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0m;
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
