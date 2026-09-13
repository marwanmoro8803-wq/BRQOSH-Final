using System.Globalization;
using System.Text;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Treasury;

public sealed record BankStatementCsvLine(
    DateOnly TransactionDate,
    string Description,
    string Reference,
    decimal SignedAmount);

/// <summary>تحليل CSV بسيط: Date,Description,Amount[,Reference] أو Date,Description,Debit,Credit[,Reference].</summary>
public static class BankStatementCsvParser
{
    public static IReadOnlyList<BankStatementCsvLine> Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new AccountingException("ملف الكشف فارغ.");

        var rows = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rows.Length < 2)
            throw new AccountingException("الكشف يجب أن يحتوي على عنوان وسطر واحد على الأقل.");

        var header = SplitCsvRow(rows[0]);
        var dateIdx = IndexOf(header, "date", "transactiondate", "تاريخ");
        var descIdx = IndexOf(header, "description", "details", "memo", "بيان");
        var amountIdx = IndexOf(header, "amount", "signedamount", "مبلغ");
        var debitIdx = IndexOf(header, "debit", "مدين");
        var creditIdx = IndexOf(header, "credit", "دائن");
        var refIdx = IndexOf(header, "reference", "ref", "مرجع");

        if (dateIdx < 0 || descIdx < 0)
            throw new AccountingException("العناوين المطلوبة: Date و Description على الأقل.");

        var hasAmount = amountIdx >= 0;
        var hasDebitCredit = debitIdx >= 0 && creditIdx >= 0;
        if (!hasAmount && !hasDebitCredit)
            throw new AccountingException("أضف عمود Amount أو أعمدة Debit و Credit.");

        var lines = new List<BankStatementCsvLine>();
        for (var i = 1; i < rows.Length; i++)
        {
            var cols = SplitCsvRow(rows[i]);
            if (cols.Length == 0 || cols.All(string.IsNullOrWhiteSpace))
                continue;

            GuardAgainstASplitAmount(header, cols, i + 1, amountIdx, debitIdx, creditIdx);

            var date = ParseDate(Get(cols, dateIdx), i + 1);
            var description = Get(cols, descIdx);
            var reference = refIdx >= 0 ? Get(cols, refIdx) : "";

            decimal signed;
            if (hasAmount)
            {
                signed = CsvAmountText.ParseCell(Get(cols, amountIdx), i + 1, header[amountIdx]);
            }
            else
            {
                var debit = CsvAmountText.ParseCell(Get(cols, debitIdx), i + 1, header[debitIdx]);
                var credit = CsvAmountText.ParseCell(Get(cols, creditIdx), i + 1, header[creditIdx]);
                signed = debit - credit;
            }

            if (signed == 0)
                continue;

            lines.Add(new BankStatementCsvLine(date, description, reference, signed));
        }

        if (lines.Count == 0)
            throw new AccountingException("لم يُعثر على سطور صالحة في الكشف.");

        return lines;
    }

    private static int IndexOf(IReadOnlyList<string> header, params string[] names)
    {
        for (var i = 0; i < header.Count; i++)
        {
            var h = header[i].Trim().ToLowerInvariant();
            if (names.Any(n => h == n))
                return i;
        }
        return -1;
    }

    private static string Get(IReadOnlyList<string> cols, int index) =>
        index >= 0 && index < cols.Count ? cols[index].Trim() : "";

    private static string[] SplitCsvRow(string row)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in row)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    /// <summary>
    /// ‏(ن-٤٨) <b>القاسمةُ نفسُها — وهو نصفُ العطل الذي لا تراه أيُّ قاعدةٍ على الحقل.</b>
    ///
    /// <para>فاصلةٌ غيرُ مُقتبَسةٍ داخل مبلغٍ <b>تشقّ الحقلَ عمودَين</b>: مع ترويسة
    /// <c>Date,Description,Amount</c> يصير الصفُّ <c>2026-07-01,حوالة,1,234.56</c>
    /// أربعَ خلايا، <b>فيُقرأ المبلغُ 1 ويهبط «234.56» في عمودٍ مُهمَل</b> — نقصٌ ١٢٣٤
    /// ضعفاً <b>في الصيغة المحلّيّة لا الأجنبيّة</b>.</para>
    ///
    /// <para><b>وعدُّ الأعمدة وحدَه لا يكفي</b>: مع ترويسةٍ رباعيّةٍ تحمل <c>Reference</c> —
    /// وهي ترويسةُ ثلاثٍ من أربع عيّناتٍ في هذا المستودع — يصير العددُ ٤ = ٤ فيمرّ.
    /// <b>فيُقاس الشكل</b>: مبلغٌ عددٌ صحيحٌ قصير، تتلوه في عمودٍ غيرِ مبلغٍ مجموعةٌ من
    /// <b>ثلاثِ خاناتٍ بالضبط</b> ثمّ كسرٌ — وتلك بصمةُ مبلغٍ مشقوقٍ لا رقمِ مرجع.</para>
    ///
    /// <para><b>وما لا يُغطّى يُقال:</b> صفٌّ رباعيٌّ مرجعُه <i>فعلاً</i> «234.56» يُرفَض
    /// خطأً — وهو احتمالٌ نادرٌ <b>وصاخب</b>، وكلفتُه إعادةُ تصدير. والكلفةُ المقابلةُ قيدٌ
    /// مُرحَّلٌ بألفِ ضعفٍ خطأً <b>لا سبيلَ إلى عكسه</b> بعد أن يُرحَّل.</para>
    /// </summary>
    private static void GuardAgainstASplitAmount(
        string[] header, string[] cols, int rowNumber, int amountIdx, int debitIdx, int creditIdx)
    {
        if (cols.Length > header.Length)
            throw new AccountingException(
                $"‏السطر {rowNumber} فيه {cols.Length} خليّةً والترويسةُ {header.Length}: "
                + $"[{string.Join(" | ", cols)}] — "
                + "‏الأرجحُ فاصلةٌ داخل مبلغٍ بلا اقتباس. اكتب المبلغَ بلا فواصلِ آلاف.");

        foreach (var idx in new[] { amountIdx, debitIdx, creditIdx })
        {
            if (idx < 0 || idx + 1 >= cols.Length) continue;
            if (idx + 1 == amountIdx || idx + 1 == debitIdx || idx + 1 == creditIdx) continue;

            if (CsvAmountText.IsBareInteger(Get(cols, idx))
                && CsvAmountText.LooksLikeASplitThousandsGroup(Get(cols, idx + 1)))
            {
                throw new AccountingException(
                    $"‏السطر {rowNumber}: [{string.Join(" | ", cols)}] — "
                    + $"‏يبدو أنّ مبلغَ العمود «{header[idx]}» شقّته فاصلةُ آلافٍ بلا اقتباس. "
                    + "‏اكتب المبلغَ بلا فواصلِ آلاف.");
            }
        }
    }

    /// <summary>
    /// ‏(ن-٤٨) <b>تاريخٌ صريحٌ أو رفضٌ باسمِه.</b>
    ///
    /// <para><b>ما كان:</b> <c>InvariantCulture</c> نمطُها <c>MM/dd/yyyy</c>، فـ«05/01/2026»
    /// تُقرأ <b>١ مايو</b> لا ٥ يناير — <b>صامتةً لكلّ يومٍ ≤ ١٢ وصاخبةً لما فوقه</b>.
    /// وذلك أسوأُ خليط: النصفُ الصاخبُ يُعلّم المُشغّلَ أنّ المحلّلَ يفحص التواريخ.
    /// ثمّ كان يليها <c>DateOnly.TryParse(value, out d)</c> <b>بلا ثقافة</b> — فالنتيجةُ
    /// دالّةٌ في إعداداتِ ويندوز: على <c>fa-IR</c> تصير «2026-01-05» ⟵ <b>2647-03-25</b>.</para>
    ///
    /// <para><b>والتاريخُ ليس كالمبلغ</b>: خطؤه يعبر حدَّ فترةٍ ماليّةٍ قد تكون مُقفلة.</para>
    /// </summary>
    private static DateOnly ParseDate(string value, int rowNumber)
    {
        string[] accepted = ["yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd"];

        if (DateOnly.TryParseExact(value, accepted, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d))
            return d;

        throw new AccountingException(
            $"‏تاريخٌ غير مقبول في السطر {rowNumber}: «{value}» — الصيغةُ المقبولة "
            + "‏سنة-شهر-يوم مثل 2026-07-01. و«05/01/2026» لا تقول أيُّهما اليومُ وأيُّهما الشهر.");
    }
}
