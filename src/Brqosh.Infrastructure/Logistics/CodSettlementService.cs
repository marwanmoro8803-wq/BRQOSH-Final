using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Logistics;

/// <summary>حصيلةُ توريدٍ إلى تاجر — والمقاصّةُ مُعلَنةٌ لا مطويّة.</summary>
public record CodRemittanceResult(
    int Shipments,
    decimal CodRemitted,
    long JournalEntryId);

/// <summary>
/// (CEEF-230 · ط3-٤) <b>الدفعُ عند الاستلام — مالُ التجّار في يد الناقل.</b>
///
/// <para><b>وهذا أخطرُ عطلٍ في هذا القطاع، وأثرُه عشرون ضعفاً:</b> شركةُ توصيلٍ تُحصّل
/// مليونَ ريالٍ شهرياً نيابةً عن التجّار <b>وأجرتُها خمسون ألفاً</b>. فقيدُ المليون
/// إيراداً <b>يُظهر شركةً بحجمٍ ليس لها</b>، وتُدفع عنه زكاةٌ وضريبةٌ على مالٍ يُردّ،
/// وتُوزَّع أرباحٌ من أمانةٍ في اليد. <b>وأوّلُ شهرٍ تتأخّر فيه التوريدات ينكشف</b>.</para>
///
/// <para><b>وهويّةُ هـ-١٣:</b> رصيدُ <c>2114</c> = المحصَّلُ − المُورَّد، <b>ولا يكون
/// سالباً أبداً</b> — وسالبُه يعني توريدَ ما لم يُحصَّل، أي دفعَ الشركة من مالها.</para>
///
/// <para><b>وأجرةُ الشحن لا تُقاصّ هنا</b>: هي فاتورةٌ على التاجر في دفتر الذمم، وتُسدَّد
/// بسند قبضٍ كأيّ ذمّة. <b>والمقاصّةُ في الأستاذ تُغيّره ولا يراها الدفترُ المساعد</b> —
/// فتنكسر <c>هـ-١</c>. والصافي عرضٌ في الكشف، لا حدثٌ محاسبيٌّ ثالث.</para>
/// </summary>
public class CodSettlementService(BrqoshDbContext db)
{
    /// <summary>حسابُ ما يُحصَّل نيابةً عن التجّار.</summary>
    public const string CodPayableCode = "2114";

    /// <summary>
    /// يُثبت تحصيلَ مال الشحنات المسلَّمة: <b>النقدُ يدخل، والالتزامُ يُسجَّل معه</b>.
    /// </summary>
    public async Task<long> CollectAsync(long companyId, IEnumerable<long> shipmentIds,
        DateOnly on, string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashOnHand,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var ids = shipmentIds.Distinct().ToList();
            var shipments = await db.Shipments
                .Where(s => s.CompanyId == companyId && ids.Contains(s.Id))
                .ToListAsync(token);

            if (shipments.Count != ids.Count)
                throw new AccountingException("بعض البوالص غير موجودة.");

            var notCollected = shipments.Where(s => !s.CodCollected).ToList();
            if (notCollected.Count > 0)
                throw new AccountingException(
                    "بوالصُ لم يُحصَّل مالُها: "
                    + string.Join("، ", notCollected.Select(s => s.WaybillNumber))
                    + " — والتسليمُ هو ما يُحصّل، فسلِّمْها أوّلاً.");

            // ‏(جاهزيّةُ الإطلاق · ح-١٢) ‏**وبوليصةٌ أُثبت تحصيلُها لا تُثبَت ثانية.**
            //
            // ‏لم يكن ثمّة وسمٌ يقول «قيدُ التحصيل وقع» — `CodCollected` يُضبط **بالتسليم**
            // ‏لا بالقيد. **فاستدعاءٌ ثانٍ بالبوالص نفسِها يُقيّد مرّةً أخرى**: نقدٌ وهميٌّ
            // ‏والتزامٌ مضاعَفٌ في `2114`، **والقيدان متوازنان فلا يشكو الميزان**.
            // ‏ويقع بضغطةٍ مكرّرةٍ أو بكشفٍ يُعاد تحصيلُه في آخر اليوم.
            var alreadyPosted = shipments.Where(s => s.CodCollectionPosted).ToList();
            if (alreadyPosted.Count > 0)
                throw new AccountingException(
                    "بوالصُ أُثبت تحصيلُها سلفاً: "
                    + string.Join("، ", alreadyPosted.Select(s => s.WaybillNumber))
                    + " — ولا يُقيَّد التحصيلُ مرّتين.");

            var total = shipments.Sum(s => s.CodAmount);
            if (total <= 0m)
                throw new AccountingException("لا مبلغَ تحصيلٍ على هذه البوالص.");

            var entryId = await PostAsync(companyId, on, cashAccountCode, total,
                debitCash: true,
                memo: "تحصيل نيابةً عن التجّار",
                sourceDocument: $"CodCollect:{on:yyyyMMdd}",
                auditDetails: $"{shipments.Count} بوليصة · {total:N2}", ct: token);

            // ‏**والوسمُ داخلَ المعاملة نفسِها** — فقيدٌ يقع ووسمٌ لا يُحفظ يُعيد الثقبَ من بابه.
            foreach (var shipment in shipments)
                shipment.MarkCodCollectionPosted();

            await db.SaveChangesAsync(token);
            return entryId;
        }, ct);
    }

    /// <summary>
    /// يُورّد ما حُصّل لتاجرٍ بعينه — <b>ويُغلق التزامَه بمقداره</b>.
    ///
    /// <para><b>ولا يُورَّد ما لم يُحصَّل</b>: توريدُه يُخرج الالتزامَ مديناً — أصلٌ موهومٌ
    /// لا يقابله حقّ، <b>والشركةُ دفعت من مالها وهي تظنّها أمانة</b>.</para>
    /// </summary>
    public async Task<CodRemittanceResult> RemitAsync(long companyId, long shipperCustomerId,
        DateOnly on, string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashAtBanks,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            // ‏(جاهزيّةُ الإطلاق · ح-١٢) ‏**والمستحقُّ هو ما قُيِّد تحصيلُه، لا ما سُلِّم.**
            //
            // ‏كان الترشيحُ على `CodCollected` — وهو يُضبط **بالتسليم بلا قيد**. فبوليصةٌ
            // ‏سُلّمت ولم يُقيَّد تحصيلُها بعدُ كانت تدخل التوريد، **فيُمدَّن `2114` بما لم
            // ‏يُدَن به قطّ ويخرج الالتزامُ سالباً** — أي أنّ الشركة دفعت من مالها وهي
            // ‏تحسبها أمانة. وهو كسرُ هـ-١٣ المكتوبة في صدر هذا الملفّ.
            var due = await db.Shipments
                .Where(s => s.CompanyId == companyId
                            && s.ShipperCustomerId == shipperCustomerId
                            && s.CodCollectionPosted && !s.CodRemitted)
                .ToListAsync(token);

            if (due.Count == 0)
                throw new AccountingException("لا مبالغَ محصَّلةً غيرَ مورَّدةٍ لهذا التاجر.");

            var total = due.Sum(s => s.CodAmount);
            foreach (var shipment in due) shipment.MarkCodRemitted();

            var entryId = await PostAsync(companyId, on, cashAccountCode, total,
                debitCash: false,
                memo: "توريد تحصيلٍ إلى التاجر",
                sourceDocument: $"CodRemit:{shipperCustomerId}:{on:yyyyMMdd}",
                auditDetails: $"{due.Count} بوليصة · {total:N2}", ct: token);

            await db.SaveChangesAsync(token);
            return new CodRemittanceResult(due.Count, total, entryId);
        }, ct);
    }

    /// <summary>ما في اليد لتاجرٍ — محصَّلٌ لم يُورَّد بعد.</summary>
    public async Task<decimal> HeldForShipperAsync(long companyId, long shipperCustomerId,
        CancellationToken ct = default) =>
        await db.Shipments
            .Where(s => s.CompanyId == companyId && s.ShipperCustomerId == shipperCustomerId
                        && s.CodCollected && !s.CodRemitted)
            .SumAsync(s => s.CodAmount, ct);

    private async Task<long> PostAsync(long companyId, DateOnly on, string cashAccountCode,
        decimal amount, bool debitCash, string memo, string sourceDocument, string auditDetails,
        CancellationToken ct)
    {
        var cash = await SystemAccountProvisioning.EnsureAsync(db, companyId, cashAccountCode, ct);
        var payable = await SystemAccountProvisioning.EnsureAsync(db, companyId, CodPayableCode, ct);

        await FinancialPostingSupport.ValidateAsync(
            db, companyId, on, PostingDocumentKind.JournalEntry,
            accountIds: [cash.Id, payable.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, on, memo, sourceDocument: sourceDocument);

        if (debitCash)
        {
            entry.AddLine(cash, amount, 0m, "نقدٌ محصَّلٌ عند الاستلام");
            entry.AddLine(payable, 0m, amount, "التزامٌ بتوريده إلى التاجر");
        }
        else
        {
            entry.AddLine(payable, amount, 0m, "إغلاق التزام التحصيل");
            entry.AddLine(cash, 0m, amount, "توريدٌ إلى التاجر");
        }

        var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
        entry.Post(number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        await FinancialPostingSupport.AuditPostAsync(
            db, companyId, debitCash ? "CodCollected" : "CodRemitted", $"{on:yyyy-MM-dd}",
            reason: memo, details: auditDetails, ct: ct);

        return entry.Id;
    }
}
