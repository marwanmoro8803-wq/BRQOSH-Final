using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Sales;

/// <summary>دوريّةُ التكرار — والخطوةُ بالأشهر إلا الأسبوعيّ.</summary>
public enum RecurrenceFrequency
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    SemiAnnual = 3,
    Annual = 4,
}

/// <summary>حالةُ الخطة.</summary>
public enum RecurringPlanStatus
{
    Active = 0,
    Paused = 1,
    Ended = 2,
}

/// <summary>
/// (CEEF-230 · ط1-٥) <b>خطةُ فوترةٍ دورية — إيجارٌ أو اشتراكٌ أو عقدُ صيانة.</b>
///
/// <para><b>ما كان:</b> صفرُ شيفرةٍ للفوترة المتكرّرة. و«Recurring» في تصميم الشاشات
/// وعدٌ لم يُنفَّذ. <b>ولا مُجدوِلَ عملٍ في التطبيق إطلاقاً</b> — لا
/// <c>IHostedService</c> ولا Quartz ولا Hangfire — وكلُّ مستندٍ يُرحَّل بفعلٍ بشريّ صريح.</para>
///
/// <para><b>ولذلك تُبنى دفعةً يُطلقها المستخدم لا خدمةً خلفية</b> — وهو <b>أصدقُ لتطبيقٍ
/// مكتبيّ</b> يُشغَّل حين يُفتح: خدمةٌ خلفيةٌ في تطبيقٍ مغلقٍ لا تعمل، وخطةٌ تعتمد عليها
/// تتوقّف بصمتٍ شهراً كاملاً. والمثالُ القائم <c>DepreciationService.RunMonthlyAsync</c>.</para>
///
/// <para><b>وتُولَّد كلُّ المستحقّات الفائتة لا آخرُها</b>: مَن أغلق البرنامج ثلاثة أشهر
/// له ثلاثُ فواتير — <b>وفاتورةٌ واحدةٌ تُسقط إيرادَ شهرين بلا أثر</b>.</para>
/// </summary>
public class RecurringInvoicePlan : IBranchScoped
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long CustomerId { get; private set; }

    public string Code { get; private set; }
    public string Description { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }
    public RecurringPlanStatus Status { get; private set; } = RecurringPlanStatus.Active;

    public DateOnly StartDate { get; private set; }

    /// <summary>آخرُ تاريخٍ تُولَّد فيه — و<c>null</c> يعني بلا نهاية.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>عددُ ما وُلِّد — وهو ما يُشتقّ منه تاريخُ التكرار التالي.</summary>
    public int OccurrencesGenerated { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    // EF Core
    private RecurringInvoicePlan()
    {
        Code = null!;
        Description = null!;
    }

    private RecurringInvoicePlan(long companyId, long customerId, string code, string description,
        RecurrenceFrequency frequency, DateOnly startDate, DateOnly? endDate)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        Code = code;
        Description = description;
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static RecurringInvoicePlan Create(long companyId, long customerId, string code,
        string description, RecurrenceFrequency frequency, DateOnly startDate, DateOnly? endDate = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الخطة مطلوب.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الخطة مطلوب.");
        if (endDate is DateOnly end && end < startDate)
            throw new AccountingException("تاريخ نهاية الخطة قبل بدايتها.");

        return new RecurringInvoicePlan(companyId, customerId, code.Trim(), description.Trim(),
            frequency, startDate, endDate);
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null,
        decimal discountPercent = 0m)
    {
        if (Status == RecurringPlanStatus.Ended)
            throw new AccountingException("الخطة منتهية — لا تُعدَّل.");
        if (quantity <= 0)
            throw new AccountingException("الكمية يجب أن تكون أكبر من صفر.");

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory,
            taxExemptionReasonCode, taxExemptionReason, null,
            Math.Round(discountPercent, 4, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// <b>تاريخُ التكرار رقم <paramref name="index"/> — مُشتقٌّ من البداية لا من سابقه.</b>
    ///
    /// <para><b>وهذا هو الفرق الذي يُفسد الجداولَ حين يُهمَل:</b> خطةٌ تبدأ في ٣١ يناير
    /// وتُحسب بالإضافة إلى <b>سابقها</b> تُعطي ٣١ يناير ثمّ ٢٨ فبراير ثمّ <b>٢٨ مارس</b> —
    /// فتنزلق إلى الثامن والعشرين إلى الأبد. والاشتقاقُ من البداية يُعطي ٣١ مارس،
    /// <b>ويعود إلى يوم الاستحقاق كلّما اتّسع الشهر</b>.</para>
    /// </summary>
    public DateOnly OccurrenceAt(int index)
    {
        if (index < 0) throw new AccountingException("رقم التكرار لا يكون سالباً.");

        if (Frequency == RecurrenceFrequency.Weekly)
            return StartDate.AddDays(7 * index);

        var months = Frequency switch
        {
            RecurrenceFrequency.Monthly => 1,
            RecurrenceFrequency.Quarterly => 3,
            RecurrenceFrequency.SemiAnnual => 6,
            _ => 12,
        };

        var shifted = new DateTime(StartDate.Year, StartDate.Month, 1).AddMonths(months * index);
        var day = Math.Min(StartDate.Day, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        return new DateOnly(shifted.Year, shifted.Month, day);
    }

    /// <summary>تاريخُ التكرار القادم — و<c>null</c> إن انتهت الخطة أو تجاوزت نهايتَها.</summary>
    public DateOnly? NextOccurrence()
    {
        if (Status != RecurringPlanStatus.Active) return null;

        var next = OccurrenceAt(OccurrencesGenerated);
        return EndDate is DateOnly end && next > end ? null : next;
    }

    /// <summary>
    /// يُثبّت أنّ تكراراً وُلِّد.
    ///
    /// <para><b>ولا يُستدعى إلا بعد نجاح الترحيل</b>: تقدُّمُ العدّاد قبله يُسقط تكراراً
    /// بلا فاتورة — <b>وإيرادٌ يضيع بلا أثرٍ أسوأُ من فاتورةٍ تُعاد</b>.</para>
    /// </summary>
    public void MarkGenerated() => OccurrencesGenerated++;

    public void Pause()
    {
        if (Status == RecurringPlanStatus.Ended)
            throw new AccountingException("الخطة منتهية.");
        Status = RecurringPlanStatus.Paused;
    }

    public void Resume()
    {
        if (Status == RecurringPlanStatus.Ended)
            throw new AccountingException("الخطة منتهية.");
        Status = RecurringPlanStatus.Active;
    }

    public void End() => Status = RecurringPlanStatus.Ended;

    public void AssignBranch(long branchId)
    {
        if (BranchId is null) BranchId = branchId;
    }
}

/// <summary>
/// (CEEF-230 · ط1-٥) <b>أثرُ تكرارٍ وُلِّد — وهو حارسُ الازدواج.</b>
///
/// <para>وقيدُ التفرّد على <c>(PlanId, OccurrenceDate)</c> هو الضمانةُ الحقيقية:
/// <b>تشغيلان متزامنان أو مكرَّران يصطدمان بالقاعدة</b> لا بفحصٍ في الذاكرة قد يسبقه
/// الآخر. <b>وفوترةُ العميل مرّتين عن شهرٍ واحد عطلٌ يراه العميلُ قبلنا.</b></para>
/// </summary>
public class RecurringInvoiceRun
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long PlanId { get; private set; }
    public DateOnly OccurrenceDate { get; private set; }
    public long SalesInvoiceId { get; private set; }
    public DateTime GeneratedAtUtc { get; private set; }

    private RecurringInvoiceRun() { }

    public static RecurringInvoiceRun Create(long companyId, long planId, DateOnly occurrenceDate,
        long salesInvoiceId) =>
        new()
        {
            CompanyId = companyId,
            PlanId = planId,
            OccurrenceDate = occurrenceDate,
            SalesInvoiceId = salesInvoiceId,
            GeneratedAtUtc = DateTime.UtcNow,
        };
}
