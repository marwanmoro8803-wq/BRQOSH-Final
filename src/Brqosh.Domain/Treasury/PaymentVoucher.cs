using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Treasury;

public enum PaymentVoucherStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2,
    Submitted = 3,
    Rejected = 4,
}

/// <summary>
/// سند صرف — سداد لمورد. القيد الآلي: الموردون (مدين) ← الصندوق/البنك (دائن).
/// </summary>
public class PaymentVoucher : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long SupplierId { get; private set; }
    public string? VoucherNumber { get; private set; }
    public DateOnly VoucherDate { get; private set; }
    public decimal Amount { get; private set; }
    public ReceiptMethod Method { get; private set; }
    public string Description { get; private set; }
    public string Reference { get; private set; }
    public PaymentVoucherStatus Status { get; private set; } = PaymentVoucherStatus.Draft;
    public long? WorkflowInstanceId { get; private set; }
    public long? JournalEntryId { get; private set; }

    /// <summary>الحساب البنكي عند طريقة الصرف بنك.</summary>
    public long? BankAccountId { get; private set; }

    // EF Core
    private PaymentVoucher()
    {
        Description = null!;
        Reference = null!;
    }

    private PaymentVoucher(long companyId, long supplierId, DateOnly voucherDate,
        decimal amount, ReceiptMethod method, string description, string reference)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        VoucherDate = voucherDate;
        Amount = amount;
        Method = method;
        Description = description;
        Reference = reference;
    }

    public static PaymentVoucher CreateDraft(long companyId, long supplierId, DateOnly voucherDate,
        decimal amount, ReceiptMethod method, string description, string reference = "",
        long? bankAccountId = null)
    {
        if (supplierId <= 0)
            throw new AccountingException("المورد مطلوب لسند الصرف.");
        if (amount <= 0)
            throw new AccountingException("مبلغ السند يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان السند مطلوب.");

        var voucher = new PaymentVoucher(companyId, supplierId, voucherDate,
            Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            method, description.Trim(), (reference ?? "").Trim());
        voucher.AssignBankAccount(bankAccountId);
        return voucher;
    }

    /// <summary>
    /// (CEEF-230 · ط1-٧) مركزُ تكلفة السند — يُختم على سطور المصروف في قيده.
    /// </summary>
    public long? CostCenterId { get; private set; }

    public void AssignCostCenter(long? costCenterId)
    {
        if (Status is not PaymentVoucherStatus.Draft and not PaymentVoucherStatus.Submitted)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        CostCenterId = costCenterId;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        if (Status is not PaymentVoucherStatus.Draft and not PaymentVoucherStatus.Submitted)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        BranchId = branchId;
    }

    public void AssignBankAccount(long? bankAccountId)
    {
        BankAccountId = Method == ReceiptMethod.Cash ? null : bankAccountId;
    }

    public void MarkSubmitted(long workflowInstanceId)
    {
        if (Status != PaymentVoucherStatus.Draft)
            throw new AccountingException("يُرسَل السند من مسودة فقط.");
        WorkflowInstanceId = workflowInstanceId;
        Status = PaymentVoucherStatus.Submitted;
    }

    public void MarkRejected()
    {
        if (Status != PaymentVoucherStatus.Submitted)
            throw new AccountingException("يُرفض سند مُرسَل فقط.");
        Status = PaymentVoucherStatus.Rejected;
    }

    /// <summary>الترحيل: مدين الموردين، دائن الصندوق/البنك.</summary>
    /// <summary>
    /// (CEEF-230 · ط4-٥) عملةُ السند — و<c>SAR</c> هي الأصل، و<see cref="Amount"/> بها.
    /// </summary>
    public string CurrencyCode { get; private set; } = "SAR";

    /// <summary>سعرُ الصرف <b>يوم السداد</b> — وهو غيرُ سعر يوم الفاتورة.</summary>
    public decimal ExchangeRate { get; private set; } = 1m;

    /// <summary>
    /// <b>ما حُرِّر من ذمّة المورّد بعملة الدفاتر</b> — وهو ما قُيّد مديناً في حساب الموردين.
    ///
    /// <para><b>والدرسُ مأخوذٌ من <c>ط1-٤</c> حرفاً</b>: هناك انفصل الدفترُ المساعد عن
    /// الأستاذ لأنّ المبلغَ الأجنبيَّ طُرح بلا تحويل. <b>وهنا يُخزَّن المحرَّرُ صراحةً</b>،
    /// ويُقرأ في <b>مساري</b> دفتر الموردين معاً — لا في أحدهما.</para>
    ///
    /// <para><b>وصفرٌ يعني سنداً قديماً</b> فيُقرأ <see cref="Amount"/> مكانَه.</para>
    /// </summary>
    public decimal PayableRelieved { get; private set; }

    /// <summary>المبلغُ بعملة الدفاتر — وهو ما يخرج من الصندوق فعلاً.</summary>
    public decimal FunctionalAmount =>
        Math.Round(Amount * ExchangeRate, 2, MidpointRounding.AwayFromZero);

    public void SetCurrency(string currencyCode, decimal exchangeRate)
    {
        if (Status is not PaymentVoucherStatus.Draft and not PaymentVoucherStatus.Submitted)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
            throw new AccountingException("رمز العملة ثلاثةُ أحرفٍ بمعيار ISO 4217.");
        if (exchangeRate <= 0m)
            throw new AccountingException("سعرُ الصرف يجب أن يكون موجباً.");

        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
    }

    /// <summary>
    /// الترحيل: مدين الموردين، دائن الصندوق/البنك — <b>وسطرُ فرقِ عملةٍ إن اختلف السعران</b>.
    /// </summary>
    /// <param name="payableFunctional">
    /// <b>القيمةُ التي نشأت بها الذمّة</b> بسعرِ يوم الفاتورة. والنقدُ يخرج بسعرِ يوم
    /// السداد، <b>والفرقُ ربحٌ أو خسارةٌ محقَّقة</b>.
    ///
    /// <para><b>والإشارةُ معكوسةٌ عن جانب العملاء</b>: نقدٌ يخرج <b>أقلَّ</b> من الذمّة
    /// المحرَّرة ربحٌ للمنشأة — دفعنا أقلَّ ممّا كنّا مدينين به. <b>ونسخُ منطق العملاء
    /// حرفاً يقلب الربحَ خسارةً في كلّ سدادٍ بالعملة.</b></para>
    /// </param>
    /// <summary>
    /// (CEEF-231 · ط1-٢ج) <b>خصمُ السداد المبكر المكتسَب — والحسابُ 5214 كان بلا كاتب.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>والإشارةُ معكوسةٌ عن جانب العملاء</b>: هناك نُعطي خصماً فيُقيَّد <b>مديناً</b>
    /// يُنقص الإيراد، وهنا <b>نكسبه</b> فيُقيَّد <b>دائناً</b> يُنقص المصروف. <b>ونسخُ
    /// جانب العملاء حرفاً يقلب الكسبَ خسارةً في كلّ سداد.</b></para>
    ///
    /// <para><b>وبغيره تبقى قيمةُ الخصم ذمّةً على المنشأة</b> تُدفع مرّةً أخرى أو تُشطب
    /// بقيدٍ يدويٍّ لا يراه دفترُ الموردين — <b>فتنكسر <c>هـ-٢</c></b>.</para>
    /// </remarks>
    public decimal SettlementDiscount { get; private set; }

    /// <summary>يُثبت خصمَ سدادٍ مبكرٍ اكتسبته المنشأةُ من مورّدها.</summary>
    public void GrantSettlementDiscount(decimal amount)
    {
        if (Status is not PaymentVoucherStatus.Draft and not PaymentVoucherStatus.Submitted)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        if (amount < 0m)
            throw new AccountingException("خصم السداد لا يكون سالباً.");

        SettlementDiscount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public JournalEntry Post(string voucherNumber, Account payableAccount, Account cashOrBankAccount,
        decimal? payableFunctional = null, Account? fxGainAccount = null, Account? fxLossAccount = null,
        Account? settlementDiscountAccount = null)
    {
        if (Status is not PaymentVoucherStatus.Draft and not PaymentVoucherStatus.Submitted)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new AccountingException("رقم السند مطلوب عند الترحيل.");

        var cash = FunctionalAmount;
        var releasedPayable = payableFunctional ?? cash;
        var difference = Math.Round(releasedPayable - cash, 2, MidpointRounding.AwayFromZero);

        var entry = JournalEntry.CreateDraft(
            CompanyId, VoucherDate,
            $"سند صرف رقم {voucherNumber} — {Description}",
            sourceDocument: $"PaymentVoucher:{voucherNumber}");

        // (CEEF-231 · ط1-٢ج) ‏**والخصمُ يُطفئ من الذمّة ما لم يخرج من الصندوق.**
        //
        // ‏فالمُحرَّرُ من الذمّة = النقدُ + الخصم، **والخصمُ دائنٌ على `5214`** — يُنقص
        // ‏المصروفَ لا يزيده. وبغيره تبقى قيمتُه ذمّةً على المنشأة تُدفع مرّةً أخرى.
        if (SettlementDiscount > 0m)
        {
            if (settlementDiscountAccount is null)
                throw new AccountingException(
                    "خصمُ سدادٍ بلا حساب «خصم مكتسب» — والقيدُ لا يُطفئ ما لا حسابَ له.");

            releasedPayable += SettlementDiscount;
        }

        entry.AddLine(payableAccount, releasedPayable, 0m, "سداد ذمة المورد");
        entry.AddLine(cashOrBankAccount, 0m, cash,
            Method == ReceiptMethod.Cash ? "صرف نقدي" : $"صرف بنكي {Reference}".Trim());

        if (SettlementDiscount > 0m)
            entry.AddLine(settlementDiscountAccount!, 0m, SettlementDiscount, "خصم سداد مبكر مكتسب");

        if (difference != 0m)
        {
            // ‏ذمّةٌ أكبرُ من النقد الخارج ⟵ **ربح** · وأصغرُ ⟵ خسارة.
            if (difference > 0m)
            {
                if (fxGainAccount is null)
                    throw new AccountingException("حساب أرباح فروق العملة المحققة مطلوب.");
                entry.AddLine(fxGainAccount, 0m, difference, "فرق عملة محقق");
            }
            else
            {
                if (fxLossAccount is null)
                    throw new AccountingException("حساب خسائر فروق العملة المحققة مطلوب.");
                entry.AddLine(fxLossAccount, -difference, 0m, "فرق عملة محقق");
            }
        }

        PayableRelieved = releasedPayable;
        VoucherNumber = voucherNumber.Trim();
        Status = PaymentVoucherStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkReversed()
    {
        if (Status != PaymentVoucherStatus.Posted)
            throw new AccountingException("لا يُعكس إلا سند صرف مرحّل.");
        Status = PaymentVoucherStatus.Reversed;
    }
}
