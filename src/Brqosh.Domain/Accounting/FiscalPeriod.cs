namespace Brqosh.Domain.Accounting;

public enum FiscalPeriodStatus
{
    Open = 0,
    Closed = 1,
}

/// <summary>
/// فترة مالية شهرية. الترحيل مسموح في الفترات المفتوحة فقط؛
/// الإقفال يمنع أي ترحيل جديد داخل الفترة (حماية القوائم المعتمدة).
/// </summary>
public class FiscalPeriod
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    /// <summary>كود الفترة بصيغة YYYY-MM (مثل 2026-07).</summary>
    public string Code { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalPeriodStatus Status { get; private set; } = FiscalPeriodStatus.Open;
    public DateTime? ClosedAtUtc { get; private set; }

    // EF Core
    private FiscalPeriod()
    {
        Code = null!;
    }

    private FiscalPeriod(long companyId, int year, int month)
    {
        CompanyId = companyId;
        Code = $"{year:D4}-{month:D2}";
        StartDate = new DateOnly(year, month, 1);
        EndDate = StartDate.AddMonths(1).AddDays(-1);
    }

    public static FiscalPeriod Create(long companyId, int year, int month)
    {
        if (month is < 1 or > 12)
            throw new AccountingException("الشهر يجب أن يكون بين 1 و 12.");
        return new FiscalPeriod(companyId, year, month);
    }

    public static string CodeFor(DateOnly date) => $"{date.Year:D4}-{date.Month:D2}";

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public void Close()
    {
        if (Status == FiscalPeriodStatus.Closed)
            throw new AccountingException($"الفترة {Code} مقفلة مسبقاً.");
        Status = FiscalPeriodStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
    }

    /// <summary>إعادة فتح فترة مقفلة — صلاحية إدارية حساسة وتُسجَّل في سجل التدقيق.</summary>
    public void Reopen()
    {
        if (Status == FiscalPeriodStatus.Open)
            throw new AccountingException($"الفترة {Code} مفتوحة أصلاً.");
        Status = FiscalPeriodStatus.Open;
        ClosedAtUtc = null;
    }
}
