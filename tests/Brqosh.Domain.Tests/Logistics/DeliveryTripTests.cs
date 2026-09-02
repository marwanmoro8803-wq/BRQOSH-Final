using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;

namespace Brqosh.Domain.Tests.Logistics;

/// <summary>
/// (CEEF-230 · ط3-٤ب) <b>رحلةُ التوصيل — والمسارُ الخاسرُ الذي لا يُرى.</b>
///
/// <para><b>ما كان:</b> الشحنةُ تحمل أجرتَها (<c>ط3-٤</c>)، <b>ولا شيءَ يحمل تكلفةَ
/// إيصالها</b>. فشركةٌ تُشغّل اثني عشرَ مساراً <b>لا تعرف أيَّها يخسر</b> — ويغطّي ربحُ
/// التسعةِ خسارةَ الثلاثة، <b>فتُجدَّد عقودُ الخاسر بالسعر نفسِه ويُوسَّع فيه</b>.</para>
///
/// <para><b>وهويّةُ هـ-٢٠:</b> <c>عهدةُ السائق = ما أُنفق منها + ما أُعيد</c> — وبقيّةٌ
/// بينهما <b>مالٌ في جيبٍ لا يعرف أحدٌ جيبَ مَن</b>.</para>
/// </summary>
public class DeliveryTripTests
{
    // ── هـ-٢٠: العهدة ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: فرقٌ في العهدة يُرفض بأرقامه الثلاثة.</b>
    ///
    /// <para>ألفان عهدةً، أُنفق ١٦٥٠ وأُعيد ٣٠٠: <b>خمسون بلا حال</b>. وبلا هذا الحارس
    /// <b>تُبتلع صامتةً</b> — إمّا تُصرف كلُّها مصروفاً فتُضخَّم تكلفةُ المسار، أو تبقى
    /// خروجاً نقدياً لا يُطابَق أبداً.</para>
    /// </summary>
    [Fact]
    public void AFloatThatDoesNotReconcile_IsRefusedByItsNumbers()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 1650m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 2));

        var error = Assert.Throws<AccountingException>(() => trip.SettleFloat(300m));

        Assert.Contains("2,000.00", error.Message);
        Assert.Contains("1,650.00", error.Message);
        Assert.Contains("50.00", error.Message);
        Assert.NotEqual(TripStatus.Settled, trip.Status);
    }

    /// <summary>
    /// <b>وتُسوَّى حين يُطبق الإنفاقُ والإعادةُ العهدةَ تماماً.</b>
    /// </summary>
    [Fact]
    public void AFloatThatReconciles_Settles()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 1400m, fromDriverFloat: true);
        trip.AddExpense(TripExpenseKind.Tolls, 250m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 2));

        trip.SettleFloat(350m);

        Assert.Equal(TripStatus.Settled, trip.Status);
        Assert.Equal(0m, trip.FloatVariance);
    }

    /// <summary>
    /// <b>وما دفعته الشركةُ مباشرةً لا يُسوَّى مع السائق.</b>
    ///
    /// <para>كلاهما تكلفةُ الرحلة، <b>والعهدةُ وحدَها تُسوَّى</b>. وخلطُهما يُخرج فرقَ
    /// عهدةٍ كاذباً <b>فيُطالَب السائقُ بما لم يأخذه</b> — أو يُبرَّأ ممّا أخذه.</para>
    /// </summary>
    [Fact]
    public void CompanyPaidExpenses_AreCostButNotPartOfTheFloat()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 1200m, fromDriverFloat: true);
        trip.AddExpense(TripExpenseKind.Maintenance, 900m, fromDriverFloat: false);
        trip.Complete(new DateOnly(2026, 7, 2));

        trip.SettleFloat(800m);

        Assert.Equal(TripStatus.Settled, trip.Status);
        Assert.Equal(1200m, trip.SpentFromFloat);
        Assert.Equal(2100m, trip.TotalExpenses);
    }

    /// <summary>
    /// <b>وإعادةُ أكثرَ ممّا أُخذ تُرفض.</b>
    ///
    /// <para><b>وهو تحصيلٌ لا إعادة</b> — يُخفي دخلاً في حساب العهد، فيظهر السائقُ
    /// أميناً والمالُ داخلٌ من مصدرٍ لا يُعرف.</para>
    /// </summary>
    [Fact]
    public void ReturningMoreThanTheFloat_IsRefused()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 500m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 2));

        var error = Assert.Throws<AccountingException>(() => trip.SettleFloat(2500m));

        Assert.Contains("تحصيلٌ لا إعادة", error.Message);
    }

    /// <summary>
    /// <b>ولا تُسوَّى عهدةُ رحلةٍ لم تنتهِ.</b>
    ///
    /// <para>فمصاريفُها لم تُحصر بعد، <b>وتسويةٌ قبل الحصر تُقفلها على فرقٍ يظهر لاحقاً</b>.</para>
    /// </summary>
    [Fact]
    public void SettlingBeforeTheTripEnds_IsRefused()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 2000m, fromDriverFloat: true);

        var error = Assert.Throws<AccountingException>(() => trip.SettleFloat(0m));

        Assert.Contains("بعد انتهاء الرحلة", error.Message);
    }

    /// <summary>
    /// <b>ولا تُسلَّم العهدةُ بعد الانطلاق.</b>
    ///
    /// <para>المبلغُ خرج من الصندوق فعلاً، <b>وتعديلُه لاحقاً يجعل القيدَ يخالف ما سُلِّم</b>.</para>
    /// </summary>
    [Fact]
    public void ChangingTheFloatAfterDispatch_IsRefused()
    {
        var trip = DispatchedTrip();

        var error = Assert.Throws<AccountingException>(() => trip.AdvanceFloat(3000m));

        Assert.Contains("قبل الانطلاق", error.Message);
    }

    // ── الشحنات والمسار ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>وشحنةٌ سُلِّمت لا تُحمَّل.</b>
    ///
    /// <para>وصلت مستلمَها، <b>وتحميلُها بعدُ يُنسب إلى الرحلة إيراداً لم تُنتجه</b> —
    /// فيظهر المسارُ رابحاً بشحناتِ غيره، <b>وهو عينُ الرقم الذي بُنيت الرحلةُ لتصحيحه</b>.</para>
    /// </summary>
    [Theory]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Returned)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void LoadingAFinishedShipment_IsRefused(ShipmentStatus status)
    {
        var trip = PlannedTrip();

        var error = Assert.Throws<AccountingException>(() => trip.LoadShipment(9L, status));

        Assert.Contains("ولا تُحمَّل شحنةٌ انتهت", error.Message);
    }

    /// <summary>
    /// <b>والشحنةُ الواحدة لا تُحمَّل مرّتين.</b>
    ///
    /// <para><b>وأجرتُها تُحسب مرّتين في ربحيّة المسار</b> — فيظهر الخاسرُ رابحاً.</para>
    /// </summary>
    [Fact]
    public void LoadingTheSameShipmentTwice_IsRefused()
    {
        var trip = PlannedTrip();
        trip.LoadShipment(9L, ShipmentStatus.InTransit);

        var error = Assert.Throws<AccountingException>(() =>
            trip.LoadShipment(9L, ShipmentStatus.InTransit));

        Assert.Contains("محمَّلةٌ سلفاً", error.Message);
        Assert.Single(trip.Shipments);
    }

    /// <summary>
    /// <b>ورحلةٌ بلا شحنةٍ لا تنطلق.</b>
    ///
    /// <para>مركبةٌ تخرج فارغةً <b>تُنفق وقوداً وبدلاً بلا إيرادٍ يقابله</b> — وتُفسد
    /// متوسّطَ تكلفة المسار الذي يُسعَّر به.</para>
    /// </summary>
    [Fact]
    public void DispatchingAnEmptyTrip_IsRefused()
    {
        var trip = PlannedTrip();

        var error = Assert.Throws<AccountingException>(trip.Dispatch);

        Assert.Contains("رحلةٌ بلا شحنات", error.Message);
    }

    /// <summary>
    /// <b>ورحلةٌ بلا مسارٍ تُرفض عند إنشائها.</b>
    ///
    /// <para><b>وبه تُقاس الربحيّة</b> — ورحلةٌ بلا مسارٍ لا تُقارَن بشيء، فتضيع في
    /// المتوسّط العامّ وهو ما بُني هذا البابُ ليكسره.</para>
    /// </summary>
    [Fact]
    public void ATripWithoutARoute_IsRefused()
    {
        var error = Assert.Throws<AccountingException>(() =>
            DeliveryTrip.Create(1L, "TR-1", "  ", "ABC-1234", new DateOnly(2026, 7, 1)));

        Assert.Contains("اسم المسار مطلوب", error.Message);
    }

    // ── دورةُ الحياة ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ولا يُعدَّل مصروفُ رحلةٍ سُوّيت.</b>
    ///
    /// <para>وإضافةُ مصروفٍ بعد التسوية <b>تكسر هـ-٢٠ بأثرٍ رجعيّ</b>: العهدةُ طُوبقت
    /// وقُيّدت، ثمّ صار المُنفَقُ أكبر.</para>
    /// </summary>
    [Fact]
    public void AddingAnExpenseAfterSettlement_IsRefused()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 2000m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 2));
        trip.SettleFloat(0m);

        var error = Assert.Throws<AccountingException>(() =>
            trip.AddExpense(TripExpenseKind.Fines, 300m, fromDriverFloat: false));

        Assert.Contains("سُوّيت", error.Message);
    }

    /// <summary>
    /// <b>ولا تنتهي إلا رحلةٌ منطلقة.</b>
    /// </summary>
    [Fact]
    public void CompletingATripThatNeverLeft_IsRefused()
    {
        var trip = PlannedTrip();
        trip.LoadShipment(9L, ShipmentStatus.Created);

        var error = Assert.Throws<AccountingException>(() =>
            trip.Complete(new DateOnly(2026, 7, 2)));

        Assert.Contains("رحلةٌ منطلقة", error.Message);
    }

    /// <summary>
    /// <b>ولا تُلغى رحلةٌ انتهت — وقد صُرف عليها.</b>
    /// </summary>
    [Fact]
    public void CancellingAFinishedTrip_IsRefused()
    {
        var trip = DispatchedTrip();
        trip.AddExpense(TripExpenseKind.Fuel, 2000m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 7, 2));

        var error = Assert.Throws<AccountingException>(trip.Cancel);

        Assert.Contains("رحلةٌ انتهت", error.Message);
    }

    /// <summary>
    /// <b>ولا تُحمَّل شحنةٌ على رحلةٍ انتهت.</b>
    /// </summary>
    [Fact]
    public void LoadingOntoAFinishedTrip_IsRefused()
    {
        var trip = DispatchedTrip();
        trip.Complete(new DateOnly(2026, 7, 2));

        var error = Assert.Throws<AccountingException>(() =>
            trip.LoadShipment(11L, ShipmentStatus.Created));

        Assert.Contains("رحلةٍ انتهت", error.Message);
    }

    /// <summary>
    /// <b>ومصروفٌ بصفرٍ أو سالبٍ يُرفض.</b>
    ///
    /// <para>وسالبٌ <b>يُنقص تكلفةَ المسار خُفيةً</b> فيظهر الخاسرُ رابحاً — وهو أخطرُ
    /// من الصفر.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-120)]
    public void ANonPositiveExpense_IsRefused(decimal amount)
    {
        var trip = DispatchedTrip();

        var error = Assert.Throws<AccountingException>(() =>
            trip.AddExpense(TripExpenseKind.Fuel, amount, fromDriverFloat: true));

        Assert.Contains("موجباً", error.Message);
    }

    private static DeliveryTrip PlannedTrip() =>
        DeliveryTrip.Create(1L, "TR-1", "الرياض — القصيم", "ABC-1234", new DateOnly(2026, 7, 1));

    private static DeliveryTrip DispatchedTrip()
    {
        var trip = PlannedTrip();
        trip.AdvanceFloat(2000m);
        trip.LoadShipment(9L, ShipmentStatus.Created);
        trip.Dispatch();
        return trip;
    }
}
