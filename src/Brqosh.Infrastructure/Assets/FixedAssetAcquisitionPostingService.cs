using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

/// <summary>
/// ترحيل اقتناء أصل ثابت — مدين الأصل / دائن الصندوق أو البنك.
///
/// <para>(جاهزيّةُ الإطلاق · ح-٩٩ · <c>treasury-payroll-assets#12</c>) <b>الأصلُ وقيدُ اقتنائه في معاملةٍ واحدة — ومن حيث دُفع.</b>
/// وكانت النافذةُ تحفظ الأصلَ ثمّ ترحّله في معاملةٍ ثانية: ترحيلٌ يُرفض (فترةٌ مقفلة · صلاحية · حساب) <b>يُبقي أصلاً بلا قيد اقتناء</b>
/// يُهلكه التشغيلُ التالي على مجمّعٍ لم يُقابَل بأصل. <b>وكان الدائنُ الصندوقَ أبداً</b> — فأصلٌ دُفع بتحويلٍ يُخرج نقداً لم يخرج من الصندوق —
/// <b>وترحيلٌ ثانٍ لأصلٍ واحدٍ يُقيّد اقتناءً ثانياً</b> بلا سؤال.</para>
/// </summary>
public class FixedAssetAcquisitionPostingService(BrqoshDbContext db)
{
    /// <param name="asset">أصلٌ جديدٌ لم يُحفظ — <b>يُضاف هنا مع قيده</b> — أو محفوظٌ بلا قيد اقتناء.</param>
    /// <param name="method">نقداً من الصندوق، أو من البنك.</param>
    /// <param name="bankAccountId">البنكُ المدفوعُ منه؛ <b>وفارغُه مع البنك هو البنكُ الافتراضيّ</b> كما في سند الصرف.</param>
    public async Task<JournalEntry> PostAcquisitionAsync(
        FixedAsset asset, ReceiptMethod method = ReceiptMethod.Cash, long? bankAccountId = null,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        if (method == ReceiptMethod.Cash && bankAccountId is not null)
            throw new AccountingException("دفعٌ نقديٌّ لا يُسمّى له بنك — اختر البنكَ طريقةً للدفع.");

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var source = $"FixedAsset:{asset.Code}";
            if (asset.AcquisitionJournalEntryId is not null
                || await db.JournalEntries
                    .Where(e => e.CompanyId == asset.CompanyId && e.SourceDocument == source)
                    .WhereStanding(db.JournalEntries)
                    .AnyAsync(token))
                throw new AccountingException($"اقتناءُ الأصل {asset.Code} مُرحَّلٌ مسبقاً — لا يُقيَّد ثانيةً.");

            // ‏**الأصلُ الجديدُ يُضاف داخل المعاملة** — فإن رُفض الترحيلُ بعدها ارتدّ معه، ولا يبقى أصلٌ بلا قيد.
            if (db.Entry(asset).State == EntityState.Detached)
                db.FixedAssets.Add(asset);

            await FinancialPostingSupport.ValidateAsync(
                db, asset.CompanyId, asset.AcquisitionDate, PostingDocumentKind.FixedAsset, ct: token);
            await BranchAssignmentHelper.EnsureAssignedAsync(asset, db, ct: token);

            var assetAccount = await db.Accounts.SingleAsync(
                a => a.Id == asset.GlAssetAccountId && a.CompanyId == asset.CompanyId, token);
            var funding = await FundingAccountAsync(asset.CompanyId, method, bankAccountId, token);

            var entry = JournalEntry.CreateDraft(
                asset.CompanyId, asset.AcquisitionDate,
                $"اقتناء أصل ثابت {asset.Code} — {asset.NameAr}",
                source);

            entry.AddLine(assetAccount, asset.AcquisitionCost, 0m, "تسجيل أصل ثابت");
            entry.AddLine(funding, 0m, asset.AcquisitionCost,
                method == ReceiptMethod.Cash ? "صرف نقدي — اقتناء أصل" : "تحويل بنكي — اقتناء أصل");

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(asset.CompanyId, asset.AcquisitionDate);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            asset.LinkAcquisitionJournal(entry.Id);
            await db.SaveChangesAsync(token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                asset.CompanyId, "FixedAssetAcquisition", asset.Id, asset.Code), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                asset.CompanyId, entry.Id, entry.EntryNumber!, asset.AcquisitionDate), token);

            await FinancialPostingSupport.AuditPostAsync(
                db, asset.CompanyId, "FixedAsset", asset.Code,
                reason: "اقتناء أصل ثابت", ct: token);

            return entry;
        }, ct);
    }

    /// <summary>
    /// ‏الصندوقُ نقداً، أو حسابُ أستاذ البنك من <see cref="BankGlAccountResolver"/> — <b>البنكُ المختارُ يُسأل قبله</b>: من المنشأة
    /// وغيرُ موقوف، <b>والموقوفُ يُرفض باسمه</b> (والمحلِّلُ يُخفيه بمرشّح الحذف فيسقط بخطأٍ لا يسمّيه).
    /// </summary>
    private async Task<Account> FundingAccountAsync(
        long companyId, ReceiptMethod method, long? bankAccountId, CancellationToken ct)
    {
        if (method == ReceiptMethod.Cash)
            return await db.Accounts.SingleAsync(
                a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand, ct);

        if (bankAccountId is long id)
        {
            var bank = await db.BankAccounts.IgnoreQueryFilters()
                .SingleOrDefaultAsync(b => b.CompanyId == companyId && b.Id == id, ct)
                ?? throw new AccountingException("الحساب البنكي المختار غير موجود في هذه المنشأة.");
            if (!bank.IsActive)
                throw new AccountingException($"الحساب البنكي «{bank.NameAr}» موقوف — لا يُدفع منه.");
        }

        return await BankGlAccountResolver.ResolveAsync(db, companyId, bankAccountId, ct);
    }
}
