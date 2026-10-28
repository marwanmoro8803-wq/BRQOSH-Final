using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Treasury;

/// <summary>كشف بنكي مستورد — سطور غير مطابقة تُطابق مع دفتر الأستاذ.</summary>
public class BankStatement
{
    private readonly List<BankStatementLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long BankAccountId { get; private set; }
    public string SourceFileName { get; private set; }
    public DateTime ImportedAtUtc { get; private set; }

    public IReadOnlyList<BankStatementLine> Lines => _lines.AsReadOnly();

    private BankStatement()
    {
        SourceFileName = null!;
    }

    private BankStatement(long companyId, long bankAccountId, string sourceFileName)
    {
        CompanyId = companyId;
        BankAccountId = bankAccountId;
        SourceFileName = sourceFileName.Trim();
        ImportedAtUtc = DateTime.UtcNow;
    }

    public static BankStatement Create(long companyId, long bankAccountId, string sourceFileName)
    {
        if (bankAccountId <= 0)
            throw new AccountingException("الحساب البنكي مطلوب.");
        if (string.IsNullOrWhiteSpace(sourceFileName))
            throw new AccountingException("اسم ملف الكشف مطلوب.");

        return new BankStatement(companyId, bankAccountId, sourceFileName);
    }

    public void AddLine(DateOnly transactionDate, string description, decimal signedAmount, string reference = "")
    {
        if (signedAmount == 0)
            throw new AccountingException("مبلغ السطر لا يمكن أن يكون صفراً.");
        _lines.Add(new BankStatementLine(transactionDate, description, reference, signedAmount));
    }

    /// <summary>
    /// <b>رصيدا الكشف كما يقولهما البنك</b> — الافتتاحيُّ والختاميّ.
    /// </summary>
    /// <remarks>
    /// كان الكشفُ سطوراً بلا رصيد: فلا يُعرف أكان الملفُّ كاملاً (سطرٌ ضائعٌ في التصدير لا يظهر)، ولا تُبنى مذكّرةُ تسويةٍ
    /// حقيقيّةٌ — رصيدُ البنك مقابلَ رصيد الدفتر. <b>والرصيدان معاً أو لا شيء، ويجب أن يُطابقا السطور</b>.
    /// </remarks>
    public decimal? OpeningBalance { get; private set; }

    /// <summary>(ح-١٥٤) الرصيدُ الختاميُّ للكشف كما يقوله البنك.</summary>
    public decimal? ClosingBalance { get; private set; }

    /// <summary>
    /// يضبط رصيدَي الكشف — <b>ويرفض ما لا يطابق السطور</b>: الافتتاحيُّ + مجموعُ الحركة = الختاميّ، وإلا فالملفُّ ناقصٌ أو زائد.
    /// </summary>
    public void SetBalances(decimal openingBalance, decimal closingBalance)
    {
        var opening = Math.Round(openingBalance, 2, MidpointRounding.AwayFromZero);
        var closing = Math.Round(closingBalance, 2, MidpointRounding.AwayFromZero);
        var movement = _lines.Sum(l => l.SignedAmount);
        var gap = closing - (opening + movement);
        if (gap != 0m)
            throw new AccountingException(
                $"الكشفُ لا يطابق رصيدَيه: الافتتاحيّ {opening:N2} والحركة {movement:N2} يعطيان {opening + movement:N2} "
                + $"والختاميّ {closing:N2} — فرقٌ {gap:N2}؛ الملفُّ ناقصٌ أو فيه سطرٌ زائد.");
        OpeningBalance = opening;
        ClosingBalance = closing;
    }

    public DateOnly? PeriodStart => _lines.Count == 0 ? null : _lines.Min(l => l.TransactionDate);
    public DateOnly? PeriodEnd => _lines.Count == 0 ? null : _lines.Max(l => l.TransactionDate);
}

public class BankStatementLine
{
    public DateOnly TransactionDate { get; private set; }
    public string Description { get; private set; }
    public string Reference { get; private set; }
    /// <summary>موجب = إيداع (مدين في GL) · سالب = سحب (دائن في GL).</summary>
    public decimal SignedAmount { get; private set; }
    public long? MatchedJournalEntryId { get; private set; }

    public bool IsMatched => MatchedJournalEntryId.HasValue;

    private BankStatementLine()
    {
        Description = null!;
        Reference = null!;
    }

    internal BankStatementLine(DateOnly transactionDate, string description, string reference, decimal signedAmount)
    {
        TransactionDate = transactionDate;
        Description = (description ?? "").Trim();
        Reference = (reference ?? "").Trim();
        SignedAmount = Math.Round(signedAmount, 2, MidpointRounding.AwayFromZero);
    }

    public void Match(long journalEntryId)
    {
        if (journalEntryId <= 0)
            throw new AccountingException("قيد اليومية غير صالح للمطابقة.");
        if (IsMatched)
            throw new AccountingException("السطر مطابق مسبقاً.");
        MatchedJournalEntryId = journalEntryId;
    }

    public void Unmatch() => MatchedJournalEntryId = null;

    public decimal ExpectedBookDebit => SignedAmount > 0 ? SignedAmount : 0m;
    public decimal ExpectedBookCredit => SignedAmount < 0 ? -SignedAmount : 0m;
}
