using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;

namespace Brqosh.Domain.Tests.RealEstate;

/// <summary>
/// (CEEF-230 · ط3-١ب) <b>أمرُ الصيانة — والتكلفةُ التي لا يعرف أحدٌ أيَّ وحدةٍ أكلتها.</b>
///
/// <para><b>ما كان:</b> المؤجِّرُ يُصلح ويدفع، <b>والمبلغُ يُقيَّد مصروفَ صيانةٍ عامّاً</b>
/// بلا رابطٍ بوحدة. فمحفظةٌ بأربعين وحدةً تُظهر ربحاً إجمالياً <b>ولا تقول أيَّ وحدةٍ
/// تخسر</b> — فيُجدَّد عقدُ الخاسرة بالسعر نفسِه سنةً بعد سنة.</para>
///
/// <para><b>والمصادرةُ كانت بلا سند</b>: <c>ReturnAsync</c> تأخذ مبلغَ المصادرة رقماً
/// مجرّداً. <b>ومصادرةٌ بلا بيانٍ دعوى يخسرها المؤجِّر.</b></para>
///
/// <para><b>وهويّةُ هـ-١٨:</b> <c>التكلفة = ما على المالك + ما على المستأجر</c>.</para>
/// </summary>
public class MaintenanceOrderTests
{
    // ── هـ-١٨ ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: حصّةُ المالك مُشتقّةٌ بالطرح فلا تفترق عن التكلفة أبداً.</b>
    ///
    /// <para>وتخزينُ الحصّتين معاً <b>قيمتان تصفان شيئاً واحداً — تفترقان يوماً</b>،
    /// وهو المزلقُ نفسُه الذي مُنع في <c>DiscountPercent</c>.</para>
    /// </summary>
    [Fact]
    public void OwnerCostAndTenantCharge_AlwaysExhaustTheTotal()
    {
        var order = DamageOrder();
        order.AddCost("قطع غيار", 700m);
        order.AddCost("أجرة فنّي", 300m);

        order.ChargeTenant(400m, leaseContractId: 7L);

        Assert.Equal(1000m, order.TotalCost);
        Assert.Equal(600m, order.OwnerCost);
        Assert.Equal(order.TotalCost, order.OwnerCost + order.TenantChargeAmount);
    }

    /// <summary>
    /// <b>وتحميلٌ يتجاوز التكلفةَ يُرفض.</b>
    ///
    /// <para><b>وتجاوزُه يجعل حصّةَ المالك سالبة</b> — فتظهر المنشأةُ <b>رابحةً من صيانةٍ
    /// دفعتها</b>، وهو رقمٌ لا معنى له في أيّ قائمة.</para>
    /// </summary>
    [Fact]
    public void ChargingMoreThanTheCost_IsRefusedByBothNumbers()
    {
        var order = DamageOrder();
        order.AddCost("إصلاح", 500m);

        var error = Assert.Throws<AccountingException>(() => order.ChargeTenant(600m, 7L));

        Assert.Contains("600.00", error.Message);
        Assert.Contains("500.00", error.Message);
    }

    /// <summary>
    /// <b>وحذفُ سطرٍ بعد التحميل يُصفّر التحميل.</b>
    ///
    /// <para>ولولاه لصار المحمَّلُ أكبرَ من الكلّ <b>بغير أن يمرّ بحارس</b> — فالحارسُ
    /// عند التحميل لا عند الحذف. <b>والتصفيرُ يُجبر إعادةَ القرار صراحةً</b> بدل تركِ
    /// رقمٍ صار باطلاً.</para>
    /// </summary>
    [Fact]
    public void RemovingACostLine_ClearsATenantChargeItWouldInvalidate()
    {
        var order = DamageOrder();
        order.AddCost("قطع غيار", 700m);
        order.AddCost("أجرة فنّي", 300m);
        order.ChargeTenant(900m, 7L);

        order.RemoveCost(0);

        Assert.Equal(300m, order.TotalCost);
        Assert.Equal(0m, order.TenantChargeAmount);
        Assert.Equal(300m, order.OwnerCost);
    }

    // ── مَن يدفع ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>والصيانةُ الدوريّةُ لا تُحمَّل على المستأجر.</b>
    ///
    /// <para>وهو أهمُّ حارسٍ هنا: <b>الاستهلاكُ العاديُّ ثمنُ التأجير لا ذنبُ الساكن</b>.
    /// وتحميلُه عليه يُخرج مطالبةً لا تصمد، <b>وإن خُصمت من التأمين صارت مصادرةً بلا
    /// وجه</b> — والمؤجِّرُ يخسر الدعوى ويردّ التأمينَ كلَّه.</para>
    /// </summary>
    [Fact]
    public void PreventiveMaintenance_MayNotBeChargedToTheTenant()
    {
        var order = MaintenanceOrder.Create(1L, propertyId: 5L, "MO-1",
            MaintenanceKind.Preventive, new DateOnly(2026, 5, 1), "دهان سنويّ", unitId: 9L);
        order.AddCost("دهان", 800m);

        var error = Assert.Throws<AccountingException>(() => order.ChargeTenant(800m, 7L));

        Assert.Contains("تلفٌ بفعله", error.Message);
    }

    /// <summary>
    /// <b>والإصلاحيّةُ كذلك على المالك.</b>
    ///
    /// <para>سخّانٌ عطل بالاستعمال العاديّ <b>ليس تلفاً بفعل المستأجر</b> — والتمييزُ
    /// بينهما هو ما يحسم النزاع، <b>وخلطُهما يجعل كلَّ عطلٍ تلفاً</b>.</para>
    /// </summary>
    [Fact]
    public void CorrectiveMaintenance_MayNotBeChargedToTheTenant()
    {
        var order = MaintenanceOrder.Create(1L, 5L, "MO-2",
            MaintenanceKind.Corrective, new DateOnly(2026, 5, 1), "تسريب ماء", unitId: 9L);
        order.AddCost("سباكة", 400m);

        var error = Assert.Throws<AccountingException>(() => order.ChargeTenant(400m, 7L));

        Assert.Contains("تلفٌ بفعله", error.Message);
    }

    /// <summary>
    /// <b>وعملٌ في المرافق المشتركة لا يُحمَّل على مستأجرٍ بعينه.</b>
    ///
    /// <para>مصعدُ العمارة <b>ينتفع به الجميع</b>، وتحميلُه على ساكن الشقّة الثالثة
    /// يُطالبه بما ينتفع به غيرُه.</para>
    /// </summary>
    [Fact]
    public void CommonAreaWork_HasNoTenantToChargeIt()
    {
        var order = MaintenanceOrder.Create(1L, 5L, "MO-3",
            MaintenanceKind.TenantDamage, new DateOnly(2026, 5, 1), "تلف باب المدخل");
        order.AddCost("باب", 1200m);

        var error = Assert.Throws<AccountingException>(() => order.ChargeTenant(1200m, 7L));

        Assert.Contains("المرافق المشتركة", error.Message);
    }

    /// <summary>
    /// <b>ولا تحميلَ بلا عقد.</b>
    ///
    /// <para>وحدةٌ شاغرةٌ تُصلَح على حساب المالك حتماً، <b>وتحميلُ تلفِها على «المستأجر»
    /// بلا عقدٍ يُنشئ ذمّةً على لا أحد</b> — تبقى في الأعمار بلا مدينٍ يُطالَب.</para>
    /// </summary>
    [Fact]
    public void ChargingWithoutALease_IsRefused()
    {
        var order = DamageOrder();
        order.AddCost("إصلاح", 500m);

        var error = Assert.Throws<AccountingException>(() => order.ChargeTenant(500m, 0L));

        Assert.Contains("العقد مطلوب", error.Message);
    }

    // ── دورةُ الحياة ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وأمرٌ بلا تكلفةٍ لا يُنهى.</b>
    ///
    /// <para>إنهاؤه بصفرٍ يُنتج سنداً <b>يبدو مُثبَتاً وهو فارغ</b> — ثمّ يُصادَر عليه
    /// تأمين، فيُطلب تفصيلُه فلا يوجد.</para>
    /// </summary>
    [Fact]
    public void CompletingAnEmptyOrder_IsRefused()
    {
        var order = DamageOrder();

        var error = Assert.Throws<AccountingException>(() =>
            order.Complete(new DateOnly(2026, 5, 10)));

        Assert.Contains("أمرُ صيانةٍ بلا تكلفة", error.Message);
    }

    /// <summary>
    /// <b>ولا يُعدَّل أمرٌ أُنهي — وتكلفتُه قُيّدت.</b>
    ///
    /// <para>وإضافةُ سطرٍ بعد الإنهاء تُغيّر تكلفةً <b>قُيّدت في اليومية</b>، فيفترق
    /// السندُ عن قيده.</para>
    /// </summary>
    [Fact]
    public void AddingCostAfterCompletion_IsRefused()
    {
        var order = DamageOrder();
        order.AddCost("إصلاح", 500m);
        order.Complete(new DateOnly(2026, 5, 10));

        var error = Assert.Throws<AccountingException>(() => order.AddCost("زيادة", 100m));

        Assert.Contains("أُنهي أو أُلغي", error.Message);
    }

    /// <summary>
    /// <b>ولا يُلغى أمرٌ أُنهي.</b>
    ///
    /// <para>وإلغاؤه يُسقط سنداً <b>قُيّدت تكلفتُه</b> — فيبقى القيدُ بلا مستند.</para>
    /// </summary>
    [Fact]
    public void CancellingACompletedOrder_IsRefused()
    {
        var order = DamageOrder();
        order.AddCost("إصلاح", 500m);
        order.Complete(new DateOnly(2026, 5, 10));

        var error = Assert.Throws<AccountingException>(order.Cancel);

        Assert.Contains("أُنهي", error.Message);
    }

    /// <summary>
    /// <b>وأمرٌ بلا وصفٍ يُرفض عند إنشائه.</b>
    ///
    /// <para><b>ولا يُراجَع ولا يُطعَن فيه</b>: «صيانة — ٣٢٠٠» ليست سنداً.</para>
    /// </summary>
    [Fact]
    public void AnOrderWithoutADescription_IsRefused()
    {
        var error = Assert.Throws<AccountingException>(() =>
            MaintenanceOrder.Create(1L, 5L, "MO-9", MaintenanceKind.Preventive,
                new DateOnly(2026, 5, 1), "   "));

        Assert.Contains("وصف العمل مطلوب", error.Message);
    }

    /// <summary>
    /// <b>وسطرٌ بصفرٍ أو سالبٍ يُرفض.</b>
    ///
    /// <para>وسطرٌ بصفرٍ يُطيل السندَ بلا أن يُضيف إليه، <b>وسالبٌ يُنقص التكلفةَ خُفيةً</b>
    /// فيصير المحمَّلُ على المستأجر أكبرَ من الحقيقة.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void ANonPositiveCostLine_IsRefused(decimal amount)
    {
        var order = DamageOrder();

        var error = Assert.Throws<AccountingException>(() => order.AddCost("سطر", amount));

        Assert.Contains("موجباً", error.Message);
    }

    /// <summary>
    /// <b>والتحميلُ يُلغى فيعود كلُّه على المالك.</b>
    /// </summary>
    [Fact]
    public void ClearingTheTenantCharge_ReturnsTheWholeCostToTheOwner()
    {
        var order = DamageOrder();
        order.AddCost("إصلاح", 500m);
        order.ChargeTenant(200m, 7L);

        order.ClearTenantCharge();

        Assert.Equal(0m, order.TenantChargeAmount);
        Assert.Null(order.LeaseContractId);
        Assert.Equal(500m, order.OwnerCost);
    }

    private static MaintenanceOrder DamageOrder() =>
        MaintenanceOrder.Create(1L, propertyId: 5L, "MO-D", MaintenanceKind.TenantDamage,
            new DateOnly(2026, 5, 1), "تلف بفعل المستأجر", unitId: 9L);
}
