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
/// <para> <b>الأصلُ وقيدُ اقتنائه في معاملةٍ واحدة — ومن حيث دُفع.</b>
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
            await EnsureNotPostedAsync(asset, source, token);

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
    /// <b>رصيدٌ افتتاحيٌّ لأصلٍ قائمٍ قبل بدء التشغيل</b> — مدين الأصل
    /// بتكلفته، ودائن مجمّع الإهلاك بما أُهلك قبل الدفاتر، <b>والصافي على الأرباح المبقاة (3103)</b> كما يفعل افتتاحيُّ المخزون.
    /// <b>ولا نقدَ يخرج</b>: وكان الأصلُ القائمُ لا يُدخَل إلا اقتناءً يُدان به الصندوقُ بتكلفته كلِّها وبتاريخ اقتنائه الأصليّ.
    ///
    /// <para><b>بتاريخ أوّل يومٍ بعد <see cref="FixedAsset.DepreciatedThrough"/></b>، <b>وبوسم الاقتناء نفسِه</b> — فلا يُرحَّل مرّتين،
    /// ولا يُرحَّل اقتناءٌ فوقه؛ والأصلُ الجديدُ يُضاف داخل المعاملة كالاقتناء.</para>
    /// </summary>
    /// <summary>
    /// ‏ <b>يُسجّل أصلاً تقتنيه فاتورةُ مورّد</b> — بلا قيد: فالقيدُ
    /// ‏قيدُ الفاتورة حين تُرحَّل، بحسابه وضريبة مدخلاته ودائنِه المورّد.
    /// </summary>
    public async Task RegisterAwaitingInvoiceAsync(FixedAsset asset, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        if (!asset.AwaitingInvoice)
            throw new AccountingException($"الأصل {asset.Code} لا ينتظر فاتورة — يُرحَّل اقتناءً.");
        if (await db.FixedAssets.AnyAsync(a => a.CompanyId == asset.CompanyId && a.Code == asset.Code, ct))
            throw new AccountingException($"رمزُ الأصل {asset.Code} مستعمَل.");

        db.FixedAssets.Add(asset);
        await BranchAssignmentHelper.EnsureAssignedAsync(asset, db, ct: ct);
        await db.SaveChangesAsync(ct);
        await FinancialPostingSupport.AuditPostAsync(
            db, asset.CompanyId, "FixedAsset", asset.Code, reason: "تسجيل أصل ينتظر فاتورة مورّد", ct: ct);
    }

    public async Task<JournalEntry> PostOpeningAsync(FixedAsset asset, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        if (asset.DepreciatedThrough is not { } through)
            throw new AccountingException($"الأصل {asset.Code} ليس رصيداً افتتاحيّاً — يُرحَّل اقتناءً.");
        var openingDate = through.AddDays(1);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var source = $"FixedAsset:{asset.Code}";
            await EnsureNotPostedAsync(asset, source, token);
            if (db.Entry(asset).State == EntityState.Detached)
                db.FixedAssets.Add(asset);

            await FinancialPostingSupport.ValidateAsync(
                db, asset.CompanyId, openingDate, PostingDocumentKind.FixedAsset, ct: token);
            await BranchAssignmentHelper.EnsureAssignedAsync(asset, db, ct: token);

            var assetAccount = await db.Accounts.SingleAsync(
                a => a.Id == asset.GlAssetAccountId && a.CompanyId == asset.CompanyId, token);
            var accumulated = await db.Accounts.SingleAsync(
                a => a.Id == asset.GlDepreciationAccountId && a.CompanyId == asset.CompanyId, token);
            var retained = await db.Accounts.SingleAsync(
                a => a.CompanyId == asset.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.RetainedEarnings, token);

            var entry = JournalEntry.CreateDraft(
                asset.CompanyId, openingDate,
                $"رصيد افتتاحي لأصل ثابت {asset.Code} — {asset.NameAr}",
                source);
            entry.AddLine(assetAccount, asset.AcquisitionCost, 0m, "رصيد افتتاحي — تكلفة الأصل");
            if (asset.AccumulatedDepreciation > 0m)
                entry.AddLine(accumulated, 0m, asset.AccumulatedDepreciation, "رصيد افتتاحي — مجمع الإهلاك");
            if (asset.BookValue > 0m)
                entry.AddLine(retained, 0m, asset.BookValue, "رصيد افتتاحي — صافي الأصل");

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(asset.CompanyId, openingDate);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            asset.LinkAcquisitionJournal(entry.Id);
            await db.SaveChangesAsync(token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                asset.CompanyId, "FixedAssetOpening", asset.Id, asset.Code), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                asset.CompanyId, entry.Id, entry.EntryNumber!, openingDate), token);
            await FinancialPostingSupport.AuditPostAsync(
                db, asset.CompanyId, "FixedAsset", asset.Code,
                reason: "رصيد افتتاحي لأصل ثابت", ct: token);

            return entry;
        }, ct);
    }

    /// <summary>
    /// ‏الأصلُ مربوطٌ بقيدٍ أو قيدٌ قائمٌ (<c>WhereStanding</c>) بوسمه — <b>اقتناءً كان أو رصيداً افتتاحيّاً</b>، فلا يُقيَّد ثانيةً.
    /// </summary>
    private async Task EnsureNotPostedAsync(FixedAsset asset, string source, CancellationToken token)
    {
        if (asset.AcquisitionJournalEntryId is not null
            || await db.JournalEntries
                .Where(e => e.CompanyId == asset.CompanyId && e.SourceDocument == source)
                .WhereStanding(db.JournalEntries)
                .AnyAsync(token))
            throw new AccountingException($"اقتناءُ الأصل {asset.Code} مُرحَّلٌ مسبقاً — لا يُقيَّد ثانيةً.");
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
