using System.Globalization;
using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Setup;

public sealed class CsvDataMigrationService(BrqoshDbContext db) : IDataMigrationService
{
    public async Task<DataMigrationResult> ImportChartOfAccountsCsvAsync(
        long companyId, string csvContent, CancellationToken ct = default)
    {
        var lines = ParseLines(csvContent);
        var details = new List<DataMigrationRowResult>();
        var imported = 0;
        var skipped = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = i + 1;
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
        var lines = ParseLines(csvContent);
        var details = new List<DataMigrationRowResult>();
        var imported = 0;
        var skipped = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var lineNo = i + 1;
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

    private static List<string> ParseLines(string csvContent) =>
        csvContent
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

    private static string[] SplitCsvLine(string line) =>
        line.Split(',', StringSplitOptions.TrimEntries);
}
