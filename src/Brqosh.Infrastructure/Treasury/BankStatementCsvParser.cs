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

            var date = ParseDate(Get(cols, dateIdx));
            var description = Get(cols, descIdx);
            var reference = refIdx >= 0 ? Get(cols, refIdx) : "";

            decimal signed;
            if (hasAmount)
            {
                signed = ParseDecimal(Get(cols, amountIdx));
            }
            else
            {
                var debit = ParseDecimal(Get(cols, debitIdx));
                var credit = ParseDecimal(Get(cols, creditIdx));
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

    private static DateOnly ParseDate(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        if (DateOnly.TryParse(value, out d))
            return d;
        throw new AccountingException($"تاريخ غير صالح: {value}");
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;
        var normalized = value.Replace(",", "").Trim();
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        if (decimal.TryParse(normalized, out d))
            return d;
        throw new AccountingException($"مبلغ غير صالح: {value}");
    }
}
