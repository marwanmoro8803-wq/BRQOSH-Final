namespace Brqosh.Domain.Accounting;

/// <summary>صاحبُ الذمّة الافتتاحيّة.</summary>
public enum OpeningBalanceParty
{
    Customer = 1,
    Supplier = 2,
}

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٤ · <c>accounting-core#7</c>) <b>ذمّةٌ مفتوحةٌ عند بدء التشغيل — فاتورةٌ من النظام السابق لم تُسدَّد.</b>
///
/// <para>‏الدفترُ المساعدُ للعملاء والموردين يُبنى من المستندات، فرصيدٌ افتتاحيٌّ في 1104 أو 2101 بلا مستندٍ لصاحبه <b>يكسر المطابقة
/// ولا يظهر في كشف حسابه ولا في أعماره</b>. وليست فاتورةَ مبيعاتٍ ولا مشتريات: تلك أُبلغت الهيئةُ بها وحُسبت ضريبتُها في النظام
/// السابق، <b>وإعادتُها فاتورةً تُرسلها ثانيةً وتُضاعف الضريبة</b>. فهي مستندٌ قائمٌ بذاته: يُرحَّل من الحساب الوسيط 3107 إلى
/// حساب الرقابة، ويقرؤه الدفترُ المساعدُ بتاريخ بدء التشغيل، وأعمارُه بتاريخ استحقاقه الأصليّ.</para>
/// </summary>
public class OpeningBalanceItem
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public OpeningBalanceParty Party { get; private set; }
    public long PartyId { get; private set; }

    /// <summary>‏رقمُ الفاتورة في النظام السابق — به يعرفها العميلُ والمورِّد.</summary>
    public string DocumentNumber { get; private set; }

    public DateOnly DocumentDate { get; private set; }
    public DateOnly DueDate { get; private set; }

    /// <summary>‏تاريخُ دخولها الدفاترَ — تاريخُ بدء التشغيل، لا تاريخُ الفاتورة الأصليّ.</summary>
    public DateOnly PostingDate { get; private set; }

    /// <summary>‏المتبقّي غيرُ المسدَّد — موجبٌ دائماً: على العميل لنا، أو علينا للمورِّد.</summary>
    public decimal Amount { get; private set; }

    public long JournalEntryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private OpeningBalanceItem()
    {
        DocumentNumber = null!;
    }

    public static OpeningBalanceItem Create(
        long companyId, OpeningBalanceParty party, long partyId, string documentNumber,
        DateOnly documentDate, DateOnly dueDate, DateOnly postingDate, decimal amount)
    {
        var number = (documentNumber ?? "").Trim();
        if (number.Length is 0 or > 50)
            throw new AccountingException("رقمُ الفاتورة الأصليّ مطلوب، ولا يزيد على خمسين محرفاً.");
        if (amount <= 0m || decimal.Round(amount, 2) != amount)
            throw new AccountingException("المتبقّي موجبٌ بخانتين عشريّتين على الأكثر.");
        if (dueDate < documentDate)
            throw new AccountingException("الاستحقاقُ لا يسبق تاريخَ الفاتورة.");
        if (documentDate > postingDate)
            throw new AccountingException("الذمّةُ الافتتاحيّةُ فاتورةٌ سبقت بدءَ التشغيل — وما بعده يُسجَّل في النظام نفسِه.");

        return new OpeningBalanceItem
        {
            CompanyId = companyId,
            Party = party,
            PartyId = partyId,
            DocumentNumber = number,
            DocumentDate = documentDate,
            DueDate = dueDate,
            PostingDate = postingDate,
            Amount = amount,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public void AttachJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;
}
