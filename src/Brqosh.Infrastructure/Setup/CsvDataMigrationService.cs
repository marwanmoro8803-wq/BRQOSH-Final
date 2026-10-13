using System.Globalization;
using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
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
            var (error, debitAmount, creditAmount) = ReadTrialBalanceLine(lines[i], lineNo, accounts);
            if (error is not null)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, error));
                continue;
            }

            var parts = SplitCsvLine(lines[i]);
            if (rows.Any(r => r.Account.Code == parts[0]))
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الحساب {parts[0]} مكرَّر في الميزان."));
            else
                rows.Add((lineNo, accounts[parts[0]], debitAmount, creditAmount));
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

    /// <summary>
    /// ‏سطرُ الميزان: الخطأُ أو المبلغان. <b>والمبلغُ يُقرأ بـ<see cref="CsvAmountText"/> وحدَه</b> — فلا تُقرأ «1,500» خمسةَ
    /// عشرَ مئة ولا «1e3» ألفاً (<c>CsvAmountTextIsTheOnlyFileParserTests</c>).
    /// </summary>
    private static (string? Error, decimal Debit, decimal Credit) ReadTrialBalanceLine(
        string line, int lineNo, IReadOnlyDictionary<string, Account> accounts)
    {
        var parts = SplitCsvLine(line);
        if (parts.Length != 3)
            return ("أعمدة مطلوبة: AccountCode,Debit,Credit", 0m, 0m);

        var code = parts[0];
        if (!accounts.TryGetValue(code, out var account))
            return ($"الحساب {code} غير موجود في الدليل.", 0m, 0m);
        if (account.IsGroup)
            return ($"الحساب {code} تجميعيّ — الأرصدةُ على الحسابات التفصيليّة.", 0m, 0m);
        if (code == SaudiChartOfAccounts.SystemCodes.GoodsReceivedNotInvoiced)
            return ("حسابُ البضاعة المستلَمة غير المفوترة يبدأ صفراً — لا رصيدَ افتتاحيَّ له.", 0m, 0m);

        // ‏والرفضُ يُنقل بنصّه إلى سطره — لا يُبتلَع (`NoSilentAccountingFailureTests`).
        decimal debit = 0m, credit = 0m;
        string? refused = null;
        try
        {
            debit = CsvAmountText.ParseCell(parts[1], lineNo, "Debit");
            credit = CsvAmountText.ParseCell(parts[2], lineNo, "Credit");
        }
        catch (AccountingException ex)
        {
            refused = ex.Message;
        }

        if (refused is not null)
            return (refused, 0m, 0m);
        if (debit < 0 || credit < 0)
            return ("مبلغٌ سالب — الدائنُ في عمود الدائن.", 0m, 0m);
        if ((debit > 0) == (credit > 0))
            return ("السطرُ مدينٌ أو دائن — لا كلاهما ولا لا شيء.", 0m, 0m);

        return (null, debit, credit);
    }

    /// <summary>
    /// ‏حساباتُ الرقابة التي يُبنى رصيدُها من دفترٍ مساعد: العملاءُ والموردون من مستنداتهم، <b>والمخزونُ من حركاته</b> (ح-١٣٥ —
    /// وكان يُرفض في الميزان فلا يتوازن ميزانٌ حقيقيٌّ فيه مخزون).
    /// </summary>
    private static bool IsSubsidiaryControl(string code) =>
        code is SaudiChartOfAccounts.SystemCodes.AccountsReceivable
            or SaudiChartOfAccounts.SystemCodes.AccountsPayable
            or SaudiChartOfAccounts.SystemCodes.Inventory;

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

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٣٥ · commercial-readiness#15) <b>الأصناف</b> — «Code,NameAr,Unit,Type,SalePrice»؛ والنوعُ
    /// <c>Stockable</c> أو <c>Service</c>، والمكرَّرُ بالكود يُتخطّى بسببه.
    /// </summary>
    public async Task<DataMigrationResult> ImportProductsCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default)
    {
        var (lines, first) = Body(csvContent, "Code");
        var details = new List<DataMigrationRowResult>();
        var imported = 0;
        var existing = (await db.Products.Where(p => p.CompanyId == companyId).Select(p => p.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            var parts = SplitCsvLine(lines[i]);
            if (parts.Length != 5)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "أعمدة مطلوبة: Code,NameAr,Unit,Type,SalePrice"));
                continue;
            }

            if (!existing.Add(parts[0]))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, $"الصنف {parts[0]} موجود مسبقاً."));
                continue;
            }

            if (!Enum.TryParse<ProductType>(parts[3], ignoreCase: true, out var type) || !Enum.IsDefined(type))
            {
                details.Add(new DataMigrationRowResult(lineNo, false, "النوع Stockable أو Service."));
                continue;
            }

            try
            {
                var price = CsvAmountText.ParseCell(parts[4], lineNo, "SalePrice");
                db.Products.Add(Product.Create(companyId, parts[0], parts[1], parts[2], type, price));
            }
            catch (AccountingException ex)
            {
                details.Add(new DataMigrationRowResult(lineNo, false, ex.Message));
                continue;
            }

            imported++;
            details.Add(new DataMigrationRowResult(lineNo, true, null));
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        return new DataMigrationResult("Products", lines.Count, imported, lines.Count - imported, details);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٣٥) <b>المخزونُ الافتتاحيّ</b> — «ProductCode,Quantity,UnitCost» في المستودع الافتراضيّ بتاريخ بدء
    /// التشغيل: حركةٌ افتتاحيّةٌ واحدة تُرحَّل بخدمة الحركات نفسِها (طبقاتُ التكلفة والقيد)، <b>وقيدُها يُدائن 3107</b> فيُفصّل رصيدَ
    /// المخزون الذي وقف فيه الميزان. كلُّها أو لا شيء، وبعد الميزان وحدَه.
    /// </summary>
    public async Task<DataMigrationResult> ImportOpeningStockCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default)
    {
        var (lines, first) = Body(csvContent, "ProductCode");
        var opening = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.SourceDocument == OpeningTrialBalanceSource)
            .WhereStanding(db.JournalEntries)
            .Select(e => (DateOnly?)e.EntryDate)
            .FirstOrDefaultAsync(ct);
        if (opening is not DateOnly openingDate)
            return new DataMigrationResult("OpeningStock", lines.Count, 0, lines.Count, [new DataMigrationRowResult(0, false,
                "رحِّل الميزانَ الافتتاحيَّ أوّلاً — به يُعرف تاريخُ بدء التشغيل، وفيه يقف رصيدُ المخزون في 3107.")]);

        var warehouseId = await db.Warehouses
            .Where(w => w.CompanyId == companyId)
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Id)
            .Select(w => (long?)w.Id)
            .FirstOrDefaultAsync(ct);
        if (warehouseId is not long warehouse)
            return new DataMigrationResult("OpeningStock", lines.Count, 0, lines.Count,
                [new DataMigrationRowResult(0, false, "لا مستودعَ للمنشأة — أنشئ مستودعاً أوّلاً.")]);

        var products = await db.Products.Where(p => p.CompanyId == companyId)
            .ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, ct);
        var errors = new List<DataMigrationRowResult>();
        var rows = new List<(int LineNo, Product Product, decimal Quantity, decimal UnitCost)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            var parts = SplitCsvLine(lines[i]);
            if (parts.Length != 3)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, "أعمدة مطلوبة: ProductCode,Quantity,UnitCost"));
                continue;
            }

            if (!products.TryGetValue(parts[0], out var product))
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الصنف {parts[0]} غير موجود — استورد الأصنافَ أوّلاً."));
                continue;
            }

            if (product.Type != ProductType.Stockable)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الصنف {parts[0]} خدمة — لا مخزونَ له."));
                continue;
            }

            decimal quantity = 0m, unitCost = 0m;
            string? refused = null;
            try
            {
                quantity = CsvAmountText.Parse(parts[1], lineNo, "Quantity");
                unitCost = CsvAmountText.Parse(parts[2], lineNo, "UnitCost");
            }
            catch (AccountingException ex)
            {
                refused = ex.Message;
            }

            if (refused is not null)
                errors.Add(new DataMigrationRowResult(lineNo, false, refused));
            else if (quantity <= 0m || unitCost < 0m)
                errors.Add(new DataMigrationRowResult(lineNo, false, "الكمّيّةُ موجبة والتكلفةُ لا تقلّ عن صفر."));
            else if (rows.Any(r => r.Product.Id == product.Id))
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الصنف {parts[0]} مكرَّر في الملفّ."));
            else
                rows.Add((lineNo, product, quantity, unitCost));
        }

        if (errors.Count > 0 || rows.Count == 0)
            return new DataMigrationResult("OpeningStock", lines.Count, 0, lines.Count,
                errors.Count > 0 ? errors : [new DataMigrationRowResult(0, false, "لا أصنافَ في الملفّ.")]);

        var movement = StockMovement.CreateDraft(companyId, openingDate, StockMovementType.Opening, warehouse,
            "المخزونُ الافتتاحيُّ عند بدء التشغيل", sourceDocument: "OpeningStock");
        foreach (var row in rows)
            movement.AddLine(row.Product.Id, row.Quantity, row.UnitCost);
        await new StockMovementPostingService(db).PostAsync(movement);

        return new DataMigrationResult("OpeningStock", lines.Count, rows.Count, 0,
            rows.Select(r => new DataMigrationRowResult(r.LineNo, true, null)).ToList());
    }

    public Task<DataMigrationResult> ImportOpeningReceivablesCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default) =>
        ImportOpenItemsAsync(companyId, OpeningBalanceParty.Customer, csvContent, ct);

    public Task<DataMigrationResult> ImportOpeningPayablesCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default) =>
        ImportOpenItemsAsync(companyId, OpeningBalanceParty.Supplier, csvContent, ct);

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٣٤ · accounting-core#7) <b>الذممُ المفتوحةُ لكلّ عميلٍ ومورِّد — ويُفرَغ بها الحسابُ الوسيط.</b>
    ///
    /// <para>‏الميزانُ الافتتاحيُّ (ح-١٣٣) أوقف رصيدَي الذمم في 3107 لأنّ الدفترَ المساعدَ يُبنى من المستندات. وهنا تصير الذمّةُ
    /// مستندَها: <see cref="OpeningBalanceItem"/> برقم الفاتورة الأصليّ وتاريخها واستحقاقها، <b>يقرؤه الرصيدُ والكشفُ والأعمار</b>،
    /// ويُرحَّل قيدٌ واحدٌ من 3107 إلى حساب الرقابة بتاريخ بدء التشغيل. <b>وكلُّها أو لا شيء</b> كالميزان؛ وفي معاملةٍ واحدة،
    /// فلا قيدَ بلا مستنداته ولا مستندَ بلا قيده — وكلاهما يفصل الأستاذَ عن المساعد.</para>
    /// </summary>
    private async Task<DataMigrationResult> ImportOpenItemsAsync(
        long companyId, OpeningBalanceParty party, string csvContent, CancellationToken ct)
    {
        var customer = party == OpeningBalanceParty.Customer;
        var kind = customer ? "OpeningReceivables" : "OpeningPayables";
        var (lines, first) = Body(csvContent, customer ? "CustomerName" : "SupplierName");

        var opening = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.SourceDocument == OpeningTrialBalanceSource)
            .WhereStanding(db.JournalEntries)
            .Select(e => (DateOnly?)e.EntryDate)
            .FirstOrDefaultAsync(ct);
        if (opening is not DateOnly postingDate)
            return new DataMigrationResult(kind, lines.Count, 0, lines.Count, [new DataMigrationRowResult(0, false,
                "رحِّل الميزانَ الافتتاحيَّ أوّلاً — به يُعرف تاريخُ بدء التشغيل، وفيه يقف رصيدُ الذمم في 3107.")]);

        var parties = customer
            ? await db.Customers.Where(c => c.CompanyId == companyId).Select(c => new { c.Id, c.NameAr }).ToListAsync(ct)
            : await db.Suppliers.Where(s => s.CompanyId == companyId).Select(s => new { s.Id, s.NameAr }).ToListAsync(ct);
        var byName = parties.GroupBy(p => p.NameAr).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
        var existing = (await db.OpeningBalanceItems
                .Where(i => i.CompanyId == companyId && i.Party == party)
                .Select(i => new { i.PartyId, i.DocumentNumber })
                .ToListAsync(ct))
            .Select(i => (i.PartyId, i.DocumentNumber))
            .ToHashSet();

        var errors = new List<DataMigrationRowResult>();
        var items = new List<(int LineNo, OpeningBalanceItem Item, string Name)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = first + i;
            var parts = SplitCsvLine(lines[i]);
            if (parts.Length != 5)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false,
                    $"أعمدة مطلوبة: {(customer ? "CustomerName" : "SupplierName")},DocumentNumber,DocumentDate,DueDate,Amount"));
                continue;
            }

            if (!byName.TryGetValue(parts[0], out var ids))
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, customer
                    ? $"العميل «{parts[0]}» غير موجود — استورد العملاءَ أوّلاً."
                    : $"المورد «{parts[0]}» غير موجود — استورد الموردين أوّلاً."));
                continue;
            }

            if (ids.Count > 1)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الاسم «{parts[0]}» لأكثر من طرف — لا يُعرف لمن الذمّة."));
                continue;
            }

            if (!TryDate(parts[2], out var documentDate) || !TryDate(parts[3], out var dueDate))
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, "التاريخ بصيغة yyyy-MM-dd."));
                continue;
            }

            decimal amount;
            try
            {
                amount = CsvAmountText.Parse(parts[4], lineNo, "Amount");
            }
            catch (AccountingException ex)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, ex.Message));
                continue;
            }

            if (!existing.Add((ids[0], parts[1])))
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, $"الفاتورة {parts[1]} لهذا الطرف مستوردةٌ سلفاً."));
                continue;
            }

            try
            {
                items.Add((lineNo, OpeningBalanceItem.Create(
                    companyId, party, ids[0], parts[1], documentDate, dueDate, postingDate, amount), parts[0]));
            }
            catch (AccountingException ex)
            {
                errors.Add(new DataMigrationRowResult(lineNo, false, ex.Message));
            }
        }

        if (errors.Count > 0 || items.Count == 0)
            return new DataMigrationResult(kind, lines.Count, 0, lines.Count,
                errors.Count > 0 ? errors : [new DataMigrationRowResult(0, false, "لا ذممَ في الملفّ.")]);

        var accounts = await db.Accounts.Where(a => a.CompanyId == companyId).ToDictionaryAsync(a => a.Code, ct);
        var clearing = await EnsureOpeningClearingAccountAsync(companyId, accounts, ct);
        var control = accounts[customer
            ? SaudiChartOfAccounts.SystemCodes.AccountsReceivable
            : SaudiChartOfAccounts.SystemCodes.AccountsPayable];

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var entry = JournalEntry.CreateDraft(companyId, postingDate,
                customer ? "ذممُ العملاء المفتوحةُ عند بدء التشغيل" : "ذممُ الموردين المفتوحةُ عند بدء التشغيل",
                sourceDocument: kind);
            var total = items.Sum(i => i.Item.Amount);
            foreach (var (_, item, name) in items)
            {
                var description = $"رصيد افتتاحي — {name} — {item.DocumentNumber}";
                if (customer)
                    entry.AddLine(control, item.Amount, 0m, description);
                else
                    entry.AddLine(control, 0m, item.Amount, description);
            }

            if (customer)
                entry.AddLine(clearing, 0m, total, "تفصيلُ ذمم العملاء الافتتاحيّة");
            else
                entry.AddLine(clearing, total, 0m, "تفصيلُ ذمم الموردين الافتتاحيّة");

            await new JournalPostingService(db).PostAsync(entry);
            foreach (var (_, item, _) in items)
            {
                item.AttachJournalEntry(entry.Id);
                db.OpeningBalanceItems.Add(item);
            }

            await db.SaveChangesAsync(token);
            return entry.Id;
        }, ct);

        return new DataMigrationResult(kind, lines.Count, items.Count, 0,
            items.Select(i => new DataMigrationRowResult(i.LineNo, true, null)).ToList());
    }

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

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

    /// <summary>‏(ح-١٣٥) بالقاسم المشترك — فالحقلُ المُقتبَسُ حقلٌ واحد (<see cref="CsvRow"/>).</summary>
    private static string[] SplitCsvLine(string line) =>
        CsvRow.Split(line).Select(f => f.Trim()).ToArray();
}
