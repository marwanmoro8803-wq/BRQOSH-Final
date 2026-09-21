using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Logistics;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Logistics;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٢) <b>التحصيلُ كان يُقيَّد مرّتين، والتوريدُ يسبقه.</b>
///
/// <para><b>الجذرُ واحد:</b> <c>Shipment.CodCollected</c> يُضبط <b>عند التسليم</b>
/// (أي «المالُ في يد المندوب») <b>ولا قيدَ معه</b>، <b>ولم يكن ثمّة وسمٌ يقول إنّ قيدَ
/// التحصيل وقع</b>.</para>
///
/// <para><b>فثقبان:</b> استدعاءُ التحصيل ثانيةً بالبوالص نفسِها <b>يُقيّد مرّةً أخرى</b> —
/// نقدٌ وهميٌّ والتزامٌ مضاعَفٌ في <c>2114</c>، <b>والقيدان متوازنان فلا يشكو الميزان</b>.
/// <b>وتوريدٌ قبل قيد التحصيل</b> يُمدِّن <c>2114</c> بما لم يُدَن به قطّ، <b>فيخرج
/// الالتزامُ سالباً</b> — أي أنّ الشركة دفعت من مالها وهي تحسبها أمانة.</para>
///
/// <para><b>وكلاهما كسرٌ لهويّة هـ-١٣:</b> «رصيدُ <c>2114</c> = المحصَّلُ − المُورَّد،
/// <b>ولا يكون سالباً أبداً</b>» — وهي مكتوبةٌ في صدر <c>CodSettlementService</c>
/// <b>ولم يكن يحرسها شيء</b>.</para>
/// </summary>
public class CodPostedOnceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _shipperId;

    public CodPostedOnceTests()
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

    // ── الثقبُ الأوّل: تحصيلٌ يُقيَّد مرّتين ────────────────────────────────

    /// <summary>
    /// <b>القياسُ الأوّل:</b> إعادةُ التحصيل على البوالص نفسِها تُرفض — <b>والرصيدُ
    /// يبقى ثلاثةَ آلاف لا ستّة</b>.
    /// </summary>
    [Fact]
    public async Task CollectingTwice_IsRefused_AndTheLiabilityIsNotDoubled()
    {
        await SetupAsync();
        var ids = await DeliveredAsync(("WB-1", 1_000m), ("WB-2", 1_000m), ("WB-3", 1_000m));
        var service = new CodSettlementService(_db);

        await service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));

        var again = await Assert.ThrowsAsync<AccountingException>(
            () => service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5)));

        Assert.Contains("سلفاً", again.Message, StringComparison.Ordinal);
        Assert.Equal(-3_000m, await LedgerAsync(CodSettlementService.CodPayableCode));
    }

    /// <summary>
    /// <b>وبوليصةٌ واحدةٌ مكرّرةٌ في دفعةٍ جديدةٍ تُسقط الدفعةَ كلَّها</b> — ولا تُقيَّد
    /// البقيّةُ وتُترك هي. <b>فقيدٌ جزئيٌّ أسوأُ من رفضٍ صريح.</b>
    /// </summary>
    [Fact]
    public async Task ABatchContainingAnAlreadyPostedWaybill_IsRefusedWhole()
    {
        await SetupAsync();
        var first = await DeliveredAsync(("WB-1", 1_000m));
        var second = await DeliveredAsync(("WB-2", 500m));
        var service = new CodSettlementService(_db);

        await service.CollectAsync(_companyId, first, new DateOnly(2026, 3, 5));

        await Assert.ThrowsAsync<AccountingException>(
            () => service.CollectAsync(_companyId, [.. first, .. second], new DateOnly(2026, 3, 6)));

        // ‏لم تُقيَّد الثانيةُ أيضاً — الرفضُ كلّيّ.
        Assert.Equal(-1_000m, await LedgerAsync(CodSettlementService.CodPayableCode));
    }

    // ── الثقبُ الثاني: توريدٌ قبل قيد التحصيل ───────────────────────────────

    /// <summary>
    /// <b>القياسُ الثاني:</b> بوليصةٌ سُلِّمت ولم يُقيَّد تحصيلُها <b>لا تُورَّد</b> —
    /// ولولاه لخرج <c>2114</c> مديناً بألف، أي أصلٌ موهومٌ لا يقابله حقّ.
    /// </summary>
    [Fact]
    public async Task RemittingBeforeTheCollectionIsPosted_IsRefused()
    {
        await SetupAsync();
        await DeliveredAsync(("WB-1", 1_000m));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new CodSettlementService(_db).RemitAsync(
                _companyId, _shipperId, new DateOnly(2026, 3, 10)));

        Assert.Equal(0m, await LedgerAsync(CodSettlementService.CodPayableCode));
        Assert.NotNull(error);
    }

    /// <summary>
    /// <b>وهويّةُ هـ-١٣ محروسةٌ بالقيمة:</b> بعد تحصيلٍ وتوريدٍ كاملَين يعود <c>2114</c>
    /// صفراً — <b>ولا يمرّ به سالبٌ في الطريق</b>.
    /// </summary>
    [Fact]
    public async Task AfterCollectingThenRemitting_TheLiabilityReturnsToZero()
    {
        await SetupAsync();
        var ids = await DeliveredAsync(("WB-1", 1_000m), ("WB-2", 2_000m));
        var service = new CodSettlementService(_db);

        await service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));
        Assert.Equal(-3_000m, await LedgerAsync(CodSettlementService.CodPayableCode));

        await service.RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 10));

        Assert.Equal(0m, await LedgerAsync(CodSettlementService.CodPayableCode));
    }

    // ── شاهدُ الضبط ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبط:</b> المسارُ السليمُ يمرّ كما كان — تسليمٌ ثمّ تحصيلٌ ثمّ توريد.
    /// <b>ولولاه لجاز أن يكون «الإصلاح» منعاً للتحصيل كلِّه</b> ويمرّ القياسان أعلاه.
    /// </summary>
    [Fact]
    public async Task TheHonestPath_StillWorks()
    {
        await SetupAsync();
        var ids = await DeliveredAsync(("WB-1", 750m));
        var service = new CodSettlementService(_db);

        var entryId = await service.CollectAsync(_companyId, ids, new DateOnly(2026, 3, 5));
        Assert.True(entryId > 0);

        var result = await service.RemitAsync(_companyId, _shipperId, new DateOnly(2026, 3, 10));

        Assert.NotNull(result);
        Assert.Equal(0m, await LedgerAsync(CodSettlementService.CodPayableCode));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

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

    /// <summary>رصيدٌ مُوقَّع: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
