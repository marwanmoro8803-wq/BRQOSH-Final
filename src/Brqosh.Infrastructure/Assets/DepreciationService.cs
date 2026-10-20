using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

public class DepreciationService(BrqoshDbContext db)
{
    private const string SourcePrefix = "Depreciation:";

    public async Task<int> RunMonthlyAsync(long companyId, DateOnly runDate, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var assets = (await db.FixedAssets
                .Where(a => a.CompanyId == companyId && a.IsActive)
                .ToListAsync(token))
                .Where(a => a.BookValue > a.SalvageValue)
                .ToList();

            var posting = new JournalPostingService(db);
            await posting.EnsurePeriodOpenAsync(companyId, runDate);

            var depExpense = await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "5211", token);
            int count = 0;

            foreach (var asset in assets)
            {
                var sourceDoc = $"{SourcePrefix}{asset.Code}";

                // ‏(جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11) ‏**آخرُ شهرٍ أُهلك — ثمّ كلُّ ما استحقّ بعده.**
                // ‏وكان السؤالُ «هل أُهلك هذا الشهرُ بعينه؟» فيُحمَّل شهرٌ واحد: أصلٌ لم يُقتنَ بعدُ يُهلَك، وشهرٌ فات
                // ‏لا يُستدرَك، وشهرٌ سابقٌ بعد لاحقه يُحمَّل ثانيةً. **والقائمُ من `WhereStanding`** (ح-٤٧) لا
                // ‏`Status != Draft` — فالمعكوسُ ليس إهلاكاً قائماً.
                var lastDepreciated = await db.JournalEntries
                    .Where(e => e.CompanyId == companyId && e.SourceDocument == sourceDoc)
                    .WhereStanding(db.JournalEntries)
                    .OrderByDescending(e => e.EntryDate)
                    .Select(e => (DateOnly?)e.EntryDate)
                    .FirstOrDefaultAsync(token);

                var months = asset.MonthsDue(lastDepreciated, runDate);
                // ‏(treasury-payroll-assets#11 · قرارُ المالك 2026-10-20) ‏وشهرُ الاقتناء بأيّامه لا كاملاً.
                var amount = asset.ApplyDepreciationDue(lastDepreciated, runDate);
                if (amount <= 0) continue;

                // ‏**والاستدراكُ قيدٌ واحدٌ بتاريخ التشغيل** — أوّلُ فترةٍ مفتوحة، مذهبُ SAP وOracle؛ والفتراتُ التي
                // ‏فاتت قد تكون أُقفلت، فلا يُكتب فيها.
                var accum = await db.Accounts.SingleAsync(a => a.Id == asset.GlDepreciationAccountId, token);
                var entry = JournalEntry.CreateDraft(companyId, runDate,
                    months == 1 ? $"إهلاك {asset.NameAr}" : $"إهلاك {asset.NameAr} — {months} أشهر (استدراك)",
                    sourceDoc);
                entry.AddLine(depExpense, amount, 0m, asset.NameAr);
                entry.AddLine(accum, 0m, amount, "مجمع إهلاك");
                var num = await new JournalEntryNumberGenerator(db).NextAsync(companyId, runDate);
                entry.Post(num);
                db.JournalEntries.Add(entry);
                count++;
            }

            if (count > 0)
            {
                await db.SaveChangesAsync(token);
                await new AuditService(db).LogAsync(companyId, username, "إهلاك شهري",
                    "FixedAsset", runDate.ToString("yyyy-MM"), $"{count} أصل");
            }

            return count;
        }, ct);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٢ · <c>treasury-payroll-assets#10</c>) <b>عكسُ تشغيل الإهلاك بتاريخه</b> — وكان لا يُعكس: العكسُ العامُّ يرفض
    /// قيداً له مستند، والمجمَّعُ على السجلّ لا ينقص بعكس القيد. <b>والأحدثُ وحدَه لكلّ أصل</b> — فعكسُ تشغيلٍ ولاحقُه قائمٌ يُخرج الشهورَ
    /// من وسط السلسلة، ويستدركها التشغيلُ التالي فوق اللاحق؛ <b>ولا لأصلٍ مُستبعَد</b>؛ <b>ولا تشغيلَ بذلك التاريخ ⟵ رفضٌ باسمه</b>.
    /// </summary>
    public async Task<int> ReverseRunAsync(long companyId, DateOnly runDate, DateOnly reversalDate, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var runEntries = await db.JournalEntries
                .Include(e => e.Lines)
                .Where(e => e.CompanyId == companyId && e.EntryDate == runDate
                            && e.SourceDocument != null && e.SourceDocument.StartsWith(SourcePrefix))
                .WhereStanding(db.JournalEntries)
                .ToListAsync(token);
            if (runEntries.Count == 0)
                throw new AccountingException("لا تشغيلَ إهلاكٍ قائماً بهذا التاريخ يُعكس.");

            foreach (var entry in runEntries)
            {
                var code = entry.SourceDocument![SourcePrefix.Length..];
                var asset = await db.FixedAssets.SingleAsync(a => a.CompanyId == companyId && a.Code == code, token);
                if (!asset.IsActive)
                    throw new AccountingException($"الأصل {asset.Code} مُستبعَد — لا يُعكس إهلاكُه.");

                var later = await db.JournalEntries
                    .Where(e => e.CompanyId == companyId && e.SourceDocument == entry.SourceDocument && e.EntryDate > runDate)
                    .WhereStanding(db.JournalEntries)
                    .AnyAsync(token);
                if (later)
                    throw new AccountingException($"للأصل {asset.Code} إهلاكٌ أحدثُ قائم — يُعكس الأحدثُ أوّلاً.");

                var amount = entry.Lines
                    .Where(l => l.AccountId == asset.GlDepreciationAccountId)
                    .Sum(l => l.Credit - l.Debit);
                await new JournalReversalService(db).ReverseAsync(
                    companyId, entry.Id, reversalDate, username, fromSourceDocument: true, token);
                asset.UndoDepreciation(amount);
            }

            await db.SaveChangesAsync(token);
            await new AuditService(db).LogAsync(companyId, username, "عكس إهلاك شهري",
                "FixedAsset", runDate.ToString("yyyy-MM-dd"), $"{runEntries.Count} أصل");
            return runEntries.Count;
        }, ct);
    }
}
