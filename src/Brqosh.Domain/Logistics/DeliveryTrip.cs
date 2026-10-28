using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Logistics;

/// <summary>حالةُ الرحلة.</summary>
public enum TripStatus
{
    Planned = 0,
    Dispatched = 1,

    /// <summary>عادت المركبة — <b>وعندها تُحصر المصاريف</b>.</summary>
    Completed = 2,

    /// <summary>سُوّيت عهدةُ السائق — <b>ولا يبقى ريالٌ بلا حال</b>.</summary>
    Settled = 3,

    Cancelled = 4,
}

/// <summary>بابُ المصروف — <b>وهو ما يُقارَن بين الرحلات</b>.</summary>
public enum TripExpenseKind
{
    Fuel = 0,
    Tolls = 1,
    DriverAllowance = 2,
    Maintenance = 3,
    Fines = 4,
    Other = 5,
}

/// <summary>
/// <b>رحلةُ توصيل — والمسارُ الخاسرُ الذي لا يُرى.</b>
///
/// <para><b>ما كان:</b> الشحنةُ تحمل أجرتَها (<c>ط3-٤</c>)، <b>ولا شيءَ يحمل تكلفةَ
/// إيصالها</b>. فالوقودُ وبدلُ السائق والتعرفةُ والمخالفاتُ تُقيَّد <b>مصاريفَ تشغيلٍ
/// عامّةً</b> بلا رابطٍ برحلةٍ ولا مسار.</para>
///
/// <para><b>والأثرُ أنّ الربحيّةَ تُقاس بالجملة لا بالمسار.</b> شركةٌ تُشغّل اثني عشرَ
/// مساراً <b>لا تعرف أيَّها يخسر</b>: ثلاثةٌ منها قد تخسر في كلّ رحلة، <b>ويغطّي ربحُ
/// التسعةِ خسارتَها</b> فتظهر الشركةُ رابحة. فتُسعَّر المسارات بالتخمين — بالوزن والمسافة
/// — <b>وتُجدَّد عقودُ المسار الخاسر بالسعر نفسِه سنةً بعد سنة</b>، ويُوسَّع فيه لأنّه
/// «كثيرُ الشحنات».</para>
///
/// <para><b>والعطلُ الثاني عهدةُ السائق:</b> يأخذ ألفين نقداً للوقود، <b>ويُنفق ١٦٥٠
/// ويُعيد ٣٥٠</b>. وبلا رحلةٍ تُسوّى عليها: <b>إمّا يُصرف الألفان كلُّها مصروفاً</b> —
/// فتُضخَّم التكلفةُ بما عاد، ويختفي النقدُ العائد من الصندوق — <b>أو تبقى خروجاً نقدياً
/// لا يُطابَق أبداً</b>.</para>
///
/// <para><b>وهويّةُ هـ-٢٠:</b> <c>عهدةُ السائق = ما أُنفق منها + ما أُعيد</c> — <b>لا
/// أقلّ ولا أكثر</b>. وبقيّةٌ بينهما <b>مالٌ في جيبٍ لا يعرف أحدٌ جيبَ مَن</b>.</para>
/// </summary>
public class DeliveryTrip : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    /// <summary>رقمُ الرحلة — لا يتكرّر في المنشأة.</summary>
    public string TripNumber { get; private set; }

    /// <summary><b>واسمُ المسار هو ما تُقاس به الربحيّة</b> — لا رقمُ الرحلة.</summary>
    public string RouteName { get; private set; }

    public string VehiclePlate { get; private set; }
    public long? DriverEmployeeId { get; private set; }

    public DateOnly DepartureDate { get; private set; }
    public DateOnly? ReturnDate { get; private set; }

    public TripStatus Status { get; private set; } = TripStatus.Planned;

    /// <summary>العهدةُ النقديّةُ المسلَّمةُ للسائق.</summary>
    public decimal DriverFloat { get; private set; }

    /// <summary>ما أعاده السائقُ من العهدة.</summary>
    public decimal ReturnedCash { get; private set; }

    public long? JournalEntryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<TripShipment> _shipments = [];
    private readonly List<TripExpense> _expenses = [];

    public IReadOnlyList<TripShipment> Shipments => _shipments;
    public IReadOnlyList<TripExpense> Expenses => _expenses;

    /// <summary>كلُّ مصاريف الرحلة — <b>وهي مقامُ ربحيّة المسار</b>.</summary>
    public decimal TotalExpenses => Round(_expenses.Sum(e => e.Amount));

    /// <summary>ما أُنفق <b>من العهدة</b> وحدَها — والباقي على حساب الشركة مباشرةً.</summary>
    public decimal SpentFromFloat => Round(_expenses
        .Where(e => e.FromDriverFloat).Sum(e => e.Amount));

    /// <summary>
    /// فرقُ العهدة — <b>وصفرُه شرطُ التسوية (هـ-٢٠)</b>.
    /// </summary>
    public decimal FloatVariance => Round(DriverFloat - SpentFromFloat - ReturnedCash);

    private DeliveryTrip(long companyId, string tripNumber, string routeName, string vehiclePlate,
        DateOnly departureDate, long? driverEmployeeId)
    {
        CompanyId = companyId;
        TripNumber = tripNumber;
        RouteName = routeName;
        VehiclePlate = vehiclePlate;
        DepartureDate = departureDate;
        DriverEmployeeId = driverEmployeeId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private DeliveryTrip()
    {
        TripNumber = "";
        RouteName = "";
        VehiclePlate = "";
    }

    public static DeliveryTrip Create(long companyId, string tripNumber, string routeName,
        string vehiclePlate, DateOnly departureDate, long? driverEmployeeId = null)
    {
        if (string.IsNullOrWhiteSpace(tripNumber))
            throw new AccountingException("رقم الرحلة مطلوب.");
        if (string.IsNullOrWhiteSpace(routeName))
            throw new AccountingException(
                "اسم المسار مطلوب — وبه تُقاس الربحيّة، ورحلةٌ بلا مسارٍ لا تُقارَن بشيء.");
        if (string.IsNullOrWhiteSpace(vehiclePlate))
            throw new AccountingException("لوحة المركبة مطلوبة.");

        return new DeliveryTrip(companyId, tripNumber.Trim(), routeName.Trim(),
            vehiclePlate.Trim(), departureDate, driverEmployeeId);
    }

    /// <summary>
    /// يُحمِّل شحنةً على الرحلة.
    /// </summary>
    /// <remarks>
    /// <para><b>والحالةُ تُمرَّر ولا تُقرأ</b>: النطاقُ لا يصل إلى الشحنات، <b>والقرارُ
    /// يحتاج حالتَها</b>. وهو الأسلوبُ نفسُه المتَّبع في ضمّ الزيارات إلى المطالبة.</para>
    ///
    /// <para><b>وشحنةٌ سُلِّمت لا تُحمَّل</b>: وصلت مستلمَها، <b>وتحميلُها على شاحنةٍ بعدُ
    /// يُنسب إلى الرحلة إيراداً لم تُنتجه</b> — فيظهر المسارُ رابحاً بشحناتِ غيره.</para>
    ///
    /// <para><b>والشحنةُ الواحدة لا تُحمَّل مرّتين</b> على الرحلة نفسِها: تُحسب أجرتُها
    /// مرّتين في ربحيّة المسار.</para>
    /// </remarks>
    public void LoadShipment(long shipmentId, ShipmentStatus currentStatus)
    {
        EnsureLoadable();

        if (shipmentId <= 0) throw new AccountingException("الشحنة غير صالحة.");

        if (currentStatus is ShipmentStatus.Delivered or ShipmentStatus.Returned
            or ShipmentStatus.Cancelled)
            throw new AccountingException(
                $"الشحنة {shipmentId} حالتُها «{currentStatus}» — ولا تُحمَّل شحنةٌ انتهت، "
                + "فتُنسب إلى الرحلة إيراداً لم تُنتجه.");

        if (_shipments.Any(s => s.ShipmentId == shipmentId))
            throw new AccountingException(
                $"الشحنة {shipmentId} محمَّلةٌ سلفاً على هذه الرحلة — وأجرتُها تُحسب مرّتين.");

        _shipments.Add(new TripShipment(shipmentId));
    }

    public void UnloadShipment(long shipmentId)
    {
        EnsureLoadable();
        _shipments.RemoveAll(s => s.ShipmentId == shipmentId);
    }

    /// <summary>
    /// يُسلّم السائقَ عهدةً نقديّة.
    /// </summary>
    /// <remarks>
    /// <b>ولا تُغيَّر بعد الانطلاق</b>: المبلغُ خرج من الصندوق فعلاً، <b>وتعديلُه لاحقاً
    /// يجعل القيدَ يخالف ما سُلِّم</b>.
    /// </remarks>
    public void AdvanceFloat(decimal amount)
    {
        if (Status != TripStatus.Planned)
            throw new AccountingException("لا تُسلَّم العهدةُ إلا قبل الانطلاق.");
        if (amount < 0m)
            throw new AccountingException("العهدة لا تكون سالبة.");

        DriverFloat = Round(amount);
    }

    public void Dispatch()
    {
        if (Status != TripStatus.Planned)
            throw new AccountingException("لا تنطلق إلا رحلةٌ مخطَّطة.");

        // ‏**ورحلةٌ بلا شحنةٍ لا تنطلق**: مركبةٌ تخرج فارغةً تُنفق وقوداً وبدلاً
        // ‏**بلا إيرادٍ يقابله** — وتُفسد متوسّطَ تكلفة المسار الذي يُسعَّر به.
        if (_shipments.Count == 0)
            throw new AccountingException(
                "رحلةٌ بلا شحنات — ومركبةٌ تخرج فارغةً تُنفق بلا إيرادٍ يقابلها.");

        Status = TripStatus.Dispatched;
    }

    /// <summary>
    /// يُسجّل مصروفاً.
    /// </summary>
    /// <remarks>
    /// <b>ويُفصل ما خرج من العهدة عمّا دفعته الشركة</b>: كلاهما تكلفةُ الرحلة،
    /// <b>ولكنّ العهدة وحدَها تُسوَّى مع السائق</b>. وخلطُهما يُخرج فرقَ عهدةٍ كاذباً
    /// فيُطالَب السائقُ بما لم يأخذه.
    /// </remarks>
    public void AddExpense(TripExpenseKind kind, decimal amount, bool fromDriverFloat,
        string? note = null)
    {
        if (Status is TripStatus.Settled or TripStatus.Cancelled)
            throw new AccountingException("لا يُعدَّل مصروفُ رحلةٍ سُوّيت أو أُلغيت.");
        if (amount <= 0m)
            throw new AccountingException("مبلغ المصروف يجب أن يكون موجباً.");

        _expenses.Add(new TripExpense(kind, Round(amount), fromDriverFloat,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
    }

    public void Complete(DateOnly on)
    {
        if (Status != TripStatus.Dispatched)
            throw new AccountingException("لا تنتهي إلا رحلةٌ منطلقة.");

        Status = TripStatus.Completed;
        ReturnDate = on;
    }

    /// <summary>
    /// يُسوّي عهدةَ السائق.
    /// </summary>
    /// <remarks>
    /// <para><b>وهويّةُ هـ-٢٠</b>: العهدةُ = ما أُنفق منها + ما أُعيد. <b>وفرقٌ بينهما
    /// مالٌ في جيبٍ لا يعرف أحدٌ جيبَ مَن</b> — ويُبتلع صامتاً في «مصاريفَ أخرى» إن لم
    /// يُرفض هنا.</para>
    ///
    /// <para><b>والمُعادُ لا يتجاوز العهدة</b>: إعادةُ أكثرَ ممّا أُخذ تعني أنّ السائقَ
    /// أعاد مالاً ليس منها — <b>وهو تحصيلٌ لا إعادة</b>، ويُخفي دخلاً في حساب العهد.</para>
    /// </remarks>
    public void SettleFloat(decimal returnedCash)
    {
        if (Status != TripStatus.Completed)
            throw new AccountingException("لا تُسوَّى العهدةُ إلا بعد انتهاء الرحلة.");
        if (returnedCash < 0m)
            throw new AccountingException("المُعاد لا يكون سالباً.");
        if (Round(returnedCash) > DriverFloat)
            throw new AccountingException(
                $"المُعاد {returnedCash:N2} أكبر من العهدة {DriverFloat:N2} — "
                + "وإعادةُ ما لم يُؤخذ تحصيلٌ لا إعادة.");

        ReturnedCash = Round(returnedCash);

        if (FloatVariance != 0m)
            throw new AccountingException(
                $"عهدةُ السائق {DriverFloat:N2} لا تُطابق ما أُنفق منها {SpentFromFloat:N2} "
                + $"وما أُعيد {ReturnedCash:N2} — والفرقُ {FloatVariance:N2} مالٌ بلا حال.");

        Status = TripStatus.Settled;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void Cancel()
    {
        if (Status is TripStatus.Completed or TripStatus.Settled)
            throw new AccountingException("لا تُلغى رحلةٌ انتهت — وقد صُرف عليها.");

        Status = TripStatus.Cancelled;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

    private void EnsureLoadable()
    {
        if (Status != TripStatus.Planned && Status != TripStatus.Dispatched)
            throw new AccountingException("لا تُحمَّل شحنةٌ على رحلةٍ انتهت أو أُلغيت.");
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>شحنةٌ محمَّلةٌ على رحلة.</summary>
public class TripShipment
{
    public long Id { get; private set; }
    public long ShipmentId { get; private set; }

    internal TripShipment(long shipmentId) => ShipmentId = shipmentId;

    private TripShipment() { }
}

/// <summary>مصروفُ رحلة.</summary>
public class TripExpense
{
    public long Id { get; private set; }
    public TripExpenseKind Kind { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary><b>وهذا ما يُفرّق التسويةَ عن التكلفة</b>: كلاهما تكلفةٌ، والعهدةُ تُسوَّى.</summary>
    public bool FromDriverFloat { get; private set; }

    public string? Note { get; private set; }

    internal TripExpense(TripExpenseKind kind, decimal amount, bool fromDriverFloat, string? note)
    {
        Kind = kind;
        Amount = amount;
        FromDriverFloat = fromDriverFloat;
        Note = note;
    }

    private TripExpense() { }
}
