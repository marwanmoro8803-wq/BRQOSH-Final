using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

/// <summary>ترحيل اقتناء أصل ثابت — مدين الأصل / دائن الصندوق.</summary>
public class FixedAssetAcquisitionPostingService(BrqoshDbContext db)
{
    public async Task<JournalEntry> PostAcquisitionAsync(FixedAsset asset, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await FinancialPostingSupport.ValidateAsync(
                db, asset.CompanyId, asset.AcquisitionDate, PostingDocumentKind.FixedAsset, ct: token);
            await BranchAssignmentHelper.EnsureAssignedAsync(asset, db, ct: token);

            var assetAccount = await db.Accounts.SingleAsync(
                a => a.Id == asset.GlAssetAccountId && a.CompanyId == asset.CompanyId, token);
            var cash = await db.Accounts.SingleAsync(
                a => a.CompanyId == asset.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand, token);

            var entry = JournalEntry.CreateDraft(
                asset.CompanyId, asset.AcquisitionDate,
                $"اقتناء أصل ثابت {asset.Code} — {asset.NameAr}",
                $"FixedAsset:{asset.Code}");

            entry.AddLine(assetAccount, asset.AcquisitionCost, 0m, "تسجيل أصل ثابت");
            entry.AddLine(cash, 0m, asset.AcquisitionCost, "صرف نقدي — اقتناء أصل");

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
}
