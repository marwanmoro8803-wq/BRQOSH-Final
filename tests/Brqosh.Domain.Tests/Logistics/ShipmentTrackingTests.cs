using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;

namespace Brqosh.Domain.Tests.Logistics;

/// <summary>
/// (CEEF-230 · ط3-٤ج) <b>أثرُ الشحنة — «أين شحنتي» وسندُ التسليم.</b>
///
/// <para><b>ما كان:</b> الشحنةُ تحمل حالةً واحدةً و<c>DeliveredOn</c>. <b>فالمستلمُ يسأل
/// أين شحنتُه فلا يُقال له إلا «في الطريق»</b> — بلا موضعٍ ولا وقتٍ ولا تاريخ.</para>
///
/// <para><b>والأخطرُ أثرُه في المال</b>: التسليمُ هو ما يُنشئ التزامَ الدفع عند الاستلام
/// (<c>هـ-١٣</c>) ويُستحقّ به أجرُ الشحن. <b>وتسليمٌ يُنكره المستلم لا سندَ له</b>:
/// التاجرُ يُطالب بماله والناقلُ يقول سُلِّمت، <b>ولا شيءَ يقول مَن استلم ومتى</b> —
/// فيُدفع المالُ مرّتين أو يُخسر العميل.</para>
///
/// <para><b>وهويّةُ هـ-٢١:</b> <c>الحالةُ = أثرُ آخرِ حدثٍ حاملٍ لحالة</c>.</para>
/// </summary>
public class ShipmentTrackingTests
{
    /// <summary>
    /// <b>القياسُ الحاكم: تسليمٌ بلا اسمِ مستلمٍ يُرفض.</b>
    ///
    /// <para><b>وهو سندُ التسليم كلُّه</b>: بغيره يُنكره المستلمُ فيُطالب التاجرُ بماله،
    /// <b>ولا شيءَ في النظام يقول مَن أخذها</b>.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DeliveringWithoutARecipient_IsRefused(string receivedBy)
    {
        var shipment = Dispatched();

        var error = Assert.Throws<AccountingException>(() =>
            shipment.Deliver(new DateOnly(2026, 3, 5), receivedBy));

        Assert.Contains("اسمُ المستلم مطلوبٌ للتسليم", error.Message);
        Assert.NotEqual(ShipmentStatus.Delivered, shipment.Status);
    }

    /// <summary>
    /// <b>وهويّةُ هـ-٢١: الانتقالُ يُسجّل أثرَه — فلا تفترق الحالةُ عن الأثر.</b>
    ///
    /// <para><b>ولو سُجِّل الأثرُ يدوياً لَأمكن أن يقول «سُلِّمت» والحالةُ «في الطريق»</b> —
    /// فتُقرأ الشحنةُ مسلَّمةً في التتبّع وقائمةً في الحساب.</para>
    /// </summary>
    [Fact]
    public void EveryTransition_LeavesItsOwnEvent()
    {
        var shipment = Dispatched();
        shipment.Deliver(new DateOnly(2026, 3, 5), "أبو خالد", "الرياض");

        Assert.Equal(ShipmentEventKind.PickedUp, shipment.Events[0].Kind);
        Assert.Equal(ShipmentEventKind.Delivered, shipment.Events[^1].Kind);
        Assert.Equal("أبو خالد", shipment.ReceivedBy);
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }

    /// <summary>
    /// <b>والأحداثُ الحاملةُ للحالة لا تُسجَّل يدوياً.</b>
    ///
    /// <para><b>وقبولُها يكسر هـ-٢١</b>: أثرٌ يقول «سُلِّمت» وحالةٌ تقول «في الطريق»،
    /// <b>ولا يُعرف أيُّهما الصادق</b>.</para>
    /// </summary>
    [Theory]
    [InlineData(ShipmentEventKind.Delivered)]
    [InlineData(ShipmentEventKind.Returned)]
    [InlineData(ShipmentEventKind.Cancelled)]
    public void StatusBearingEvents_CannotBeScannedByHand(ShipmentEventKind kind)
    {
        var shipment = Dispatched();

        var error = Assert.Throws<AccountingException>(() =>
            shipment.RecordScan(kind, new DateOnly(2026, 3, 4)));

        Assert.Contains("تُسجّلها انتقالاتُها", error.Message);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    /// <summary>
    /// <b>والأثرُ لا يُكتب إلى الوراء.</b>
    ///
    /// <para><b>ومسحٌ بتاريخٍ يسبق آخرَ ما سُجّل يُعيد كتابة التاريخ</b> — وهو ما يُبطل
    /// السندَ الذي بُني هذا الأثرُ ليكونه: يُدَّعى أنّ الشحنةَ كانت في جدّة يومَ كانت
    /// في الرياض.</para>
    /// </summary>
    [Fact]
    public void ABackdatedScan_IsRefusedByBothDates()
    {
        var shipment = Dispatched();
        shipment.RecordScan(ShipmentEventKind.AtHub, new DateOnly(2026, 3, 4), "فرز الرياض");

        var error = Assert.Throws<AccountingException>(() =>
            shipment.RecordScan(ShipmentEventKind.OutForDelivery, new DateOnly(2026, 3, 2)));

        Assert.Contains("2026-03-02", error.Message);
        Assert.Contains("2026-03-04", error.Message);
        Assert.Equal(2, shipment.Events.Count);
    }

    /// <summary>
    /// <b>ومحاولاتُ التسليم تُعدّ ولا تُغيّر الحالة.</b>
    ///
    /// <para><b>وثلاثُ محاولاتٍ فاشلةٍ هي ما يُبرّر الإرجاع</b> — وبغير سجلِّها يبدو
    /// الإرجاعُ تقصيراً من الناقل، <b>فيُطالَب بقيمة البضاعة</b>.</para>
    /// </summary>
    [Fact]
    public void FailedAttempts_AreCountedAndDoNotChangeStatus()
    {
        var shipment = Dispatched();
        shipment.RecordScan(ShipmentEventKind.DeliveryAttempted, new DateOnly(2026, 3, 4), note: "لا أحد");
        shipment.RecordScan(ShipmentEventKind.DeliveryAttempted, new DateOnly(2026, 3, 5), note: "مغلق");
        shipment.RecordScan(ShipmentEventKind.DeliveryAttempted, new DateOnly(2026, 3, 6), note: "مؤجّل");

        Assert.Equal(3, shipment.FailedAttempts);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    /// <summary>
    /// <b>وشحنةٌ ملغاةٌ لا تُمسَح.</b>
    ///
    /// <para>مسارُها انتهى، <b>ومسحٌ بعده يُوهم بحركةٍ لم تقع</b>.</para>
    /// </summary>
    [Fact]
    public void ScanningACancelledShipment_IsRefused()
    {
        var shipment = Shipment.Create(1L, "WB-1", 5L, consigneeName: "مستلم",
            destinationCity: "جدة", shipDate: new DateOnly(2026, 3, 1), freightCharge: 100m);
        shipment.Cancel();

        var error = Assert.Throws<AccountingException>(() =>
            shipment.RecordScan(ShipmentEventKind.AtHub, new DateOnly(2026, 3, 4)));

        Assert.Contains("مسارُها انتهى", error.Message);
    }

    /// <summary>
    /// <b>والإرجاعُ يترك أثرَه كذلك.</b>
    /// </summary>
    [Fact]
    public void AReturn_LeavesItsEvent()
    {
        var shipment = Dispatched();
        shipment.Return(new DateOnly(2026, 3, 8));

        Assert.Equal(ShipmentEventKind.Returned, shipment.Events[^1].Kind);
        Assert.Equal(ShipmentStatus.Returned, shipment.Status);
        Assert.Null(shipment.ReceivedBy);
    }

    /// <summary>
    /// <b>ولا تُمسَح شحنةٌ انتهى مسارُها.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت هذا</b>: كان الحارسُ يمنع الملغاةَ وحدَها، <b>فتُمسَح
    /// المسلَّمةُ بعد تسليمها</b> — فيُضاف إلى سندها حدثٌ لاحق، <b>ويصير آخرُ الأحداث
    /// مسحَ فرزٍ لا تسليماً</b>. ومَن يقرأ الأخيرَ سنداً يقرأ اسمَ موظّفِ فرزٍ مستلماً.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScanningAFinishedShipment_IsRefused(bool delivered)
    {
        var shipment = Dispatched();
        if (delivered) shipment.Deliver(new DateOnly(2026, 3, 5), "أبو خالد");
        else shipment.Return(new DateOnly(2026, 3, 5));

        var error = Assert.Throws<AccountingException>(() =>
            shipment.RecordScan(ShipmentEventKind.AtHub, new DateOnly(2026, 3, 6)));

        Assert.Contains("مسارُها انتهى", error.Message);
    }

    /// <summary>
    /// <b>والموضعُ يُحفظ مع الحدث — وبه يُجاب «أين شحنتي».</b>
    /// </summary>
    [Fact]
    public void ScansCarryTheirLocation()
    {
        var shipment = Dispatched();
        shipment.RecordScan(ShipmentEventKind.AtHub, new DateOnly(2026, 3, 4), "فرز القصيم");

        Assert.Equal("فرز القصيم", shipment.Events[^1].Location);
    }

    private static Shipment Dispatched()
    {
        var shipment = Shipment.Create(1L, "WB-1", 5L, consigneeName: "مستلم",
            destinationCity: "جدة", shipDate: new DateOnly(2026, 3, 1), freightCharge: 100m,
            codAmount: 500m);
        shipment.Dispatch("مستودع الرياض");
        return shipment;
    }
}
