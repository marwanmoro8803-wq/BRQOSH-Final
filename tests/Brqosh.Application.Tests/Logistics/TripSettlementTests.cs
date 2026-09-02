using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Logistics;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Logistics;

/// <summary>
/// (CEEF-230 · ط3-٤ب) <b>عهدةُ الرحلة أصلٌ لا مصروف — وربحيّةُ المسار تُشتقّ.</b>
///
/// <para><b>وهذا ما يُقاس هنا</b>: النطاقُ أثبت هويّةَ <c>هـ-٢٠</c> حساباً،
/// <b>وهذه تُثبت أنّها تصمد في الأستاذ</b> — فرصيدُ <c>1117</c> يعود صفراً بعد التسوية.</para>
/// </summary>
public class TripSettlementTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public TripSettlementTests()
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
    /// <b>القياسُ الحاكم: تسليمُ العهدة لا يمسّ المصروف بريال.</b>
    ///
    /// <para><b>وبغير ذلك تُصرف العهدةُ كلُّها مصروفاً يومَ تسليمها</b> — فتُضخَّم تكلفةُ
    /// المسار بما سيعود، <b>ويختفي النقدُ العائد من الصندوق</b>.</para>
    /// </summary>
    [Fact]
    public async Task AdvancingTheFloat_MovesAnAsset_AndNeverTouchesExpense()
    {
        await SetupAsync();
        var expenseBefore = await BalanceAsync("5");

        var trip = await TripAsync();
        await new DeliveryTripService(_db).AdvanceFloatAsync(
            _companyId, trip.Id, 2000m, new DateOnly(2026, 7, 1));

        Assert.Equal(2000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TripCashFloat));
        Assert.Equal(expenseBefore, await BalanceAsync("5"));
    }

    /// <summary>
    /// <b>وهويّةُ هـ-٢٠ في الأستاذ: التسويةُ تُفرغ العهدةَ تماماً.</b>
    ///
    /// <para>ألفان عهدةً، أُنفق ١٦٥٠ وأُعيد ٣٥٠: <b>يصير المصروفُ ١٦٥٠ ويعود النقدُ ٣٥٠</b>،
    /// <b>ورصيدُ <c>1117</c> صفر</b>. وبقاءُ رصيدٍ فيه هو الفرقُ الذي يمنعه النطاق.</para>
    /// </summary>
    [Fact]
    public async Task Settling_EmptiesTheFloatAccountExactly()
    {
        await SetupAsync();
        var trip = await TripAsync();
        var service = new DeliveryTripService(_db);

        await service.AdvanceFloatAsync(_companyId, trip.Id, 2000m, new DateOnly(2026, 7, 1));

        trip.Dispatch();
        trip.AddExpense(TripExpenseKind.Fuel, 1650m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 3));
        await _db.SaveChangesAsync();

        await service.SettleAsync(_companyId, trip.Id, 350m, new DateOnly(2026, 7, 3));

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TripCashFloat));
        Assert.Equal(1650m, await BalanceAsync(DeliveryTripService.TripExpenseCode));
    }

    /// <summary>
    /// <b>وما دفعته الشركةُ مباشرةً مصروفٌ لا يمرّ بالعهدة.</b>
    ///
    /// <para><b>ومرورُه بها يُظهر على السائق التزاماً بما لم يأخذه</b> — فيُطالَب بصيانةٍ
    /// دُفعت ببطاقة الشركة.</para>
    /// </summary>
    [Fact]
    public async Task CompanyPaidExpenses_HitExpenseWithoutPassingThroughTheFloat()
    {
        await SetupAsync();
        var trip = await TripAsync();
        var service = new DeliveryTripService(_db);

        await service.AdvanceFloatAsync(_companyId, trip.Id, 1000m, new DateOnly(2026, 7, 1));

        trip.Dispatch();
        trip.AddExpense(TripExpenseKind.Fuel, 1000m, fromDriverFloat: true);
        trip.AddExpense(TripExpenseKind.Maintenance, 700m, fromDriverFloat: false);
        trip.Complete(new DateOnly(2026, 7, 3));
        await _db.SaveChangesAsync();

        await service.SettleAsync(_companyId, trip.Id, 0m, new DateOnly(2026, 7, 3));

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TripCashFloat));
        Assert.Equal(1700m, await BalanceAsync(DeliveryTripService.TripExpenseCode));
    }

    /// <summary>
    /// <b>وفرقُ العهدة يمنع التسويةَ ولا يُقيَّد شيء.</b>
    ///
    /// <para>والحارسُ في النطاق، <b>والكاتبُ يستدعيه قبل أن يبني قيداً</b> — فلا يُقيَّد
    /// فرقٌ صامتٌ في «مصاريفَ أخرى».</para>
    /// </summary>
    [Fact]
    public async Task AMismatchedFloat_IsRefusedAndPostsNothing()
    {
        await SetupAsync();
        var trip = await TripAsync();
        var service = new DeliveryTripService(_db);

        await service.AdvanceFloatAsync(_companyId, trip.Id, 2000m, new DateOnly(2026, 7, 1));

        trip.Dispatch();
        trip.AddExpense(TripExpenseKind.Fuel, 1650m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 3));
        await _db.SaveChangesAsync();

        // ‏**والرفضُ يُقاس بنصّه لا بوقوعه**: طفرةٌ نجت فأخرجت أنّ ابتلاعَ حارس هـ-٢٠
        // ‏**لا يُمرّر القيد** — يُخرجه غيرَ متوازنٍ فيرميه حارسُ التوازن. **فيبدو
        // ‏الاختبارُ أخضرَ وقد سقط الحارسُ المقصود**، وهما حارسان يستر أحدُهما الآخر.
        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            service.SettleAsync(_companyId, trip.Id, 300m, new DateOnly(2026, 7, 3)));

        Assert.Contains("مالٌ بلا حال", error.Message);

        // ‏العهدةُ باقيةٌ كما هي — ولا مصروفَ قُيّد.
        Assert.Equal(2000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TripCashFloat));
        Assert.Equal(0m, await BalanceAsync(DeliveryTripService.TripExpenseCode));
    }

    /// <summary>
    /// <b>وربحيّةُ المسار تُشتقّ من المستندات — والخاسرُ يظهر أوّلاً.</b>
    ///
    /// <para><b>وهذا هو الرقمُ الذي كان مستحيلاً</b>: أجورُ الشحنات من البوالص، ومصاريفُ
    /// الرحلة من سطورها. <b>والترتيبُ بالهامش يضع الخاسرَ في الأعلى</b> — فمَن يفتح
    /// الشاشةَ يراه أوّلَ شيء.</para>
    /// </summary>
    [Fact]
    public async Task RouteProfitability_DerivesFromDocuments_AndSurfacesTheLoser()
    {
        await SetupAsync();

        // ‏مسارٌ رابح: أجرةٌ ٩٠٠ بمصاريفَ ٤٠٠.
        var good = await TripAsync("TR-G", "الرياض — الخرج", freight: 900m);
        good.Dispatch();
        good.AddExpense(TripExpenseKind.Fuel, 400m, fromDriverFloat: false);
        good.Complete(new DateOnly(2026, 7, 3));

        // ‏ومسارٌ خاسر: أجرةٌ ٣٠٠ بمصاريفَ ٩٠٠.
        var bad = await TripAsync("TR-B", "الرياض — حائل", freight: 300m);
        bad.Dispatch();
        bad.AddExpense(TripExpenseKind.Fuel, 900m, fromDriverFloat: false);
        bad.Complete(new DateOnly(2026, 7, 3));
        await _db.SaveChangesAsync();

        var routes = await new DeliveryTripService(_db).RouteProfitabilityAsync(
            _companyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Equal(2, routes.Count);
        Assert.Equal("الرياض — حائل", routes[0].RouteName);
        Assert.Equal(-600m, routes[0].Margin);
        Assert.Equal(500m, routes[1].Margin);
    }

    /// <summary>
    /// <b>والرحلةُ الملغاةُ لا تدخل الربحيّة.</b>
    ///
    /// <para>لم تُنفَّذ، <b>وإدخالُها يُنقص متوسّطَ ربح المسار بمصاريفَ لم تقع</b>.</para>
    /// </summary>
    [Fact]
    public async Task ACancelledTrip_IsExcludedFromProfitability()
    {
        await SetupAsync();

        var trip = await TripAsync("TR-X", "الرياض — الدمام", freight: 500m);
        trip.AddExpense(TripExpenseKind.Fuel, 200m, fromDriverFloat: false);
        trip.Cancel();
        await _db.SaveChangesAsync();

        var routes = await new DeliveryTripService(_db).RouteProfitabilityAsync(
            _companyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Empty(routes);
    }

    private async Task<decimal> BalanceAsync(string prefix)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode.StartsWith(prefix))
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private int _seq;

    private async Task<DeliveryTrip> TripAsync(string number = "TR-1",
        string route = "الرياض — القصيم", decimal freight = 500m)
    {
        var shipment = Shipment.Create(_companyId, $"WB-{++_seq:000}", _customerId,
            consigneeName: "مستلم", destinationCity: "الوجهة",
            shipDate: new DateOnly(2026, 7, 1), freightCharge: freight, codAmount: 0m);
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        var trip = DeliveryTrip.Create(_companyId, number, route, "ABC-1234",
            new DateOnly(2026, 7, 1));
        trip.LoadShipment(shipment.Id, shipment.Status);
        _db.DeliveryTrips.Add(trip);
        await _db.SaveChangesAsync();

        return trip;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة شحن", "310123456700088", BusinessProfile.Logistics);
        _companyId = company.Id;

        var customer = Domain.Sales.Customer.Create(_companyId, "تاجر");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
