using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Treasury;

public enum ReceiptMethod
{
    /// <summary>نقداً — يُقيَّد على الصندوق.</summary>
    Cash = 0,

    /// <summary>تحويل بنكي / شبكة — يُقيَّد على البنك.</summary>
    Bank = 1,
}

public enum ReceiptVoucherStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2,
}

/// <summary>
/// سند قبض — تحصيل من عميل. القيد الآلي: الصندوق/البنك (مدين) ← العملاء (دائن).
/// </summary>
public class ReceiptVoucher : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long CustomerId { get; private set; }
    public string? VoucherNumber { get; private set; }
    public DateOnly VoucherDate { get; private set; }
    public decimal Amount { get; private set; }
    public ReceiptMethod Method { get; private set; }
    public string Description { get; private set; }

    /// <summary>مرجع خارجي: رقم الحوالة/الشيك أو مرجع الشبكة.</summary>
    public string Reference { get; private set; }

    public ReceiptVoucherStatus Status { get; private set; } = ReceiptVoucherStatus.Draft;
    public long? JournalEntryId { get; private set; }

    /// <summary>الحساب البنكي عند طريقة القبض بنك — يُقيَّد على حسابه الفرعي في الأستاذ.</summary>
    public long? BankAccountId { get; private set; }

    // EF Core
    private ReceiptVoucher()
    {
        Description = null!;
        Reference = null!;
    }

    private ReceiptVoucher(long companyId, long customerId, DateOnly voucherDate,
        decimal amount, ReceiptMethod method, string description, string reference)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        VoucherDate = voucherDate;
        Amount = amount;
        Method = method;
        Description = description;
        Reference = reference;
    }

    public static ReceiptVoucher CreateDraft(long companyId, long customerId, DateOnly voucherDate,
        decimal amount, ReceiptMethod method, string description, string reference = "",
        long? bankAccountId = null)
    {
        if (customerId <= 0)
            throw new AccountingException("العميل مطلوب لسند القبض.");
        if (amount <= 0)
            throw new AccountingException("مبلغ السند يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان السند مطلوب.");

        var voucher = new ReceiptVoucher(companyId, customerId, voucherDate,
            Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            method, description.Trim(), (reference ?? "").Trim());
        voucher.AssignBankAccount(bankAccountId);
        return voucher;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        if (Status != ReceiptVoucherStatus.Draft)
            throw new AccountingException("السند مرحّل — لا يُعدَّل، والتصحيح بقيد عكسي.");
        BranchId = branchId;
    }

    public void AssignBankAccount(long? bankAccountId)
    {
        BankAccountId = Method == ReceiptMethod.Cash ? null : bankAccountId;
    }

    /// <summary>
    /// عملةُ السند — و<c>SAR</c> هي الأصل، و<see cref="Amount"/> بها.
    /// </summary>
    public string CurrencyCode { get; private set; } = "SAR";

    /// <summary>سعرُ الصرف <b>يوم التحصيل</b> — وهو غيرُ سعر يوم الفاتورة، ومن فرقهما يقع الربحُ أو الخسارة.</summary>
    public decimal ExchangeRate { get; private set; } = 1m;

    /// <summary>
    /// <b>ما حُرِّر من ذمّة العميل بعملة الدفاتر</b> — وهو ما قُيّد
    /// دائناً في حساب الذمم.
    ///
    /// <para><b>ويخالف <see cref="FunctionalAmount"/> بمقدار فرق العملة</b>: النقدُ يدخل
    /// بسعر يوم التحصيل، والذمّةُ تُحرَّر بقيمتها يوم نشأت. <b>ومن غير تخزينه ينفصل
    /// الدفترُ المساعد عن الأستاذ</b> — وقد أسقطت الهويّةُ هـ-١ أوّلَ تشغيل: الأستاذُ صفرٌ
    /// والمساعدُ 3,162.50، لأنّ المساعدَ كان يطرح المبلغَ الأجنبيَّ بلا تحويل.</para>
    ///
    /// <para><b>وصفرٌ يعني «سندٌ قديم»</b> — فيُقرأ <see cref="Amount"/> مكانَه، وهو
    /// الصوابُ لكلّ سندٍ بالريال.</para>
    /// </summary>
    public decimal ReceivableRelieved { get; private set; }

    /// <summary>المبلغُ بعملة الدفاتر — وهو ما يدخل الصندوقَ فعلاً.</summary>
    public decimal FunctionalAmount =>
        Math.Round(Amount * ExchangeRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// <b>خصمُ السداد المبكر — والحسابُ 4103 كان بلا كاتب.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>وهو ممارسةٌ قائمةٌ لا حالةٌ شاذّة</b>: «٢٪ خلال عشرة أيام، وإلا فثلاثون».
    /// فالعميلُ يدفع ٩٨٠ عن فاتورةِ ألف، <b>والذمّةُ تُطفأ كاملةً</b> والعشرون خصمٌ
    /// مسموحٌ به.</para>
    ///
    /// <para><b>وبغيره أحدُ أمرين، وكلاهما عطل:</b> إمّا تبقى العشرون ذمّةً مفتوحةً إلى
    /// الأبد <b>فتُقرأ ديناً وتُطالَب</b> ويكبر الوهمُ بكلّ فاتورة؛ وإمّا تُشطب <b>بقيدٍ
    /// يدويّ</b> — <b>وقيدٌ يدويٌّ يُغيّر الأستاذَ ولا يراه الدفترُ المساعد</b>، فتنكسر
    /// <c>هـ-١</c> ويفترق كشفُ العميل عن الميزانية.</para>
    ///
    /// <para><b>ولا يُخلط بفرق العملة</b>: الفرقُ يقع بغير إرادةٍ ويُقاس بسعرَين،
    /// <b>والخصمُ قرارٌ يُتّخذ ويُصرَّح به</b> — ودفنُهما في حسابٍ واحد يُخفي أيَّهما وقع.</para>
    /// </remarks>
    public decimal SettlementDiscount { get; private set; }

    /// <summary>
    /// يمنح خصمَ سدادٍ مبكرٍ يُطفئ من الذمّة أكثرَ ممّا دخل الصندوق.
    /// </summary>
    /// <remarks>
    /// <b>ولا يُمنَح على سندٍ بلا تخصيص</b>: الخصمُ يخصّ فاتورةً سُدِّدت مبكراً،
    /// <b>وخصمٌ على دفعةٍ عائمةٍ يُطفئ ذمّةً لا يُعرف أيُّها</b>.
    /// </remarks>
    public void GrantSettlementDiscount(decimal amount)
    {
        if (Status != ReceiptVoucherStatus.Draft)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        if (amount < 0m)
            throw new AccountingException("خصم السداد لا يكون سالباً.");

        SettlementDiscount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public void SetCurrency(string currencyCode, decimal exchangeRate, string? booksCurrencyCode = null)
    {
        if (Status != ReceiptVoucherStatus.Draft)
            throw new AccountingException("السند مرحّل — لا يُعدَّل.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
            throw new AccountingException("رمز العملة ثلاثةُ أحرفٍ بمعيار ISO 4217.");
        if (exchangeRate <= 0m)
            throw new AccountingException("سعرُ الصرف يجب أن يكون موجباً.");
        BooksCurrency.Validate(currencyCode, exchangeRate, booksCurrencyCode);

        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
    }

    /// <summary>
    /// (دفاترٌ بغير الريال) مستندٌ بسعر صرفٍ <c>1</c> هو بعملة الدفاتر نفسِها، فيُوسَم بها: المستندُ يُنشأ بـ<c>SAR</c> أصلاً،
    /// وفي منشأةٍ دفاترُها بغيره يُصحَّح الوسمُ قبل الحفظ (<c>BooksCurrencyInterceptor</c>). لا يمسّ مستنداً بعملةٍ أجنبيّةٍ صريحة.
    /// </summary>
    public void AlignToBooksCurrency(string booksCurrencyCode)
    {
        if (ExchangeRate == 1m && CurrencyCode == "SAR" && !string.IsNullOrWhiteSpace(booksCurrencyCode))
            CurrencyCode = booksCurrencyCode.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// الترحيل: مدين الصندوق/البنك، دائن العملاء — <b>وسطرُ فرقِ عملةٍ إن اختلف السعران</b>.
    ///
    /// <para> <paramref name="receivableFunctional"/> هو <b>القيمةُ التي
    /// نشأت بها الذمّة</b> (بسعرِ يوم الفاتورة)، والنقدُ يدخل بسعرِ يوم التحصيل.
    /// <b>والفرقُ ربحٌ أو خسارةٌ محقَّقة</b> — لا تقديرٌ يُعاد ولا يُعكس.</para>
    ///
    /// <para><b>وإسقاطُ هذا السطر يترك الذمّةَ مفتوحةً بفرقٍ لا يُغلق أبداً</b>: العميلُ
    /// دفع كلَّ ما عليه والدفاترُ تقول إنّه مدين.</para>
    /// </summary>
    public JournalEntry Post(string voucherNumber, Account cashOrBankAccount, Account receivableAccount,
        decimal? receivableFunctional = null, Account? fxGainAccount = null, Account? fxLossAccount = null,
        Account? settlementDiscountAccount = null)
    {
        if (Status != ReceiptVoucherStatus.Draft)
            throw new AccountingException("السند مرحّل — لا يُعدَّل، والتصحيح بقيد عكسي.");
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new AccountingException("رقم السند مطلوب عند الترحيل.");

        var cash = FunctionalAmount;
        var releasedReceivable = receivableFunctional ?? cash;
        var difference = Math.Round(cash - releasedReceivable, 2, MidpointRounding.AwayFromZero);

        var entry = JournalEntry.CreateDraft(
            CompanyId, VoucherDate,
            $"سند قبض رقم {voucherNumber} — {Description}",
            sourceDocument: $"ReceiptVoucher:{voucherNumber}");

        entry.AddLine(cashOrBankAccount, cash, 0m,
            Method == ReceiptMethod.Cash ? "تحصيل نقدي" : $"تحصيل بنكي {Reference}".Trim());

        // ‏**والخصمُ يُطفئ من الذمّة ما لم يدخل الصندوق.**
        //
        // ‏فالمُحرَّرُ من الذمّة = النقدُ + الخصم، **والقيدُ يحمل الخصمَ مديناً على `4103`**.
        // ‏وبغيره تبقى قيمةُ الخصم ذمّةً مفتوحةً تُقرأ ديناً وتُطالَب، **أو تُشطب بقيدٍ
        // ‏يدويٍّ لا يراه الدفترُ المساعد** فتنكسر هـ-١.
        if (SettlementDiscount > 0m)
        {
            if (settlementDiscountAccount is null)
                throw new AccountingException(
                    "خصمُ سدادٍ بلا حساب «خصم مسموح به» — والقيدُ لا يُطفئ ما لا حسابَ له.");

            releasedReceivable += SettlementDiscount;
            entry.AddLine(settlementDiscountAccount, SettlementDiscount, 0m, "خصم سداد مبكر");
        }

        entry.AddLine(receivableAccount, 0m, releasedReceivable, "تحصيل من ذمة العميل");

        if (difference != 0m)
        {
            // ‏نقدٌ أكثرُ من الذمّة ⟵ ربح · وأقلُّ ⟵ خسارة.
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

        ReceivableRelieved = releasedReceivable;
        VoucherNumber = voucherNumber.Trim();
        Status = ReceiptVoucherStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkReversed()
    {
        if (Status != ReceiptVoucherStatus.Posted)
            throw new AccountingException("لا يُعكس إلا سند قبض مرحّل.");
        Status = ReceiptVoucherStatus.Reversed;
    }
}
