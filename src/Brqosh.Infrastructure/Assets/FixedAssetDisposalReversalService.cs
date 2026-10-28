using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

/// <summary>
/// <b>عكسُ استبعاد أصل — وبيعُه المُفوتَرُ يُلغى بإشعارٍ دائن.</b>
///
/// <para><b>ما كان:</b> استبعادٌ بتاريخٍ أو مقابلٍ أو مشترٍ خطأٍ لا يُصحَّح: قيدُه مرتبطٌ بمستندٍ فيرفض العكسُ المباشرُ عكسَه، والأصلُ خرج
/// من السجلّ فلا يُهلَك ولا يُعاد. <b>وبيعٌ فُوتِر لا يُمحى من الدفتر وحدَه</b>: فاتورتُه وصلت الهيئة، وعكسُ القيد بلا إشعارٍ دائنٍ يُبقي
/// التوريدَ قائماً عندها وضريبتَه في إقرارها.</para>
///
/// <para><b>فالعكسُ يعكس القيدَ</b> (المجمّعُ والتكلفةُ والمقبوضُ والضريبةُ والربحُ أو الخسارة كلُّها)، <b>ويُعيد الأصلَ إلى السجلّ</b>،
/// <b>ويُصدر للفاتورة إشعاراً دائناً يُشير إليها</b> (<see cref="ZatcaDocumentType.DisposalCancel"/>). <b>والفوترةُ المعطَّلةُ عند العكس
/// تمنعه</b> لبيعٍ فُوتِر — لا يُلغى توريدٌ في الدفتر وهو قائمٌ لدى الهيئة.</para>
/// </summary>
public class FixedAssetDisposalReversalService(BrqoshDbContext db)
{
    public async Task<JournalEntry> ReverseDisposalAsync(
        long companyId, long assetId, DateOnly reversalDate, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);
        ZatcaEInvoiceDocument? creditNote = null;

        var reversal = await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var asset = await db.FixedAssets.SingleAsync(a => a.CompanyId == companyId && a.Id == assetId, token);
            if (asset.IsActive || asset.DisposalJournalEntryId is not long entryId)
                throw new AccountingException($"الأصل {asset.Code} غيرُ مُستبعَد — لا استبعادَ يُعكس.");

            var invoiced = await db.ZatcaEInvoiceDocuments.AnyAsync(d =>
                d.CompanyId == companyId && d.DocumentType == ZatcaDocumentType.AssetDisposal
                && d.SourceDocumentId == asset.Id, token);

            var entry = await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, reversalDate, username, fromSourceDocument: true, token);

            asset.UndoDisposal();
            await db.SaveChangesAsync(token);

            if (invoiced)
            {
                creditNote = await new ZatcaEInvoiceGenerator(db).TryGenerateCancellationForAssetDisposalAsync(
                    asset.Id, reversalDate, $"عكس استبعاد الأصل {asset.Code}")
                    ?? throw new AccountingException(
                        $"بيعُ الأصل {asset.Code} فُوتِر بالفاتورة {asset.DisposalInvoiceNumber} والفوترةُ الإلكترونيّةُ معطَّلةٌ الآن — "
                        + "فعّلها ليصدر الإشعارُ الدائن، ثمّ اعكس.");
            }

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "FixedAssetDisposal", asset.Code, reason: "عكس استبعاد أصل ثابت",
                details: invoiced ? $"إشعارٌ دائنٌ للفاتورة {asset.DisposalInvoiceNumber}" : null, ct: token);
            return entry;
        }, ct);

        if (creditNote is not null)
            await ZatcaAutoSubmissionHelper.TrySubmitDocumentAsync(db, companyId, creditNote, ct);
        return reversal;
    }
}
