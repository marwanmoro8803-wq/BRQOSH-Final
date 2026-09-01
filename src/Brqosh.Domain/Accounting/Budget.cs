namespace Brqosh.Domain.Accounting;

/// <summary>حالةُ الموازنة.</summary>
public enum BudgetStatus
{
    /// <summary>تُعدَّل بحرّية — ولا تُقارَن بالفعليّ.</summary>
    Draft = 0,

    /// <summary>مُعتمَدة: تُقارَن بالفعليّ ولا تُعدَّل إلا بإعادة فتحها.</summary>
    Approved = 1,

    /// <summary>مُقفَلة: انتهت سنتُها.</summary>
    Closed = 2,
}

/// <summary>
/// (CEEF-230 · ط1-٦) <b>موازنةُ سنةٍ ماليّة.</b>
///
/// <para><b>ما كان:</b> صفرٌ تام — لا كيانَ ولا جدولَ ولا تقريرَ ولا حارس.
/// <b>والمطابقةُ الوحيدة لكلمة «موازنة» في المستودع كلِّه كانت نصَّ اقتراحٍ في مساعد
/// الذكاء الاصطناعيّ</b>: «قارِنِ التدفّقَ النقديَّ بالموازنة» — اقتراحٌ لا يقود إلى شيء.</para>
///
/// <para><b>والحبّةُ الشهرية مقصودة</b>: <see cref="FiscalPeriod"/> شهريُّ الحبّة
/// (<c>YYYY-MM</c>)، والموازنةُ تُقارَن به. <b>وحبّةٌ أخرى تعني نموذجَ زمنٍ ثانياً في
/// النظام</b> — وشيئان يصفان الزمنَ يفترقان يوماً.</para>
///
/// <para><b>ولا تمنع الموازنةُ ترحيلاً.</b> منشأةٌ صغيرةٌ تُوقفها موازنةٌ عن دفع فاتورة
/// كهرباءٍ تجاوزت تقديرَها تُعطّل عملَها لا تضبطه. <b>فالموازنةُ أداةُ قياسٍ لا بوّابة</b> —
/// والبوّابةُ إن لزمت موضعُها سيرُ اعتماد الطلب، وهو بابٌ آخر.</para>
/// </summary>
public class Budget
{
    private readonly List<BudgetLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public int Year { get; private set; }
    public BudgetStatus Status { get; private set; } = BudgetStatus.Draft;
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<BudgetLine> Lines => _lines.AsReadOnly();

    // EF Core
    private Budget()
    {
        Code = null!;
        NameAr = null!;
    }

    private Budget(long companyId, string code, string nameAr, int year)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        Year = year;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Budget Create(long companyId, string code, string nameAr, int year)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الموازنة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الموازنة مطلوب.");
        if (year is < 2000 or > 2200)
            throw new AccountingException("سنةُ الموازنة خارج المدى المعقول.");

        return new Budget(companyId, code.Trim(), nameAr.Trim(), year);
    }

    /// <summary>
    /// يُضيف مبلغاً مُخطَّطاً لحسابٍ في شهر.
    ///
    /// <para><b>ويُجمَع على السطر القائم لا يُكرَّر</b>: سطران للحساب نفسِه في الشهر نفسِه
    /// يجعلان «المُخطَّط» رقمين، <b>ويُقرأ التقريرُ بأيّهما صادف</b>.</para>
    /// </summary>
    public void SetLine(string accountCode, int month, decimal amount, long? costCenterId = null)
    {
        if (Status != BudgetStatus.Draft)
            throw new AccountingException("الموازنة معتمدةٌ أو مقفلة — أعِدْ فتحَها لتعديلها.");
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new AccountingException("كود الحساب مطلوب في سطر الموازنة.");
        if (month is < 1 or > 12)
            throw new AccountingException($"الشهر يجب أن يكون بين 1 و12 — ووردَ {month}.");
        if (amount < 0)
            throw new AccountingException("مبلغُ الموازنة لا يكون سالباً.");

        var code = accountCode.Trim();
        var period = FiscalPeriod.CodeFor(new DateOnly(Year, month, 1));

        var existing = _lines.FirstOrDefault(l =>
            l.AccountCode == code && l.PeriodCode == period && l.CostCenterId == costCenterId);

        if (existing is not null)
        {
            existing.SetAmount(amount);
            return;
        }

        _lines.Add(BudgetLine.Create(code, period, amount, costCenterId));
    }

    /// <summary>المُخطَّطُ الكليّ — وهو ما يُعرض ترويسةً.</summary>
    public decimal TotalPlanned => _lines.Sum(l => l.Amount);

    public void Approve()
    {
        if (Status == BudgetStatus.Closed)
            throw new AccountingException("الموازنة مقفلة.");
        if (_lines.Count == 0)
            throw new AccountingException("لا تُعتمد موازنةٌ بلا سطور.");
        Status = BudgetStatus.Approved;
    }

    public void Reopen()
    {
        if (Status == BudgetStatus.Closed)
            throw new AccountingException("الموازنة مقفلة — لا تُعاد فتحُها.");
        Status = BudgetStatus.Draft;
    }

    public void Close() => Status = BudgetStatus.Closed;
}

/// <summary>
/// (CEEF-230 · ط1-٦) سطرُ موازنة — حسابٌ في شهرٍ، ومركزُ تكلفةٍ اختياريّ.
///
/// <para><b>و<c>PeriodCode</c> بصيغة <see cref="FiscalPeriod"/> نفسِها</b> — فالمقارنةُ
/// تقع على المفتاح ذاته، ولا يُترجَم زمنٌ إلى زمن.</para>
/// </summary>
public class BudgetLine
{
    // ‏**ولا `Id` هنا**: النوعُ مملوكٌ ومفتاحُه ظلٌّ يُنشئه EF. وإعلانُ `Id` معه
    // ‏يُنتج عمودين بالاسم نفسِه — ويسقط إنشاءُ الجدول على SQLite بـ«duplicate column».
    // ‏والسطرُ يُميَّز بمعناه لا بمعرّفٍ: (الحساب، الفترة، مركز التكلفة).
    public string AccountCode { get; private set; }
    public string PeriodCode { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>مركزُ التكلفة — و<c>null</c> يعني الحسابَ كلَّه بلا تصنيف.</summary>
    public long? CostCenterId { get; private set; }

    private BudgetLine()
    {
        AccountCode = null!;
        PeriodCode = null!;
    }

    internal static BudgetLine Create(string accountCode, string periodCode, decimal amount,
        long? costCenterId) =>
        new()
        {
            AccountCode = accountCode,
            PeriodCode = periodCode,
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            CostCenterId = costCenterId,
        };

    internal void SetAmount(decimal amount) =>
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
