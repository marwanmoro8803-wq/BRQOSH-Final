using System.Globalization;
using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Setup;

public sealed class CsvDataMigrationService(BrqoshDbContext db) : IDataMigrationService
{
    public async Task<DataMigrationResult> ImportChartOfAccountsCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default)
    {
        // ‏(ح-١٣٣) والشاشةُ تَعِد بأنّ «الصفَّ الأوّل عناوين» — وكان يُقرأ حساباً ويُرفض.
        var (lines, first) = Body(csvContent, "Code");
        var details = new List<DataMigrationRowResult>();
        var imported = 0;
        var skipped = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            var parts = SplitCsvLine(lines[i]);
            if (parts.Length < 4)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "أعمدة مطلوبة: Code,NameAr,NameEn,Type"));
                skipped++;
                continue;
            }

            var code = parts[0].Trim();
            if (!code.All(char.IsDigit))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "رقم الحساب يجب أن يكون أرقاماً."));
                skipped++;
                continue;
            }

            if (await db.Accounts.AnyAsync(a => a.CompanyId == companyId && a.Code == code, ct))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "الحساب موجود مسبقاً."));
                skipped++;
                continue;
            }

            if (!Enum.TryParse<AccountType>(parts[3].Trim(), ignoreCase: true, out var type))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "نوع الحساب غير معروف."));
                skipped++;
                continue;
            }

            var isGroup = parts.Length > 4 && bool.TryParse(parts[4], out var g) && g;
            db.Accounts.Add(Account.Create(code, parts[1].Trim(), parts[2].Trim(), type, isGroup: isGroup, companyId: companyId));
            imported++;
            details.Add(new DataMigrationRowResult(lineNo, true, null));
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        return new DataMigrationResult("ChartOfAccounts", lines.Count, imported, skipped, details);
    }

    public async Task<DataMigrationResult> ImportCustomersCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default)
    {
        var (lines, first) = Body(csvContent, "NameAr");
        var details = new List<DataMigrationRowResult>();
        var imported = 0;
        var skipped = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            var parts = SplitCsvLine(lines[i]);
            if (parts.Length < 2)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "أعمدة مطلوبة: NameAr,VatNumber"));
                skipped++;
                continue;
            }

            var name = parts[0].Trim();
            var vat = parts[1].Trim();
            if (name.Length < 2)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "اسم العميل مطلوب."));
                skipped++;
                continue;
            }

            if (await db.Customers.AnyAsync(c => c.CompanyId == companyId && c.NameAr == name, ct))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "عميل بنفس الاسم موجود."));
                skipped++;
                continue;
            }

            db.Customers.Add(Customer.Create(companyId, name, vat));
            imported++;
            details.Add(new DataMigrationRowResult(lineNo, true, null));
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        return new DataMigrationResult("Customers", lines.Count, imported, skipped, details);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٣٣ · accounting-core#7) <b>الموردون — وكان الترحيلُ لا يعرفهم.</b> كالعملاء: الاسمُ والرقمُ
    /// الضريبيّ، والمكرَّرُ بالاسم يُتخطّى بسببه، ورقمٌ ضريبيٌّ معيبٌ يُرفض بسطره لا يُسقط الملفّ.
    /// </summary>
    public async Task<DataMigrationResult> ImportSuppliersCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default)
    {
        var (lines, first) = Body(csvContent, "NameAr");
        var details = new List<DataMigrationRowResult>();
        var imported = 0;
        var skipped = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            var parts = SplitCsvLine(lines[i]);
            var name = parts[0].Trim();
            var vat = parts.Length > 1 ? parts[1].Trim() : "";
            if (name.Length < 2)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "اسم المورد مطلوب."));
                skipped++;
                continue;
            }

            if (!seen.Add(name) || await db.Suppliers.AnyAsync(s => s.CompanyId == companyId && s.NameAr == name, ct))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "مورد بنفس الاسم موجود."));
                skipped++;
                continue;
            }

            try
            {
                db.Suppliers.Add(Supplier.Create(companyId, name, vat));
            }
            catch (ArgumentException ex)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, ex.Message));
                skipped++;
                continue;
            }

            imported++;
            details.Add(new DataMigrationRowResult(lineNo, true, null));
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        return new DataMigrationResult("Suppliers", lines.Count, imported, skipped, details);
    }

    /// <summary>‏مصدرُ قيد الميزان الافتتاحيّ — به يُعرف أنّه رُحّل.</summary>
    public const string OpeningTrialBalanceSource = "OpeningTrialBalance";

    /// <summary>
    /// ‏الحسابُ الوسيط: رصيدا الذمم (1104 و2101) يقفان فيه حتى يُفصَّلا مستنداتٍ لأصحابهما، فيعود صفراً.
    /// </summary>
    public const string OpeningClearingCode = "3107";

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٣٣ · accounting-core#7) <b>الميزانُ الافتتاحيّ — قيدٌ واحدٌ مرحَّلٌ أو لا شيء.</b>
    ///
    /// <para>‏كلُّ السطور تُفحص أوّلاً، <b>وسطرٌ معيبٌ واحدٌ يُسقط الميزانَ كلَّه</b> ويُعرض بسببه: نصفُ أرصدةٍ مرحَّلةٍ أسوأُ من
    /// لا أرصدة، إذ يُبنى عليها ولا يُرى ما نقص. <b>والذممُ لا تدخل حسابَي الرقابة</b> — فالدفترُ المساعدُ يُبنى من المستندات،
    /// وذمّةٌ في 1104 بلا مستندٍ لعميلٍ تكسر مطابقته — <b>بل <see cref="OpeningClearingCode"/></b>؛ والمخزونُ يدخل بحركاته
    /// الافتتاحيّة لكلّ صنف، والبضاعةُ المستلَمةُ غيرُ المفوترة تبدأ صفراً.</para>
    /// </summary>
    public async Task<DataMigrationResult> ImportOpeningTrialBalanceCsvAsync(
        long companyId, DateOnly openingDate, string csvContent, CancellationToken ct = default)
    {
        var (lines, first) = Body(csvContent, "AccountCode");

        var posted = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.SourceDocument == OpeningTrialBalanceSource)
            .WhereStanding(db.JournalEntries)
            .Select(e => e.EntryNumber)
            .FirstOrDefaultAsync(ct);
        if (posted is not null)
            return Refused(lines.Count, $"الميزانُ الافتتاحيُّ مرحَّلٌ سلفاً (قيد {posted}) — اعكسه أوّلاً إن أردت إعادته.");

        var accounts = await db.Accounts.Where(a => a.CompanyId == companyId).ToDictionaryAsync(a => a.Code, ct);
        var errors = new List<DataMigrationRowResult>();
        var rows = new List<(int LineNo, Account Account, decimal Debit, decimal Credit)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            if (ReadTrialBalanceLine(lines[i], accounts) is { } error)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, error));
                continue;
            }

            var parts = SplitCsvLine(lines[i]);
            if (rows.Any(r => r.Account.Code == parts[0]))
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الحساب {parts[0]} مكرَّر في الميزان."));
            else
                rows.Add((lineNo, accounts[parts[0]], Amount(parts[1]), Amount(parts[2])));
        }

        if (errors.Count > 0)
            return new DataMigrationResult("OpeningTrialBalance", lines.Count, 0, lines.Count, errors);

        var debit = rows.Sum(r => r.Debit);
        var credit = rows.Sum(r => r.Credit);
        if (rows.Count == 0 || debit != credit)
            return Refused(lines.Count,
                $"الميزانُ لا يتوازن: المدينُ {Format(debit)} والدائنُ {Format(credit)} — الفرق {Format(Math.Abs(debit - credit))}.");

        var clearing = await EnsureOpeningClearingAccountAsync(companyId, accounts, ct);
        var entry = JournalEntry.CreateDraft(companyId, openingDate, "الميزانُ الافتتاحيُّ عند بدء التشغيل",
            sourceDocument: OpeningTrialBalanceSource);
        foreach (var row in rows)
        {
            if (IsSubsidiaryControl(row.Account.Code))
                entry.AddLine(clearing, row.Debit, row.Credit,
                    $"رصيد {row.Account.Code} الافتتاحيّ — بانتظار تفصيله على أصحابه");
            else
                entry.AddLine(row.Account, row.Debit, row.Credit, "رصيد افتتاحي");
        }

        await new JournalPostingService(db).PostAsync(entry);

        return new DataMigrationResult("OpeningTrialBalance", lines.Count, rows.Count, 0,
            rows.Select(r => new DataMigrationRowResult(r.LineNo, true, null)).ToList());

        static DataMigrationResult Refused(int total, string why) =>
            new("OpeningTrialBalance", total, 0, total, [new DataMigrationRowResult(0, false, why)]);
    }

    private static string? ReadTrialBalanceLine(string line, IReadOnlyDictionary<string, Account> accounts)
    {
        var parts = SplitCsvLine(line);
        if (parts.Length != 3)
            return "أعمدة مطلوبة: AccountCode,Debit,Credit";

        var code = parts[0];
        if (!accounts.TryGetValue(code, out var account))
            return $"الحساب {code} غير موجود في الدليل.";
        if (account.IsGroup)
            return $"الحساب {code} تجميعيّ — الأرصدةُ على الحسابات التفصيليّة.";
        if (code == SaudiChartOfAccounts.SystemCodes.Inventory)
            return "المخزونُ يدخل بحركة مخزونٍ افتتاحيّة لكلّ صنف (بكمّيته وتكلفته) — لا برصيدٍ في الميزان.";
        if (code == SaudiChartOfAccounts.SystemCodes.GoodsReceivedNotInvoiced)
            return "حسابُ البضاعة المستلَمة غير المفوترة يبدأ صفراً — لا رصيدَ افتتاحيَّ له.";

        if (!TryAmount(parts[1], out var debit) || !TryAmount(parts[2], out var credit))
            return "مبلغ غير مقروء — رقمٌ بنقطةٍ عشريّة بلا فواصل آلاف.";
        if (debit < 0 || credit < 0)
            return "مبلغٌ سالب — الدائنُ في عمود الدائن.";
        if ((debit > 0) == (credit > 0))
            return "السطرُ مدينٌ أو دائن — لا كلاهما ولا لا شيء.";

        return null;
    }

    /// <summary>‏حسابا الرقابة اللذان يُبنى رصيدُهما من مستندات العملاء والموردين.</summary>
    private static bool IsSubsidiaryControl(string code) =>
        code is SaudiChartOfAccounts.SystemCodes.AccountsReceivable or SaudiChartOfAccounts.SystemCodes.AccountsPayable;

    private async Task<Account> EnsureOpeningClearingAccountAsync(
        long companyId, IReadOnlyDictionary<string, Account> accounts, CancellationToken ct)
    {
        if (accounts.TryGetValue(OpeningClearingCode, out var existing))
            return existing;

        var clearing = Account.Create(OpeningClearingCode, "أرصدة افتتاحية — بانتظار تفصيل الذمم",
            "Opening Balances — Clearing", AccountType.Equity, accounts["3"].Id,
            isGroup: false, isSystem: true, companyId: companyId);
        db.Accounts.Add(clearing);
        await db.SaveChangesAsync(ct);
        return clearing;
    }

    private static bool TryAmount(string text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out amount);

    private static decimal Amount(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    private static string Format(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>
    /// ‏سطورُ البيانات ورقمُ أوّلها في الملفّ — <b>وسطرُ العناوين يُتخطّى</b> إن بدأ بالعمود الأوّل المعلَن، فتبقى أرقامُ
    /// السطور المعروضة أرقامَها في الملفّ.
    /// </summary>
    private static (List<string> Lines, int FirstLineNumber) Body(string csvContent, string firstColumn)
    {
        var lines = ParseLines(csvContent);
        if (lines.Count > 0 && SplitCsvLine(lines[0])[0].Equals(firstColumn, StringComparison.OrdinalIgnoreCase))
            return (lines.Skip(1).ToList(), 2);
        return (lines, 1);
    }

    private static List<string> ParseLines(string csvContent) =>
        csvContent
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

    private static string[] SplitCsvLine(string line) =>
        line.Split(',', StringSplitOptions.TrimEntries);
}
