using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Construction;

public enum ProgressBillingStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>مستخلص أعمال — يُرحَّل محاسبياً مع ضريبة القيمة المضافة 15%.</summary>
public class ProgressBilling : IBranchScoped
{
    public const decimal SaudiVatRate = VatCalculator.SaudiVatRate;

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long ProjectId { get; private set; }
    public long CustomerId { get; private set; }
    public string? BillingNumber { get; private set; }
    public DateOnly BillingDate { get; private set; }
    public string PeriodDescription { get; private set; }
    public decimal CurrentWorkAmount { get; private set; }
    public decimal RetentionPercent { get; private set; }
    public ProgressBillingStatus Status { get; private set; } = ProgressBillingStatus.Draft;
    public long? JournalEntryId { get; private set; }

    public decimal RetentionAmount =>
        Math.Round(CurrentWorkAmount * RetentionPercent / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>الوعاء الضريبي — قيمة الأعمال قبل الضريبة.</summary>
    public decimal TaxableAmount => CurrentWorkAmount;

    public decimal VatAmount => Math.Round(TaxableAmount * SaudiVatRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public decimal TotalAmount => TaxableAmount + VatAmount;

    /// <summary>صافي الذمة (قبل الضريبة) بعد المحتجز.</summary>
    public decimal NetBillableAmount => CurrentWorkAmount - RetentionAmount;

    /// <summary>إجمالي مستحق على العميل (ذمم + محتجز + ضريبة).</summary>
    public decimal GrandTotal => NetBillableAmount + RetentionAmount + VatAmount;

    private ProgressBilling()
    {
        PeriodDescription = null!;
    }

    private ProgressBilling(long companyId, long projectId, long customerId, DateOnly billingDate,
        string periodDescription, decimal currentWorkAmount, decimal retentionPercent)
    {
        CompanyId = companyId;
        ProjectId = projectId;
        CustomerId = customerId;
        BillingDate = billingDate;
        PeriodDescription = periodDescription;
        CurrentWorkAmount = currentWorkAmount;
        RetentionPercent = retentionPercent;
    }

    public static ProgressBilling CreateDraft(long companyId, long projectId, long customerId,
        DateOnly billingDate, string periodDescription, decimal currentWorkAmount, decimal retentionPercent)
    {
        if (projectId <= 0)
            throw new AccountingException("المشروع مطلوب للمستخلص.");
        if (customerId <= 0)
            throw new AccountingException("العميل مطلوب للمستخلص.");
        if (currentWorkAmount <= 0)
            throw new AccountingException("قيمة الأعمال الحالية يجب أن تكون أكبر من صفر.");
        if (retentionPercent is < 0 or > 100)
            throw new AccountingException("نسبة المحتجز يجب أن تكون بين 0 و 100.");

        return new ProgressBilling(companyId, projectId, customerId, billingDate,
            (periodDescription ?? "").Trim(),
            Math.Round(currentWorkAmount, 2, MidpointRounding.AwayFromZero),
            Math.Round(retentionPercent, 2, MidpointRounding.AwayFromZero));
    }

    public JournalEntry Post(string billingNumber, Account receivable, Account retentionReceivable,
        Account revenue, Account vatOutput, decimal recognizedRevenue, Account? deferredRevenue = null)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(billingNumber))
            throw new AccountingException("رقم المستخلص مطلوب عند الترحيل.");
        if (recognizedRevenue < 0)
            throw new AccountingException("إيراد المستخلص لا يكون سالباً.");
        if (recognizedRevenue > CurrentWorkAmount)
            throw new AccountingException("الإيراد المعترف لا يتجاوز قيمة الأعمال في المستخلص.");

        BillingNumber = billingNumber.Trim();
        var entry = JournalEntry.CreateDraft(
            CompanyId, BillingDate,
            $"مستخلص أعمال رقم {BillingNumber} — {PeriodDescription}",
            sourceDocument: $"ProgressBilling:{BillingNumber}");

        var arAmount = NetBillableAmount + VatAmount;
        entry.AddLine(receivable, arAmount, 0m, PeriodDescription);
        if (RetentionAmount > 0)
            entry.AddLine(retentionReceivable, RetentionAmount, 0m, "محتجز مستخلص");

        entry.AddLine(revenue, 0m, recognizedRevenue, PeriodDescription);

        var deferred = Math.Round(CurrentWorkAmount - recognizedRevenue, 2, MidpointRounding.AwayFromZero);
        if (deferred > 0)
        {
            if (deferredRevenue is null)
                throw new AccountingException("حساب الإيراد المؤجل مطلوب عند اختلاف نسبة الإنجاز (IFRS 15).");
            entry.AddLine(deferredRevenue, 0m, deferred, "إيراد مؤجل — نسبة إنجاز");
        }

        if (VatAmount > 0)
            entry.AddLine(vatOutput, 0m, VatAmount, "ضريبة القيمة المضافة 15%");

        Status = ProgressBillingStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkCancelled()
    {
        if (Status != ProgressBillingStatus.Posted)
            throw new AccountingException("لا يُلغى إلا مستخلص مرحّل.");
        Status = ProgressBillingStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != ProgressBillingStatus.Draft)
            throw new AccountingException("المستخلص غير قابل للتعديل — الحالة: مرحّل.");
    }

    public static string StatusLabel(ProgressBillingStatus status) => status switch
    {
        ProgressBillingStatus.Draft => "مسودة",
        ProgressBillingStatus.Posted => "مرحّل",
        ProgressBillingStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

}

