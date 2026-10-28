using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Assets;

/// <summary>ترحيل استبعاد أصل ثابت — إزالة الأصل من الدفاتر مع ربح/خسارة البيع.</summary>
public class FixedAssetDisposalPostingService(BrqoshDbContext db)
{
    /// <param name="saleProceeds">
    /// (CEEF-232 · ض-٢) <b>المقابلُ قبل الضريبة</b> — لا المحصَّل.
    ///
    /// <para><b>والربحُ والخسارةُ يُقاسان عليه</b>، وهذا أخطرُ التباسٍ في البند: أصلٌ
    /// دفتريّتُه ٩٦٬٠٠٠ يُباع بمئة ألفٍ ربحُه أربعةُ آلاف. <b>ولو قِيس على المحصَّل
    /// (١١٥٬٠٠٠) لبلغ تسعةَ عشرَ ألفاً</b> — فيُعلَن ربحٌ لم يقع <b>وتُدفع عليه زكاةٌ
    /// ويُوزَّع</b>، وهو مالُ الهيئة في يد المنشأة.</para>
    /// </param>
    /// <param name="vatCategory">
    /// (CEEF-232 · ض-٢) فئةُ التوريد — <b>والأساسيّةُ هي الأصل</b>: بيعُ أصلٍ من أصول
    /// النشاط توريدٌ خاضعٌ نظاماً.
    ///
    /// <para><b>وغيرُها يقع فعلاً:</b> تصديرُ معدّةٍ إلى الخارج توريدٌ صفريُّ النسبة —
    /// <b>وفرضُ خمسةَ عشرَ بالمئة عليه يجعل المنشأةَ تدفع من مالها ضريبةً لا تُحصّلها</b>.</para>
    /// </param>
    /// <param name="buyerCustomerId">
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>المشتري — ولم يكن للاستبعاد مشترٍ قطّ.</b>
    ///
    /// <para><b>وفارغٌ مقبولٌ عمداً</b>: بيعُ خردةٍ نقداً لمشترٍ لا يُعرَّف عن نفسه واقعٌ
    /// يوميّ، <b>ويُفوتَر مبسَّطةً</b> كما تفعل نقطةُ البيع — وهو مسارٌ نظاميّ.
    /// <b>وفرضُ اختيارِ عميلٍ كان سيُغلق بابَ التخلّص من الخردة</b> لأجل بابٍ آخر.</para>
    /// </param>
    /// <param name="vatExemptionReasonCode">
    /// رمزُ سبب الإعفاء (BT-121) — <b>مطلوبٌ لغير الفئة الأساسيّة</b> وترفض الهيئةُ بدونه
    /// (<c>BR-KSA-CL-06</c>). <b>ويُطلب هنا لا عند التوليد</b>: المولّدُ يستهلك عدّاداً
    /// تسلسليّاً قبل أن يفحص، <b>ورقمٌ استُهلك لمستندٍ مرفوضٍ يُلوّث سلسلةَ البصمات بعده</b>.
    /// </param>
    public async Task<JournalEntry> PostDisposalAsync(
        FixedAsset asset, decimal saleProceeds, DateOnly disposalDate, string username,
        VatCategory vatCategory = VatCategory.Standard,
        long? buyerCustomerId = null,
        string? vatExemptionReasonCode = null,
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
                a => a.CompanyId == asset.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.OtherIncome, token);
            var miscExpense = await db.Accounts.SingleAsync(
                a => a.CompanyId == asset.CompanyId && a.Code == SaudiChartOfAccounts.PostingRoleCodes.MiscellaneousExpenses, token);

            // (CEEF-232 · ض-٤) ‏**ومنشأةٌ غير مسجَّلةٍ لا تُصدر ضريبةً أصلاً.**
            //
            // ‏وكانت الفئةُ تُفترض «أساسيّةً» لكلّ مستدعٍ — **والشاشةُ الوحيدةُ لا تُمرّر فئة**.
            // ‏فمنشأةٌ بلا رقمٍ ضريبيٍّ تبيع معدّةً بمئة ألفٍ **تُقيّد التزاماً مُختلَقاً بخمسةَ
            // ‏عشرَ ألفاً تجاه الهيئة**، وتُحصّلها من المشتري بغير حقّ — ثمّ يبلغها الإقرارُ
            // ‏بعد ض-١ فتُقدَّم بضريبةٍ لا تخصّها.
            //
            // ‏**والحكمُ من بيانات المنشأة لا من وسيطٍ يُنسى** — والقاعدةُ هي قاعدةُ نقطة
            // ‏البيع نفسُها (`PosSessionPage`): رقمٌ ضريبيٌّ من خمسةَ عشرَ رقماً.
            var company = await db.Companies.SingleAsync(c => c.Id == asset.CompanyId, token);
            var effectiveCategory = company.VatRegistrationNumber.Trim().Length == 15
                ? vatCategory
                : VatCategory.OutOfScope;

            // (CEEF-232 · ض-٢) ‏وحسابُ المخرجات يُقرأ عند الحاجة وحدَها — فمنشأةٌ لا تبيع
            // ‏أصولَها إلا خردةً لا يلزمها الحسابُ، **وقراءتُه بلا حاجةٍ تُسقط استبعاداً
            // ‏صحيحاً على دليلٍ ناقص**.
            var vat = Math.Round(
                saleProceeds * effectiveCategory.RateFor(VatCalculator.SaudiVatRate),
                2, MidpointRounding.AwayFromZero);
            var vatOutput = vat > 0
                ? await db.Accounts.SingleAsync(
                    a => a.CompanyId == asset.CompanyId
                         && a.Code == SaudiChartOfAccounts.SystemCodes.VatOutput, token)
                : null;

            // (قرارُ المالك 2026-10-20 · ملاحظةُ تدقيق Codex) ‏**إهلاكُ ما انقضى قبل الاستبعاد يُقيَّد أوّلاً.**
            //
            // ‏كان الربحُ والخسارةُ يُقاسان على الدفتريّة «وقتَ آخر تشغيل» — والإهلاكُ يُشغَّل بزرّ — فأصلٌ يُباع في
            // ‏٢٨ فبراير قبل تشغيل فبراير يُخرَج بدفتريّةٍ تفوق الحقيقة بقسط الشهر، فتُضخَّم الخسارةُ وتُهمَل المصروفُ.
            // ‏والآن: ما استحقّ من الأشهر السابقة كاملاً، وشهرُ الاستبعاد بأيّامه المنقضية (يومُ الاستبعاد محسوب)،
            // ‏بالقاعدة نفسها التي يُشغَّل بها الإهلاك — `ApplyDepreciationDue` — فلا يختلف الرقمان.
            var sourceDoc = $"Depreciation:{asset.Code}";
            var lastDepreciated = await db.JournalEntries
                .Where(e => e.CompanyId == asset.CompanyId && e.SourceDocument == sourceDoc)
                .WhereStanding(db.JournalEntries)
                .OrderByDescending(e => e.EntryDate)
                .Select(e => (DateOnly?)e.EntryDate)
                .FirstOrDefaultAsync(token);

            var catchUp = asset.ApplyDepreciationDue(lastDepreciated, disposalDate, throughRunDay: true);
            if (catchUp > 0)
            {
                var depExpense = await db.Accounts.SingleAsync(
                    a => a.CompanyId == asset.CompanyId && a.Code == SaudiChartOfAccounts.PostingRoleCodes.DepreciationExpense, token);
                var catchUpEntry = JournalEntry.CreateDraft(
                    asset.CompanyId, disposalDate, $"إهلاك {asset.NameAr} — حتى تاريخ الاستبعاد", sourceDoc);
                catchUpEntry.AddLine(depExpense, catchUp, 0m, asset.NameAr);
                catchUpEntry.AddLine(accumAccount, 0m, catchUp, "مجمع إهلاك");
                catchUpEntry.Post(await new JournalEntryNumberGenerator(db).NextAsync(asset.CompanyId, disposalDate));
                db.JournalEntries.Add(catchUpEntry);
                await db.SaveChangesAsync(token);
            }

            var bookValue = asset.BookValue;
            var gainLoss = Math.Round(saleProceeds - bookValue, 2, MidpointRounding.AwayFromZero);

            var entry = JournalEntry.CreateDraft(
                asset.CompanyId, disposalDate,
                $"استبعاد أصل {asset.Code} — {asset.NameAr}",
                $"FixedAssetDisposal:{asset.Code}");

            if (asset.AccumulatedDepreciation > 0)
                entry.AddLine(accumAccount, asset.AccumulatedDepreciation, 0m, "إقفال مجمع الإهلاك");
            // (CEEF-232 · ض-٢) ‏والنقدُ الداخل هو المقابلُ **زائد الضريبة** — وهو ما يقبضه
            // ‏البائع فعلاً. **والضريبةُ ليست إيراداً ولا تمسّ الربح**: أمانةٌ تُورَّد.
            if (saleProceeds + vat > 0)
                entry.AddLine(cash, saleProceeds + vat, 0m, "عائد بيع/خردة");
            if (vatOutput is not null)
                entry.AddLine(vatOutput, 0m, vat, "ضريبة القيمة المضافة على بيع أصل");
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

            // (جاهزيّةُ الإطلاق · ح-١٦) ‏**وبيعُ أصلٍ توريدٌ خاضعٌ نظاماً — فله فاتورةُ ضريبة.**
            //
            // ‏كان القيدُ يُنشئ التزاماً على `2106` **بلا مستندٍ خلفَه**: لا رقمَ فاتورةٍ
            // ‏ولا مشترياً ولا مستنداً إلكترونيّاً. **والضريبةُ تبلغ الإقرارَ منذ ض-١**،
            // ‏فالنقصُ ليس في الإفصاح بل في المستند: **توريدٌ خاضعٌ لا تعرفه الهيئةُ أبداً**
            // ‏— وهي مخالفةُ الطور الثاني بعينها، وتُكتشف عند الفحص لا قبلَه.
            //
            // ‏**والشرطُ هو شرطُ الضريبة نفسُه**: مقابلٌ موجبٌ وفئةٌ داخل النطاق. فخردةٌ
            // ‏بلا مقابلٍ لا توريدَ فيها، **ومنشأةٌ غير مسجَّلةٍ لا تُصدر فاتورةَ ضريبةٍ
            // ‏أصلاً** — وكلاهما يمرّ كما كان بلا فاتورة.
            if (saleProceeds > 0 && effectiveCategory != VatCategory.OutOfScope)
            {
                var invoiceNumber = await DocumentNumberGenerator.NextAsync(
                    db,
                    db.FixedAssets.IgnoreQueryFilters()
                        .Where(a => a.CompanyId == asset.CompanyId)
                        .Select(a => a.DisposalInvoiceNumber),
                    "FAS", disposalDate, asset.CompanyId);

                asset.RecordDisposalSale(
                    buyerCustomerId, invoiceNumber, saleProceeds, effectiveCategory, vatExemptionReasonCode);
                await db.SaveChangesAsync(token);

                // ‏**والتوليدُ من حيث تُولَّد كلُّ فاتورة** — داخل المعاملة، والإرسالُ
                // ‏الشبكيُّ بعد الـcommit. وهو عرفُ هذا المستودع منذ أوّله.
                await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
                    db, asset.CompanyId,
                    () => new ZatcaEInvoiceGenerator(db).TryGenerateForAssetDisposalAsync(asset.Id),
                    token);
            }

            await FinancialPostingSupport.AuditPostAsync(
                db, asset.CompanyId, "FixedAssetDisposal", asset.Code,
                reason: "استبعاد أصل ثابت",
                details: $"عائد: {saleProceeds:N2} — ضريبة: {vat:N2} — ربح/خسارة: {gainLoss:N2}", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                asset.CompanyId, "FixedAssetDisposal", asset.Id, asset.Code), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                asset.CompanyId, entry.Id, entry.EntryNumber!, disposalDate), token);

            return entry;
        }, ct);
    }
}
