using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Hospitality;

/// <summary>من أين جاءت الإكرامية — <b>ولكلٍّ مسارٌ نقديٌّ مختلف</b>.</summary>
public enum TipSource
{
    /// <summary>نقداً على الطاولة — <b>وتدخل الدرج فتظهر فائضاً</b> إن لم تُفصل.</summary>
    Cash = 0,

    /// <summary>مضافةٌ على البطاقة — <b>وتصل البنكَ مع التحصيل</b> فتبدو إيراداً.</summary>
    Card = 1,
}

/// <summary>حالةُ وعاء الإكراميات.</summary>
public enum TipPoolStatus
{
    Open = 0,

    /// <summary>وُزِّع بالكامل وأُقفل — <b>ولا يبقى فيه ريال</b>.</summary>
    Closed = 1,
}

/// <summary>
/// (CEEF-230 · ط3-٣د) <b>وعاءُ الإكراميات — مالُ النُّدُل يُقيَّد إيراداً للمطعم.</b>
///
/// <para><b>ما كان:</b> لا شيءَ في النظام يعرف الإكرامية. <b>ومصيرُها أحدُ عطلين</b>:</para>
///
/// <para><b>النقديّةُ تدخل الدرج</b> فتُحسب في <c>ClosingCash</c>، ويظهر الفرقُ
/// <b>فائضَ صندوقٍ يبدو خطأً في العدّ</b> — فيُطارَد شهرياً ولا يُفهم. أو تُؤخذ من الدرج
/// بلا قيد، <b>فيظهر عجزٌ يُتَّهم به أمينُ الصندوق</b>.</para>
///
/// <para><b>وإكراميةُ البطاقة تصل البنكَ مع التحصيل</b>، فلا يجد لها النظامُ إلا الإيرادَ
/// موضعاً. <b>وذاك يُقيَّد مالَ النُّدُل إيراداً للمطعم</b>: تُدفع عنه <b>ضريبةُ قيمةٍ
/// مضافةٍ وزكاةٌ على مالٍ ليس للمنشأة</b>، ويُعلَن إيرادٌ أكبرُ من الحقيقة — <b>ثمّ يُوزَّع
/// على النُّدُل فيُقيَّد مصروفاً</b>، فيتضخّم طرفا القائمة معاً.</para>
///
/// <para><b>والتمييزُ الذي تُخطئه أكثرُ الأنظمة:</b> <b>الإكراميةُ تطوّعٌ ومالُ العامل</b>
/// — التزامٌ لا إيراد، ولا ضريبةَ عليها. <b>ورسمُ الخدمة نسبةٌ تفرضها المنشأة</b> — إيرادٌ
/// خاضعٌ للضريبة. <b>وخلطُهما يُخطئ في الاتّجاهين</b>: عدُّ الإكرامية إيراداً يدفع ضريبةً
/// لا تجب، <b>وعدُّ رسم الخدمة إكراميةً يكتم ضريبةً تجب</b> — وهي مخالفة. <b>ولذلك لا
/// يدخل رسمُ الخدمة هذا الوعاءَ أصلاً</b>: هو سطرٌ في الفاتورة.</para>
///
/// <para><b>وهويّةُ هـ-١٩:</b> <c>رصيدُ «إكرامياتٌ مستحقّة» = المحصَّلُ − الموزَّع</c>،
/// <b>ولا يكون سالباً أبداً</b> — وسالبُه يعني توزيعَ ما لم يُحصَّل. وهي هويّةُ <c>هـ-١٣</c>
/// نفسُها في قطاعٍ آخر: <b>مالٌ في اليد ليس مالَ صاحب اليد</b>.</para>
/// </summary>
public class TipPool : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    /// <summary>الوردية — و<c>null</c> لوعاءٍ يوميٍّ لا يخصّ ورديةً بعينها.</summary>
    public long? ShiftId { get; private set; }

    public DateOnly PoolDate { get; private set; }
    public TipPoolStatus Status { get; private set; } = TipPoolStatus.Open;
    public long? JournalEntryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<TipCollection> _collections = [];
    private readonly List<TipDistribution> _distributions = [];

    public IReadOnlyList<TipCollection> Collections => _collections;
    public IReadOnlyList<TipDistribution> Distributions => _distributions;

    /// <summary><b>والنقديُّ يُفصل عن البطاقة</b> — لأنّ تسويةَ كلٍّ منهما من حسابٍ آخر.</summary>
    public decimal CollectedCash => Round(_collections
        .Where(c => c.Source == TipSource.Cash).Sum(c => c.Amount));

    public decimal CollectedCard => Round(_collections
        .Where(c => c.Source == TipSource.Card).Sum(c => c.Amount));

    public decimal TotalCollected => Round(_collections.Sum(c => c.Amount));
    public decimal TotalDistributed => Round(_distributions.Sum(d => d.Amount));

    /// <summary>ما بقي في الوعاء — <b>وهو ما يمنع الإقفال</b>.</summary>
    public decimal Undistributed => Round(TotalCollected - TotalDistributed);

    private TipPool(long companyId, DateOnly poolDate, long? shiftId)
    {
        CompanyId = companyId;
        PoolDate = poolDate;
        ShiftId = shiftId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private TipPool() { }

    public static TipPool Open(long companyId, DateOnly poolDate, long? shiftId = null)
    {
        if (shiftId is long id && id <= 0)
            throw new AccountingException("الوردية غير صالحة.");

        return new TipPool(companyId, poolDate, shiftId);
    }

    /// <summary>
    /// يُسجّل إكراميةً محصَّلة.
    /// </summary>
    /// <remarks>
    /// <b>ولا يُسجَّل هنا رسمُ الخدمة</b>: هو إيرادٌ خاضعٌ للضريبة وسطرٌ في الفاتورة،
    /// <b>وإدخالُه الوعاءَ يكتم ضريبةً تجب</b>.
    /// </remarks>
    public void Collect(TipSource source, decimal amount, string? note = null)
    {
        EnsureOpen();

        if (amount <= 0m)
            throw new AccountingException("مبلغ الإكرامية يجب أن يكون موجباً.");

        _collections.Add(new TipCollection(source, Round(amount),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
    }

    /// <summary>
    /// يوزّع مبلغاً على موظّف.
    /// </summary>
    /// <remarks>
    /// <para><b>والتوزيعُ للموظّف الواحد يُجمع لا يُستبدل</b>: نادلٌ يأخذ حصّةً من الغداء
    /// وأخرى من العشاء. <b>وإسنادٌ بدل جمعٍ يُسقط إحداهما صامتاً</b> — فيبقى في الوعاء
    /// مالٌ لا يعرف أحدٌ لمن هو. وهو العطلُ نفسُه الذي وقع في وصفات المطبخ.</para>
    ///
    /// <para><b>وتوزيعٌ يتجاوز المحصَّل يُرفض (هـ-١٩)</b>: يدفع للنُّدُل مالاً <b>لم يتركه
    /// أحد</b>، فيصير رصيدُ الالتزام سالباً — وتُصبح المنشأةُ دائنةً لنفسها بفرقٍ لا سبب له.</para>
    /// </remarks>
    public void Distribute(long employeeId, decimal amount)
    {
        EnsureOpen();

        if (employeeId <= 0) throw new AccountingException("الموظّف مطلوب للتوزيع.");
        if (amount <= 0m)
            throw new AccountingException("المبلغ الموزَّع يجب أن يكون موجباً.");

        if (Round(TotalDistributed + amount) > TotalCollected)
            throw new AccountingException(
                $"الموزَّع {Round(TotalDistributed + amount):N2} أكبر من المحصَّل {TotalCollected:N2} — "
                + "ولا يُوزَّع ما لم يُترك.");

        var existing = _distributions.SingleOrDefault(d => d.EmployeeId == employeeId);
        if (existing is not null) existing.Add(Round(amount));
        else _distributions.Add(new TipDistribution(employeeId, Round(amount)));
    }

    /// <summary>
    /// يقسم ما بقي بالتساوي — <b>وآخرُ حصّةٍ تأخذ الباقي</b>.
    /// </summary>
    /// <remarks>
    /// <b>وقسمةُ ١٠٠ على ٣ تُنتج ٣٣٫٣٣ ثلاثَ مرّات ومجموعُها ٩٩٫٩٩</b> — فيبقى فلسٌ في
    /// الوعاء يمنع إقفالَه إلى الأبد. <b>وآخرُ حصّةٍ تأخذ الباقي</b> فيُقفل بالضبط، وهو
    /// مذهبُ التوزيع نفسُه المتَّبع في أوعية الضريبة وحصص التأمين.
    /// </remarks>
    public void DistributeEvenly(IReadOnlyList<long> employeeIds)
    {
        EnsureOpen();

        if (employeeIds is null || employeeIds.Count == 0)
            throw new AccountingException("لا موظّفين للتوزيع عليهم.");
        if (employeeIds.Distinct().Count() != employeeIds.Count)
            throw new AccountingException("موظّفٌ مكرَّرٌ في قائمة التوزيع.");

        var pot = Undistributed;
        if (pot <= 0m)
            throw new AccountingException("لا شيءَ في الوعاء يُوزَّع.");

        var share = Math.Round(pot / employeeIds.Count, 2, MidpointRounding.AwayFromZero);
        var running = 0m;

        for (var i = 0; i < employeeIds.Count; i++)
        {
            var amount = i == employeeIds.Count - 1 ? Round(pot - running) : share;
            if (amount <= 0m) continue;

            running += amount;
            Distribute(employeeIds[i], amount);
        }
    }

    /// <summary>
    /// يُقفل الوعاء.
    /// </summary>
    /// <remarks>
    /// <b>وبقيّةٌ غيرُ موزَّعةٍ تمنع الإقفال</b>: التزامٌ بلا صاحب. <b>وإقفالُه عليها
    /// يترك مالاً في حسابٍ لا أحدَ يُطالب به</b>، ثمّ يُشطب بعد سنواتٍ إيراداً — <b>وهو
    /// أخذُ مال النُّدُل بمرور الوقت</b>.
    /// </remarks>
    public void Close()
    {
        EnsureOpen();

        if (_collections.Count == 0)
            throw new AccountingException("وعاءٌ بلا تحصيل — ولا يُقفل ما لم يُفتح على مال.");

        if (Undistributed != 0m)
            throw new AccountingException(
                $"بقي في الوعاء {Undistributed:N2} غيرُ موزَّع — والتزامٌ بلا صاحبٍ يُشطب "
                + "بعد سنواتٍ إيراداً، وهو أخذُ مالِ النُّدُل بمرور الوقت.");

        Status = TipPoolStatus.Closed;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

    private void EnsureOpen()
    {
        if (Status != TipPoolStatus.Open)
            throw new AccountingException("الوعاء مُقفل — ولا يُعدَّل بعد إقفاله.");
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>إكراميةٌ محصَّلة.</summary>
public class TipCollection
{
    public long Id { get; private set; }
    public TipSource Source { get; private set; }
    public decimal Amount { get; private set; }
    public string? Note { get; private set; }

    internal TipCollection(TipSource source, decimal amount, string? note)
    {
        Source = source;
        Amount = amount;
        Note = note;
    }

    private TipCollection() { }
}

/// <summary>حصّةُ موظّفٍ من الوعاء.</summary>
public class TipDistribution
{
    public long Id { get; private set; }
    public long EmployeeId { get; private set; }
    public decimal Amount { get; private set; }

    internal TipDistribution(long employeeId, decimal amount)
    {
        EmployeeId = employeeId;
        Amount = amount;
    }

    private TipDistribution() { }

    internal void Add(decimal amount) => Amount = Math.Round(Amount + amount, 2,
        MidpointRounding.AwayFromZero);
}
