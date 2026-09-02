using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Logistics;

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

    public void Dispatch()
    {
        if (Status != ShipmentStatus.Created)
            throw new AccountingException("لا تُشحن إلا بوليصةٌ منشأة.");
        Status = ShipmentStatus.InTransit;
    }

    /// <summary>
    /// يُسلّم الشحنة، ويُثبت تحصيلَ مالها إن كان عليها تحصيل.
    ///
    /// <para><b>والتسليمُ لا يعني التحصيل دائماً</b>: شحنةٌ بلا «دفعٍ عند الاستلام» تُسلَّم
    /// ولا يُقبض عنها شيء. <b>وافتراضُ التحصيل مع كلّ تسليمٍ يُنشئ التزاماً للتاجر بمالٍ
    /// لم يدخل الصندوق</b> — ثمّ يُورَّد إليه من مال الشركة.</para>
    /// </summary>
    public void Deliver(DateOnly on)
    {
        if (Status != ShipmentStatus.InTransit && Status != ShipmentStatus.Created)
            throw new AccountingException("لا تُسلَّم إلا شحنةٌ قائمة.");
        if (on < ShipDate)
            throw new AccountingException("تاريخ التسليم قبل تاريخ الشحن.");

        Status = ShipmentStatus.Delivered;
        DeliveredOn = on;
        CodCollected = CodAmount > 0m;
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
    }

    public void Cancel()
    {
        if (Status == ShipmentStatus.Delivered || Status == ShipmentStatus.Returned)
            throw new AccountingException("شحنةٌ انتهى مسارُها لا تُلغى.");
        Status = ShipmentStatus.Cancelled;
    }

    /// <summary>
    /// يُثبت توريدَ مال التحصيل إلى التاجر.
    ///
    /// <para><b>ولا يُورَّد ما لم يُحصَّل</b> — وتوريدُه يُخرج الالتزامَ مديناً: أصلٌ موهومٌ
    /// في الميزانية لا يقابله حقّ. <b>ولا يُورَّد مرّتين</b> — والثانيةُ خروجُ مالٍ بلا سبب.</para>
    /// </summary>
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
