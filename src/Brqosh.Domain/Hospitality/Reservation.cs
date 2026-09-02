using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Hospitality;

/// <summary>صنفُ الغرفة.</summary>
public enum RoomKind
{
    Single = 0,
    Double = 1,
    Twin = 2,
    Suite = 3,
    Hall = 4,
}

/// <summary>حالةُ الغرفة.</summary>
public enum RoomStatus
{
    Vacant = 0,
    Occupied = 1,
    Maintenance = 2,
}

/// <summary>
/// (CEEF-230 · ط3-٣ب) <b>غرفةٌ — وحالتُها قيدٌ لا وصف.</b>
///
/// <para>وهي نظيرةُ <c>PropertyUnit</c> في <c>ط3-١</c>، <b>وحالتُها لا تُضبط إلا من حجز</b>:
/// فالنزولُ هو ما يشغلها، والمغادرةُ هي ما يُخليها.</para>
/// </summary>
public class Room
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    public string RoomNumber { get; private set; }
    public RoomKind Kind { get; private set; }
    public int Floor { get; private set; }
    public decimal NightlyRate { get; private set; }
    public RoomStatus Status { get; private set; } = RoomStatus.Vacant;

    // EF Core
    private Room() => RoomNumber = null!;

    private Room(long companyId, string roomNumber, RoomKind kind, int floor, decimal nightlyRate)
    {
        CompanyId = companyId;
        RoomNumber = roomNumber;
        Kind = kind;
        Floor = floor;
        NightlyRate = nightlyRate;
    }

    public static Room Create(long companyId, string roomNumber, RoomKind kind,
        decimal nightlyRate, int floor = 0)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
            throw new AccountingException("رقم الغرفة مطلوب.");
        if (nightlyRate < 0m)
            throw new AccountingException("سعر الليلة لا يكون سالباً.");

        return new Room(companyId, roomNumber.Trim(), kind, floor,
            Math.Round(nightlyRate, 2, MidpointRounding.AwayFromZero));
    }

    internal void Occupy() => Status = RoomStatus.Occupied;
    internal void Release() => Status = RoomStatus.Vacant;

    /// <summary><b>ولا تدخل الصيانةَ غرفةٌ مشغولة</b> — النزيلُ فيها، وإخفاؤها يُعطّل خدمتَه.</summary>
    public void EnterMaintenance()
    {
        if (Status == RoomStatus.Occupied)
            throw new AccountingException("الغرفة مشغولة — لا تدخل الصيانة والنزيل فيها.");
        Status = RoomStatus.Maintenance;
    }

    public void LeaveMaintenance()
    {
        if (Status == RoomStatus.Maintenance) Status = RoomStatus.Vacant;
    }

    public void SetRate(decimal nightlyRate)
    {
        if (nightlyRate < 0m) throw new AccountingException("سعر الليلة لا يكون سالباً.");
        NightlyRate = Math.Round(nightlyRate, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>حالةُ الحجز.</summary>
public enum ReservationStatus
{
    Booked = 0,
    CheckedIn = 1,
    CheckedOut = 2,
    Cancelled = 3,
    NoShow = 4,
}

/// <summary>نوعُ رسمٍ على حساب النزيل.</summary>
public enum FolioChargeKind
{
    RoomNight = 0,
    Restaurant = 1,
    Laundry = 2,
    Minibar = 3,
    Other = 4,
}

/// <summary>
/// (CEEF-230 · ط3-٣ب) <b>حجزٌ — وحسابُ نزيلٍ يتراكم ليلةً بليلة.</b>
///
/// <para><b>والليالي تُحسب بيوم المغادرة مستثنًى</b>: نزيلٌ يصل الأوّلَ ويغادر الثالث
/// <b>بات ليلتين لا ثلاثاً</b>. وحسابُ الفرق شاملاً يُضيف ليلةً كاملةً إلى كلّ فاتورةٍ
/// في الفندق — <b>وهو أشيعُ خطأٍ في هذا القطاع</b>، ويراه النزيلُ عند الحساب فيُنازع.</para>
///
/// <para><b>ويوم المغادرة يُخالف عقدَ الإيجار قصداً</b>: في <c>ط3-١</c> يومُ الانتهاء
/// <b>داخلٌ</b> — المستأجرُ يسكن فيه؛ وهنا <b>خارج</b> — الغرفةُ تُخلى صباحاً وتُؤجَّر
/// ليلتَها لغيره. <b>فالحدُّ نفسُه بمعنيين، وتوحيدُهما يُنتج عطلاً في أحد القطاعين.</b></para>
/// </summary>
public class Reservation : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    public long RoomId { get; private set; }

    /// <summary>النزيلُ — عميلٌ في دفتر الذمم، فحسابُه يُفوتَر ويُحصَّل.</summary>
    public long GuestCustomerId { get; private set; }

    public string ReservationNumber { get; private set; }
    public DateOnly CheckInDate { get; private set; }
    public DateOnly CheckOutDate { get; private set; }
    public decimal RatePerNight { get; private set; }
    public int Guests { get; private set; } = 1;

    public ReservationStatus Status { get; private set; } = ReservationStatus.Booked;

    /// <summary>فاتورةُ التصفية عند المغادرة.</summary>
    public long? SettlementInvoiceId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary><b>عددُ الليالي — ويومُ المغادرة مستثنًى.</b></summary>
    public int Nights => CheckOutDate.DayNumber - CheckInDate.DayNumber;

    // EF Core
    private Reservation() => ReservationNumber = null!;

    private Reservation(long companyId, long roomId, long guestCustomerId, string number,
        DateOnly checkIn, DateOnly checkOut, decimal ratePerNight, int guests)
    {
        CompanyId = companyId;
        RoomId = roomId;
        GuestCustomerId = guestCustomerId;
        ReservationNumber = number;
        CheckInDate = checkIn;
        CheckOutDate = checkOut;
        RatePerNight = ratePerNight;
        Guests = guests;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Reservation Create(long companyId, long roomId, long guestCustomerId,
        string reservationNumber, DateOnly checkIn, DateOnly checkOut, decimal ratePerNight,
        int guests = 1)
    {
        if (roomId <= 0) throw new AccountingException("الغرفة مطلوبة للحجز.");
        if (guestCustomerId <= 0) throw new AccountingException("النزيل مطلوب للحجز.");
        if (string.IsNullOrWhiteSpace(reservationNumber))
            throw new AccountingException("رقم الحجز مطلوب.");
        if (checkOut <= checkIn)
            throw new AccountingException("تاريخ المغادرة يجب أن يكون بعد الوصول — ولا حجزَ بلا ليلة.");
        if (ratePerNight < 0m)
            throw new AccountingException("سعر الليلة لا يكون سالباً.");
        if (guests <= 0)
            throw new AccountingException("عدد النزلاء يجب أن يكون موجباً.");

        return new Reservation(companyId, roomId, guestCustomerId, reservationNumber.Trim(),
            checkIn, checkOut, Math.Round(ratePerNight, 2, MidpointRounding.AwayFromZero), guests);
    }

    /// <summary>
    /// مدّتان متداخلتان — <b>ويومُ المغادرة مستثنًى من الطرفين</b>.
    ///
    /// <para>فحجزٌ يغادر الثالثَ وآخرُ يصل الثالثَ <b>لا يتداخلان</b>: الغرفةُ تُخلى صباحاً
    /// وتُنظَّف وتُسلَّم مساءً. <b>ومنعُه يُعطّل غرفةً ليلةً كاملةً بلا سبب</b> — وهو في
    /// فندقٍ بمئة غرفةٍ خسارةُ إشغالٍ يوميّة.</para>
    /// </summary>
    public bool OverlapsWith(DateOnly otherCheckIn, DateOnly otherCheckOut) =>
        CheckInDate < otherCheckOut && otherCheckIn < CheckOutDate;

    public void CheckIn(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (Status != ReservationStatus.Booked)
            throw new AccountingException("لا يُنزَل إلا حجزٌ قائم.");
        if (room.Id != RoomId)
            throw new AccountingException("الغرفة المُمرَّرة ليست غرفةَ هذا الحجز.");
        if (room.Status == RoomStatus.Maintenance)
            throw new AccountingException("الغرفة في الصيانة — أخرِجْها قبل الإنزال.");

        Status = ReservationStatus.CheckedIn;
        room.Occupy();
    }

    public void CheckOut(Room room, long settlementInvoiceId)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (Status != ReservationStatus.CheckedIn)
            throw new AccountingException("لا يُغادر إلا نزيلٌ نازل.");
        if (room.Id != RoomId)
            throw new AccountingException("الغرفة المُمرَّرة ليست غرفةَ هذا الحجز.");

        SettlementInvoiceId = settlementInvoiceId;
        Status = ReservationStatus.CheckedOut;
        room.Release();
    }

    public void Cancel()
    {
        if (Status == ReservationStatus.CheckedIn || Status == ReservationStatus.CheckedOut)
            throw new AccountingException("حجزٌ نزل صاحبُه لا يُلغى.");
        Status = ReservationStatus.Cancelled;
    }

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}

/// <summary>
/// (CEEF-230 · ط3-٣ب) <b>حسابُ النزيل — يتراكم ثمّ يُصفّى بفاتورةٍ واحدة.</b>
///
/// <para><b>وهويّةُ هـ-١٢:</b> مجموعُ رسوم الحساب = إجماليُّ فاتورة التصفية. <b>ورسمٌ يسقط
/// بين الحساب والفاتورة إيرادٌ قُدِّم ولم يُطالَب به</b> — والنزيلُ غادر.</para>
/// </summary>
public class GuestFolio
{
    private readonly List<FolioCharge> _charges = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long ReservationId { get; private set; }
    public bool IsSettled { get; private set; }

    public IReadOnlyList<FolioCharge> Charges => _charges;

    public decimal Total => Math.Round(_charges.Sum(c => c.Amount), 2, MidpointRounding.AwayFromZero);

    // EF Core
    private GuestFolio() { }

    public static GuestFolio OpenFor(long companyId, long reservationId) =>
        reservationId <= 0
            ? throw new AccountingException("الحجز مطلوب لفتح حساب النزيل.")
            : new GuestFolio { CompanyId = companyId, ReservationId = reservationId };

    /// <summary>هل قُيّدت ليلةُ هذا التاريخ سلفاً؟ — والحارسُ الأخير قيدُ تفرّدٍ في القاعدة.</summary>
    public bool HasRoomNight(DateOnly date) =>
        _charges.Any(c => c.Kind == FolioChargeKind.RoomNight && c.RoomNightDate == date);

    public void AddRoomNight(DateOnly night, decimal amount, VatCategory vatCategory = VatCategory.Standard)
    {
        EnsureOpen();
        if (HasRoomNight(night))
            throw new AccountingException($"ليلة {night:yyyy-MM-dd} مقيَّدةٌ سلفاً — ولا تُحسب مرّتين.");

        _charges.Add(FolioCharge.Create(night, FolioChargeKind.RoomNight,
            $"إيجار غرفة — ليلة {night:yyyy-MM-dd}", amount, vatCategory, night));
    }

    public void AddCharge(DateOnly date, FolioChargeKind kind, string description, decimal amount,
        VatCategory vatCategory = VatCategory.Standard)
    {
        EnsureOpen();
        if (kind == FolioChargeKind.RoomNight)
            throw new AccountingException("ليلةُ الغرفة تُقيَّد بمسارها لا كرسمٍ عامّ.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان الرسم مطلوب.");
        if (amount <= 0m)
            throw new AccountingException("مبلغ الرسم يجب أن يكون موجباً.");

        _charges.Add(FolioCharge.Create(date, kind, description.Trim(), amount, vatCategory, null));
    }

    public void MarkSettled()
    {
        EnsureOpen();
        if (_charges.Count == 0)
            throw new AccountingException("لا يُصفّى حسابٌ بلا رسوم.");
        IsSettled = true;
    }

    private void EnsureOpen()
    {
        if (IsSettled)
            throw new AccountingException("الحساب مُصفّى — ولا يُضاف إليه بعد المغادرة.");
    }
}

/// <summary>رسمٌ على حساب النزيل.</summary>
public class FolioCharge
{
    public DateOnly ChargeDate { get; private set; }
    public FolioChargeKind Kind { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public VatCategory VatCategory { get; private set; }

    /// <summary>
    /// ليلةُ الغرفة — و<c>null</c> لغيرها.
    ///
    /// <para><b>وهو مفتاحُ منع الازدواج</b>: قيدُ تفرّدٍ على (الحساب، هذا العمود) يمنع
    /// قيدَ ليلةٍ مرّتين، <b>والفراغُ لا يتصادم</b> فتتعدّد الرسومُ الأخرى بحرّية.</para>
    /// </summary>
    public DateOnly? RoomNightDate { get; private set; }

    // EF Core
    private FolioCharge() => Description = null!;

    internal static FolioCharge Create(DateOnly date, FolioChargeKind kind, string description,
        decimal amount, VatCategory vatCategory, DateOnly? roomNightDate) =>
        new()
        {
            ChargeDate = date,
            Kind = kind,
            Description = description,
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            VatCategory = vatCategory,
            RoomNightDate = roomNightDate,
        };
}
