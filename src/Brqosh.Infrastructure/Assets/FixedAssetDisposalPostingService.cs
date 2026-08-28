using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

/// <summary>ترحيل استبعاد أصل ثابت — إزالة الأصل من الدفاتر مع ربح/خسارة البيع.</summary>
public class FixedAssetDisposalPostingService(BrqoshDbContext db)
{
    public async Task<JournalEntry> PostDisposalAsync(
        FixedAsset asset, decimal saleProceeds, DateOnly disposalDate, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            asset.EnsureReadyForDisposal();
            if (saleProceeds < 0)
                throw new AccountingException("عائد البيع لا يكون سالباً.");

            // (CEEF-229 · م0-١٥) العائد يُقرَّب إلى منزلتين قبل أيّ استعمال.
            //
            // كان `gainLoss` وحده مُقرَّباً وسطرُ النقدية يأخذ الرقم خاماً، و`NumericInput` يقبل
            // أيّ عدد منازل. فعائد 4,000.005 على أصلٍ تكلفته 5,000 بلا إهلاك يُنتج فرقاً بـ0.005
            // فيرفضه `Post` برسالة **تلوم القيد لا المُدخَل** — ويعجز المستخدم عن فهم السبب.
            //
            // ولم يظهر هذا قبل اليوم لأن الاستبعاد كان محصوراً بالأصل المستهلَك بالكامل، وعائدُ
            // الخردة رقمٌ مستدير. أمّا البيع المبكّر — الذي فتحناه للتوّ — فثمنُه تفاوضيّ.
            saleProceeds = Math.Round(saleProceeds, 2, MidpointRounding.AwayFromZero);

            await FinancialPostingSupport.ValidateAsync(
                db, asset.CompanyId, disposalDate, PostingDocumentKind.FixedAssetDisposal,
                accountIds: [asset.GlAssetAccountId, asset.GlDepreciationAccountId], ct: token);

            var assetAccount = await db.Accounts.SingleAsync(
                a => a.Id == asset.GlAssetAccountId && a.CompanyId == asset.CompanyId, token);
            var accumAccount = await db.Accounts.SingleAsync(
                a => a.Id == asset.GlDepreciationAccountId && a.CompanyId == asset.CompanyId, token);
            var cash = await db.Accounts.SingleAsync(
                a => a.CompanyId == asset.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand, token);
            var otherIncome = await db.Accounts.SingleAsync(
                a => a.CompanyId == asset.CompanyId && a.Code == "4106", token);
            var miscExpense = await db.Accounts.SingleAsync(
                a => a.CompanyId == asset.CompanyId && a.Code == "5215", token);

            var bookValue = asset.BookValue;
            var gainLoss = Math.Round(saleProceeds - bookValue, 2, MidpointRounding.AwayFromZero);

            var entry = JournalEntry.CreateDraft(
                asset.CompanyId, disposalDate,
                $"استبعاد أصل {asset.Code} — {asset.NameAr}",
                $"FixedAssetDisposal:{asset.Code}");

            if (asset.AccumulatedDepreciation > 0)
                entry.AddLine(accumAccount, asset.AccumulatedDepreciation, 0m, "إقفال مجمع الإهلاك");
            if (saleProceeds > 0)
                entry.AddLine(cash, saleProceeds, 0m, "عائد بيع/خردة");
            if (gainLoss > 0)
                entry.AddLine(otherIncome, 0m, gainLoss, "ربح استبعاد أصل");
            else if (gainLoss < 0)
                entry.AddLine(miscExpense, -gainLoss, 0m, "خسارة استبعاد أصل");
            entry.AddLine(assetAccount, 0m, asset.AcquisitionCost, "إخراج أصل من الدفاتر");

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(asset.CompanyId, disposalDate);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            asset.LinkDisposalJournal(entry.Id);
            asset.MarkDisposed();
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, asset.CompanyId, "FixedAssetDisposal", asset.Code,
                reason: "استبعاد أصل ثابت", details: $"عائد: {saleProceeds:N2} — ربح/خسارة: {gainLoss:N2}", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                asset.CompanyId, "FixedAssetDisposal", asset.Id, asset.Code), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                asset.CompanyId, entry.Id, entry.EntryNumber!, disposalDate), token);

            return entry;
        }, ct);
    }
}
