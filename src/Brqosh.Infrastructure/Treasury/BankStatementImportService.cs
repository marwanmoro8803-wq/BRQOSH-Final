using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public sealed class BankStatementImportService(BrqoshDbContext db)
{
    public async Task<BankStatement> ImportCsvAsync(
        long companyId,
        long bankAccountId,
        string sourceFileName,
        string csv,
        CancellationToken ct = default,
        decimal? openingBalance = null,
        decimal? closingBalance = null)
    {
        SecurityContext.EnsureCanPostAccounting();
        _ = await db.BankAccounts.SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId, ct);

        var parsed = BankStatementCsvParser.Parse(csv);
        var statement = BankStatement.Create(companyId, bankAccountId, sourceFileName);
        foreach (var line in parsed)
            statement.AddLine(line.TransactionDate, line.Description, line.SignedAmount, line.Reference);

        // ‏(ح-١٥٤) ‏**رصيدا البنك معاً أو لا شيء، ويُطابقان السطور** — وإلا فالملفُّ ناقص، ولا يُحفظ ليُسوّى عليه.
        if (openingBalance.HasValue != closingBalance.HasValue)
            throw new Domain.Accounting.AccountingException("رصيدا الكشف يُدخلان معاً — الافتتاحيُّ والختاميّ — أو يُتركان معاً.");
        if (openingBalance is decimal opening && closingBalance is decimal closing)
            statement.SetBalances(opening, closing);

        db.BankStatements.Add(statement);
        await db.SaveChangesAsync(ct);

        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(),
            "استيراد كشف بنكي", "BankStatement", statement.Id.ToString(),
            $"{sourceFileName} · {parsed.Count} سطر");

        return statement;
    }
}
