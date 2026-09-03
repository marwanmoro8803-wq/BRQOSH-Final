using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Logistics;

/// <summary>
/// نوعُ حدث التتبّع — <b>وهو ما يُجيب «أين شحنتي» ويُثبت التسليم</b>.
/// </summary>
public enum ShipmentEventKind
{
    /// <summary>استُلمت من المرسِل.</summary>
    PickedUp = 0,

    /// <summary>وصلت فرزاً أو مستودعَ عبور.</summary>
    AtHub = 1,

    /// <summary>خرجت للتسليم.</summary>
    OutForDelivery = 2,

    /// <summary>
    /// مُحاولةُ تسليمٍ لم تنجح — <b>ولا تُغيّر الحالة</b>.
    /// </summary>
    /// <remarks>
    /// <b>وتُسجَّل مرّاتٍ</b>: ثلاثُ محاولاتٍ فاشلةٍ هي ما يُبرّر الإرجاع، <b>وبغير
    /// سجلِّها يبدو الإرجاعُ تقصيراً من الناقل</b> — فيُطالَب بقيمة البضاعة.
    /// </remarks>
    DeliveryAttempted = 3,

    /// <summary>سُلِّمت — <b>وهذا هو الحدثُ الذي يُثبت الاستحقاق</b>.</summary>
    Delivered = 4,

    /// <summary>أُعيدت إلى المرسِل.</summary>
    Returned = 5,

    /// <summary>أُلغيت.</summary>
    Cancelled = 6,
}

/// <summary>حالةُ الشحنة.</summary>
public enum ShipmentStatus
{
    Created = 0,
    InTransit = 1,
    Delivered = 2,
    Returned = 3,
    Cancelled = 4,
}

/// <summary>
/// (CEEF-230 · ط3-٤) <b>شحنةٌ ببوليصة — والدفعُ عند الاستلام أخطرُ ما فيها.</b>
///
/// <para><b>ما كان:</b> ملفُّ «اللوجستيات» في شاشة التأسيس، <b>ولا شحنةَ في النظام ولا
/// بوليصة</b>. فالشركةُ تُدير بوالصَها في جدولٍ خارجيّ، <b>وتُقيّد ما تحصّله كلَّه
/// إيراداً</b>.</para>
///
/// <para><b>وذلك عطلٌ يُضخّم الإيرادَ بمقدار عشرين ضعفاً:</b> شركةُ توصيلٍ تُحصّل مليونَ
/// ريالٍ «دفعاً عند الاستلام» شهرياً <b>ليست هي مالَها</b> — هو مالُ التجّار، تردُّه
/// إليهم بعد خصم أجرة التوصيل. <b>وأجرتُها خمسون ألفاً</b>. فقيدُ المليون إيراداً يُظهر
/// شركةً بحجمٍ ليس لها، <b>وتُدفع عنه زكاةٌ وضريبةٌ على مالٍ يُردّ</b>، وتُوزَّع أرباحٌ
/// من أمانةٍ في اليد.</para>
///
/// <para><b>وهويّةُ هـ-١٣:</b> رصيدُ حساب «تحصيلٌ نيابةً عن التجّار» = المحصَّلُ − المُورَّد،
/// <b>ولا يكون سالباً أبداً</b> — وسالبُه يعني توريدَ ما لم يُحصَّل.</para>
/// </summary>
public class Shipment : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    /// <summary>رقمُ البوليصة — لا يتكرّر، وهو ما يتتبّع به المرسِلُ والمرسَلُ إليه.</summary>
    public string WaybillNumber { get; private set; }

    /// <summary>المرسِلُ — تاجرٌ في دفتر العملاء، عليه أجرةُ الشحن وله مالُ التحصيل.</summary>
    public long ShipperCustomerId { get; private set; }

    public string ConsigneeName { get; private set; }
    public string ConsigneePhone { get; private set; }
    public string DestinationCity { get; private set; }

    public int Pieces { get; private set; }
    public decimal WeightKg { get; private set; }

    /// <summary>أجرةُ الشحن — <b>وهي وحدها إيرادُ الشركة</b>.</summary>
    public decimal FreightCharge { get; private set; }

    /// <summary>
    /// المبلغُ المطلوب تحصيلُه من المستلم لصالح التاجر — <b>وهو أمانةٌ لا إيراد</b>.
    /// </summary>
    public decimal CodAmount { get; private set; }

    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Created;

    public DateOnly ShipDate { get; private set; }
    public DateOnly? DeliveredOn { get; private set; }

    /// <summary>هل حُصّل مالُ التحصيل فعلاً؟ — <b>والتسليمُ لا يعني التحصيل</b>.</summary>
    public bool CodCollected { get; private set; }

    /// <summary>هل وُرِّد إلى التاجر؟</summary>
    public bool CodRemitted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<ShipmentEvent> _events = [];

    /// <summary>
    /// أثرُ الشحنة — <b>وبه تُجاب «أين شحنتي» ويُثبت التسليم</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>وما كان:</b> الشحنةُ تحمل حالةً واحدةً و<c>DeliveredOn</c>. <b>فالمستلمُ
    /// يسأل أين شحنتُه فلا يُقال له إلا «في الطريق»</b> — بلا موضعٍ ولا وقتٍ ولا تاريخ.</para>
    ///
    /// <para><b>والأخطرُ أثرُه في المال</b>: التسليمُ هو ما يُنشئ التزامَ الدفع عند
    /// الاستلام (<c>هـ-١٣</c>) ويُستحقّ به أجرُ الشحن. <b>وتسليمٌ يُنكره المستلم لا سندَ
    /// له</b>: التاجرُ يُطالب بماله، والناقلُ يقول سُلِّمت، <b>ولا شيءَ في النظام يقول
    /// مَن استلم ومتى وأين</b>. فيُدفع المالُ مرّتين أو يُخسر العميل.</para>
    ///
    /// <para><b>وهويّةُ هـ-٢١:</b> <c>الحالةُ = أثرُ آخرِ حدثٍ حاملٍ لحالة</c> — <b>ولا
    /// تفترقان</b>، لأنّ الانتقالاتِ نفسَها هي التي تُسجّل أحداثَها.</para>
    /// </remarks>
    public IReadOnlyList<ShipmentEvent> Events => _events;

    /// <summary>عددُ محاولات التسليم الفاشلة — <b>وهي ما يُبرّر الإرجاع</b>.</summary>
    public int FailedAttempts => _events.Count(e => e.Kind == ShipmentEventKind.DeliveryAttempted);

    /// <summary>
    /// مَن استلم الشحنة — و<c>null</c> قبل التسليم.
    /// </summary>
    /// <remarks>
    /// <b>والترشيحُ بنوع الحدث لا تقتله طفرة</b> — <b>وذلك مُصرَّحٌ به لا مطويّ</b>:
    /// حارسُ <c>RecordScan</c> يمنع المسحَ بعد انتهاء المسار، <b>فحدثُ التسليم هو آخرُ
    /// الأحداث دائماً</b> وقراءةُ الأخير تُساويه.
    ///
    /// <para><b>وأُبقي لأنّه يقول ما يعنيه الحقل</b>: «مَن أخذ الشحنة» لا «آخرُ ما كُتب
    /// فيها». <b>ولو رُفع حارسُ الانتهاء يوماً لصار الترشيحُ هو ما يمنع أن يُقرأ اسمُ
    /// موظّفِ فرزٍ سنداً للتسليم.</b></para>
    /// </remarks>
    public string? ReceivedBy => _events
        .LastOrDefault(e => e.Kind == ShipmentEventKind.Delivered)?.ReceivedBy;

    // EF Core
    private Shipment()
    {
        WaybillNumber = null!;
        ConsigneeName = null!;
        ConsigneePhone = null!;
        DestinationCity = null!;
    }

    private Shipment(long companyId, string waybill, long shipperCustomerId, string consigneeName,
        string consigneePhone, string destinationCity, DateOnly shipDate, int pieces,
        decimal weightKg, decimal freightCharge, decimal codAmount)
    {
        CompanyId = companyId;
        WaybillNumber = waybill;
        ShipperCustomerId = shipperCustomerId;
        ConsigneeName = consigneeName;
        ConsigneePhone = consigneePhone;
        DestinationCity = destinationCity;
        ShipDate = shipDate;
        Pieces = pieces;
        WeightKg = weightKg;
        FreightCharge = freightCharge;
        CodAmount = codAmount;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Shipment Create(long companyId, string waybillNumber, long shipperCustomerId,
        string consigneeName, string destinationCity, DateOnly shipDate, decimal freightCharge,
        decimal codAmount = 0m, int pieces = 1, decimal weightKg = 0m, string consigneePhone = "")
    {
        if (string.IsNullOrWhiteSpace(waybillNumber))
            throw new AccountingException("رقم البوليصة مطلوب.");
        if (shipperCustomerId <= 0)
            throw new AccountingException("المرسِل مطلوب للشحنة.");
        if (string.IsNullOrWhiteSpace(consigneeName))
            throw new AccountingException("اسم المرسَل إليه مطلوب.");
        if (string.IsNullOrWhiteSpace(destinationCity))
            throw new AccountingException("مدينة الوجهة مطلوبة.");
        if (freightCharge < 0m)
            throw new AccountingException("أجرة الشحن لا تكون سالبة.");
        if (codAmount < 0m)
            throw new AccountingException("مبلغ التحصيل لا يكون سالباً.");
        if (pieces <= 0)
            throw new AccountingException("عدد الطرود يجب أن يكون موجباً.");
        if (weightKg < 0m)
            throw new AccountingException("الوزن لا يكون سالباً.");

        return new Shipment(companyId, waybillNumber.Trim(), shipperCustomerId,
            consigneeName.Trim(), (consigneePhone ?? "").Trim(), destinationCity.Trim(), shipDate,
            pieces, Math.Round(weightKg, 3, MidpointRounding.AwayFromZero),
            Math.Round(freightCharge, 2, MidpointRounding.AwayFromZero),
            Math.Round(codAmount, 2, MidpointRounding.AwayFromZero));
    }

    public void Dispatch(string? location = null)
    {
        if (Status != ShipmentStatus.Created)
            throw new AccountingException("لا تُشحن إلا بوليصةٌ منشأة.");

        Status = ShipmentStatus.InTransit;
        Append(ShipmentEventKind.PickedUp, ShipDate, location, null, null);
    }

    /// <summary>
    /// يُسلّم الشحنة، ويُثبت تحصيلَ مالها إن كان عليها تحصيل.
    ///
    /// <para><b>والتسليمُ لا يعني التحصيل دائماً</b>: شحنةٌ بلا «دفعٍ عند الاستلام» تُسلَّم
    /// ولا يُقبض عنها شيء. <b>وافتراضُ التحصيل مع كلّ تسليمٍ يُنشئ التزاماً للتاجر بمالٍ
    /// لم يدخل الصندوق</b> — ثمّ يُورَّد إليه من مال الشركة.</para>
    /// </summary>
    /// <param name="receivedBy">
    /// <b>مَن استلم — وهو سندُ التسليم.</b>
    ///
    /// <para><b>وتسليمٌ بلا اسمِ مستلمٍ لا يُثبَت</b>: يُنكره المستلمُ فيُطالب التاجرُ
    /// بماله، <b>ولا شيءَ في النظام يقول مَن أخذها</b> — فيُدفع المالُ مرّتين.</para>
    /// </param>
    public void Deliver(DateOnly on, string receivedBy, string? location = null)
    {
        if (Status != ShipmentStatus.InTransit && Status != ShipmentStatus.Created)
            throw new AccountingException("لا تُسلَّم إلا شحنةٌ قائمة.");
        if (on < ShipDate)
            throw new AccountingException("تاريخ التسليم قبل تاريخ الشحن.");
        if (string.IsNullOrWhiteSpace(receivedBy))
            throw new AccountingException(
                "اسمُ المستلم مطلوبٌ للتسليم — وتسليمٌ بلا سندٍ يُنكره المستلمُ فيُدفع المالُ مرّتين.");

        Status = ShipmentStatus.Delivered;
        DeliveredOn = on;
        CodCollected = CodAmount > 0m;
        Append(ShipmentEventKind.Delivered, on, location, null, receivedBy.Trim());
    }

    /// <summary>
    /// تُرَدُّ الشحنة — <b>ولا تحصيلَ لها</b>.
    ///
    /// <para><b>وأجرةُ الشحن تبقى على التاجر</b>: الشركةُ نقلت ورجعت، والخدمةُ قُدِّمت.
    /// <b>وإسقاطُها يُعدم إيراداً استُحقّ.</b></para>
    /// </summary>
    public void Return(DateOnly on)
    {
        if (Status == ShipmentStatus.Delivered)
            throw new AccountingException("شحنةٌ سُلّمت لا تُردّ — أنشئ بوليصةَ إرجاعٍ جديدة.");
        if (Status == ShipmentStatus.Cancelled)
            throw new AccountingException("شحنةٌ ملغاةٌ لا تُردّ.");

        // ‏**ولا يُصفَّر `CodCollected` هنا** — وكان مكتوباً، **فنجَت طفرةُ نزعِه**.
        // ‏والسببُ أنّ الإرجاع لا يُقبل إلا من «منشأة» أو «في الطريق»، **وكلتاهما لم
        // ‏يُحصَّل فيها شيء**. فالسطرُ لم يكن حارساً بل زينةً تُوهم بحراسة — **وسطرٌ
        // ‏لا تستطيع طفرةٌ أن تقتله لا يحرس شيئاً**، ووجودُه يُخفي أنّ الحمايةَ الحقيقية
        // ‏في حارس الحالة أعلاه.
        Status = ShipmentStatus.Returned;
        DeliveredOn = on;
        Append(ShipmentEventKind.Returned, on, null, null, null);
    }

    public void Cancel()
    {
        if (Status == ShipmentStatus.Delivered || Status == ShipmentStatus.Returned)
            throw new AccountingException("شحنةٌ انتهى مسارُها لا تُلغى.");

        Status = ShipmentStatus.Cancelled;
        Append(ShipmentEventKind.Cancelled, ShipDate, null, null, null);
    }

    /// <summary>
    /// يُثبت توريدَ مال التحصيل إلى التاجر.
    ///
    /// <para><b>ولا يُورَّد ما لم يُحصَّل</b> — وتوريدُه يُخرج الالتزامَ مديناً: أصلٌ موهومٌ
    /// في الميزانية لا يقابله حقّ. <b>ولا يُورَّد مرّتين</b> — والثانيةُ خروجُ مالٍ بلا سبب.</para>
    /// </summary>
    /// <summary>
    /// يُسجّل مسحاً تشغيلياً — <b>وصولاً لفرزٍ أو خروجاً للتسليم أو محاولةً فاشلة</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>والأحداثُ الحاملةُ للحالة لا تُسجَّل من هنا</b>: التسليمُ والإرجاعُ
    /// والإلغاءُ تُسجّلها انتقالاتُها. <b>ولو قُبلت يدوياً لَافترق الأثرُ عن الحالة</b> —
    /// فتُقرأ الشحنةُ «مُسلَّمةً» في التتبّع و«في الطريق» في الحساب، وتكسر <c>هـ-٢١</c>.</para>
    ///
    /// <para><b>والأثرُ لا يُكتب إلى الوراء</b>: مسحٌ بتاريخٍ يسبق آخرَ ما سُجّل
    /// <b>يُعيد كتابة التاريخ</b> — وهو ما يُبطل السندَ الذي بُني هذا الأثرُ ليكونه.</para>
    /// </remarks>
    public void RecordScan(ShipmentEventKind kind, DateOnly on, string? location = null,
        string? note = null)
    {
        if (Status is ShipmentStatus.Cancelled or ShipmentStatus.Delivered
            or ShipmentStatus.Returned)
            throw new AccountingException(
                $"شحنةٌ حالتُها «{Status}» لا تُمسَح — ومسارُها انتهى، ومسحٌ بعده يُوهم بحركةٍ لم تقع.");

        if (kind is ShipmentEventKind.Delivered or ShipmentEventKind.Returned
            or ShipmentEventKind.Cancelled)
            throw new AccountingException(
                "التسليمُ والإرجاعُ والإلغاءُ تُسجّلها انتقالاتُها لا المسحُ اليدويّ — "
                + "وإلا افترق الأثرُ عن الحالة.");

        Append(kind, on, location, note, null);
    }

    private void Append(ShipmentEventKind kind, DateOnly on, string? location, string? note,
        string? receivedBy)
    {
        var last = _events.Count > 0 ? _events[^1].OccurredOn : (DateOnly?)null;
        if (last is DateOnly previous && on < previous)
            throw new AccountingException(
                $"الحدث في {on:yyyy-MM-dd} يسبق آخرَ ما سُجّل في {previous:yyyy-MM-dd} — "
                + "والأثرُ لا يُكتب إلى الوراء.");

        _events.Add(new ShipmentEvent(kind, on,
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            receivedBy));
    }

    public void MarkCodRemitted()
    {
        if (!CodCollected)
            throw new AccountingException(
                $"البوليصة «{WaybillNumber}» لم يُحصَّل مالُها — ولا يُورَّد ما لم يُحصَّل.");
        if (CodRemitted)
            throw new AccountingException($"البوليصة «{WaybillNumber}» ورِّد مالُها سلفاً.");

        CodRemitted = true;
    }

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}

/// <summary>
/// حدثُ تتبّعٍ في مسار الشحنة — <b>وهو سندُ التسليم لا زينةُ عرض</b>.
/// </summary>
public class ShipmentEvent
{
    public long Id { get; private set; }
    public ShipmentEventKind Kind { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Location { get; private set; }
    public string? Note { get; private set; }

    /// <summary>مَن استلم — <b>ولا يكون إلا في حدث التسليم</b>.</summary>
    public string? ReceivedBy { get; private set; }

    internal ShipmentEvent(ShipmentEventKind kind, DateOnly occurredOn, string? location,
        string? note, string? receivedBy)
    {
        Kind = kind;
        OccurredOn = occurredOn;
        Location = location;
        Note = note;
        ReceivedBy = receivedBy;
    }

    private ShipmentEvent() { }
}
