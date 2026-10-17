using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c>) <b>إلغاءُ اقتناء أصلٍ أُدخل خطأً — بعكس قيده.</b>
///
/// <para><b>ما كان:</b> أصلٌ أُدخل بتكلفةٍ أو حسابٍ أو تاريخٍ خطأ لا يُصحَّح أبداً: قيدُ اقتنائه مرتبطٌ بمستند فيرفض العكسُ
/// المباشرُ عكسَه، ولا مسارَ في الأصل نفسِه. فيبقى في السجلّ، ويُهلَك كلَّ شهر، والاستبعادُ «يبيعه» بربحٍ أو خسارةٍ لم تقع.</para>
///
/// <para><b>ولا يُلغى إلا أصلٌ لم يُمسّ بعد اقتنائه</b>: قيدُ إهلاكٍ قائمٌ يُعكس أوّلاً (عكسُ تشغيل الإهلاك)، والمُستبعَدُ له
/// فاتورتُه، والمُرسمَلُ عليه تحسينٌ له قيدُه — فعكسُ الاقتناء وحدَه يتركها معلّقةً على أصلٍ لا وجود له.</para>
/// </summary>
public class FixedAssetCancellationService(BrqoshDbContext db)
{
    private const string DepreciationPrefix = "Depreciation:";

    public Task<JournalEntry> CancelAcquisitionAsync(
        long companyId, long assetId, DateOnly cancellationDate, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        return UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var asset = await db.FixedAssets.SingleAsync(a => a.CompanyId == companyId && a.Id == assetId, token);
            if (asset.CancelledOn is not null)
                throw new AccountingException($"اقتناءُ الأصل {asset.Code} مُلغًى مسبقاً.");
            if (!asset.IsActive || asset.DisposalJournalEntryId is not null)
                throw new AccountingException($"الأصل {asset.Code} مُستبعَد — يُصحَّح استبعادُه لا اقتناؤه.");
            if (asset.AcquisitionJournalEntryId is not long entryId)
                throw new AccountingException($"الأصل {asset.Code} بلا قيد اقتناء — لا شيءَ يُعكس.");

            var depreciationSource = DepreciationPrefix + asset.Code;
            if (await db.JournalEntries
                    .Where(e => e.CompanyId == companyId && e.SourceDocument == depreciationSource)
                    .WhereStanding(db.JournalEntries)
                    .AnyAsync(token))
                throw new AccountingException(
                    $"الأصل {asset.Code} أُهلك — اعكس تشغيلَ الإهلاك أوّلاً ثمّ ألغِ الاقتناء.");

            // ‏المُرسمَلُ عليه: تكلفتُه في السجلّ غيرُ ما قيّده الاقتناءُ على حسابه.
            var acquisition = await db.JournalEntries.Include(e => e.Lines)
                .SingleAsync(e => e.CompanyId == companyId && e.Id == entryId, token);
            var booked = acquisition.Lines.Where(l => l.AccountId == asset.GlAssetAccountId).Sum(l => l.Debit - l.Credit);
            if (booked != asset.AcquisitionCost)
                throw new AccountingException(
                    $"الأصل {asset.Code} رُسمِل عليه بعد اقتنائه — تكلفتُه {asset.AcquisitionCost:N2} والاقتناءُ {booked:N2}؛ "
                    + "يُلغى التحسينُ أوّلاً.");

            var reversal = await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, cancellationDate, username, fromSourceDocument: true, token);

            asset.CancelAcquisition(cancellationDate);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "FixedAsset", asset.Code, reason: "إلغاء اقتناء أصل ثابت", ct: token);
            return reversal;
        }, ct);
    }
}
