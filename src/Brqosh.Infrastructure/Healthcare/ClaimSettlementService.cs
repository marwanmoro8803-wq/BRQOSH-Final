using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Healthcare;

/// <summary>حصيلةُ معالجة المرفوض — والمستنداتُ مُعلَنةٌ لا مطويّة.</summary>
public record ClaimSettlementResult(
    long ClaimId,
    decimal ApprovedTotal,
    decimal RejectedTotal,
    decimal RebilledToPatients,
    decimal WrittenOff,
    IReadOnlyList<long> CreditNoteIds,
    IReadOnlyList<long> RebillInvoiceIds);

/// <summary>
/// <b>يُخرج المرفوضَ من ذمّة الشركة — بمستندٍ لا بقيدٍ يدويّ.</b>
///
/// <para><b>والدفترُ المساعد يُبنى من المستندات لا من القيود</b> — وهو قرارُ هذا المستودع
/// منذ <c>ط1-٤</c>. فقيدٌ يدويٌّ يُنقص حساب <c>1104</c> <b>يُصلح الميزانيةَ ويترك كشفَ
/// حساب الشركة كما كان</b>: الرصيدُ في الأعلى يخالف مجموعَ السطور تحته، <b>ويقضي المحاسبُ
/// يومَه يبحث عن فرقٍ سببُه النظام</b>.</para>
///
/// <para><b>والإشعارُ الدائن هو الصواب نظاماً أيضاً</b>: فاتورةٌ ضريبيةٌ صدرت للشركة،
/// <b>وإنقاصُها بلا إشعارٍ يترك ضريبةَ المخرجات مُعلَنةً بأكثر ممّا وجب</b> — ويخرج إلى
/// الهيئة بمستندٍ إلكترونيٍّ صحيح لأنّ مسارَ الإشعارات مبنيٌّ سلفاً.</para>
///
/// <para><b>والمصيران يفترقان في مستندٍ واحد</b>: كلاهما يُصدر الإشعارَ الدائن، <b>وإعادةُ
/// التحميل وحدَها تُتبعه فاتورةً على المريض</b>. فالشطبُ إنقاصُ إيرادٍ صريح، وإعادةُ
/// التحميل نقلُ مدينٍ لا إنقاصُ إيراد.</para>
/// </summary>
public class ClaimSettlementService(BrqoshDbContext db)
{
    /// <summary>
    /// يُنشئ دفعةَ مطالبةٍ من زياراتٍ فُوترت على شركةٍ ولم تُطالَب بعد.
    /// </summary>
    /// <remarks>
    /// <b>والاشتقاقُ من المستندات لا من إدخالٍ يدويّ</b>: المبلغُ المطالَبُ به هو إجمالي
    /// فاتورة المطالبة نفسِها. <b>ورقمٌ يُكتب بيدٍ يُخالفها يوماً</b> — فتُطالَب الشركةُ
    /// بغير ما في دفترنا، ولا يُطابَق أبداً.
    /// </remarks>
    public async Task<InsuranceClaim> BuildAsync(long companyId, long payerId, string claimNumber,
        DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default)
    {
        var payer = await db.InsurancePayers
            .SingleOrDefaultAsync(p => p.Id == payerId && p.CompanyId == companyId, ct)
            ?? throw new AccountingException("شركة التأمين غير موجودة.");

        var claim = InsuranceClaim.Create(companyId, payerId, claimNumber, periodStart, periodEnd);

        var visits = await db.Visits
            .Where(v => v.CompanyId == companyId
                        && v.Status == VisitStatus.Billed
                        && v.PayerInvoiceId != null
                        && v.VisitDate >= periodStart && v.VisitDate <= periodEnd)
            .Select(v => new { v.Id, InvoiceId = v.PayerInvoiceId!.Value })
            .ToListAsync(ct);

        if (visits.Count == 0) return claim;

        var invoiceIds = visits.Select(v => v.InvoiceId).ToList();

        // ‏**وفاتورةُ الشركة تُميَّز بعميلها** — فزياراتُ المدة تشمل كلَّ الشركات، وضمُّ
        // ‏فاتورةِ شركةٍ إلى دفعةِ أخرى يُطالِب بها مَن لا يعرفها.
        var invoices = await db.SalesInvoices
            .Where(i => invoiceIds.Contains(i.Id)
                        && i.CustomerId == payer.CustomerId
                        && i.Status == SalesInvoiceStatus.Posted)
            .Select(i => new { i.Id, i.Subtotal, i.VatAmount })
            .ToDictionaryAsync(i => i.Id, ct);

        // ‏**وما طُولب به سلفاً لا يُعاد** — والقيدُ في القاعدة يمنعه، والاستبعادُ هنا
        // ‏يجعل الدفعةَ تُبنى بلا رمي.
        var alreadyClaimed = await db.InsuranceClaims
            .Where(c => c.CompanyId == companyId)
            .SelectMany(c => c.Lines)
            .Select(l => l.PayerInvoiceId)
            .ToListAsync(ct);
        var claimedSet = alreadyClaimed.ToHashSet();

        foreach (var visit in visits)
        {
            if (claimedSet.Contains(visit.InvoiceId)) continue;
            if (!invoices.TryGetValue(visit.InvoiceId, out var invoice)) continue;

            claim.AddVisit(visit.Id, visit.InvoiceId,
                Math.Round(invoice.Subtotal + invoice.VatAmount, 2, MidpointRounding.AwayFromZero));
        }

        return claim;
    }

    /// <summary>
    /// يُصدر إشعارَ الدائن لكلّ مرفوض، ويُعيد تحميلَ ما قُرّر تحميلُه، ثمّ يُقفل.
    /// </summary>
    /// <remarks>
    /// <para><b>وكلُّه في معاملةٍ واحدة</b>: إشعارٌ يُرحَّل ثمّ تعثر فاتورةُ إعادة التحميل
    /// <b>يترك الشركةَ معفاةً والمريضَ غيرَ مدين</b> — والمالُ يختفي من الطرفين.</para>
    ///
    /// <para><b>وتُستأنَف بلا ازدواج</b>: السطرُ الذي صدر إشعارُه يُتخطّى، فإعادةُ التشغيل
    /// بعد انقطاعٍ لا تُصدر إشعارين لدينٍ واحد.</para>
    /// </remarks>
    public async Task<ClaimSettlementResult> SettleAsync(long companyId, long claimId, DateOnly on,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var claim = await db.InsuranceClaims
                .SingleOrDefaultAsync(c => c.Id == claimId && c.CompanyId == companyId, token)
                ?? throw new AccountingException("المطالبة غير موجودة.");

            if (claim.Status != ClaimStatus.Adjudicated)
                throw new AccountingException(
                    "لا تُعالَج إلا مطالبةٌ فُصل في كلّ سطورها — وسطرٌ بلا قرارٍ يُخرج معالجةً ناقصة.");

            var payer = await db.InsurancePayers
                .SingleAsync(p => p.Id == claim.PayerId, token);

            var creditNoteIds = new List<long>();
            var rebillIds = new List<long>();
            var rebilled = 0m;
            var writtenOff = 0m;

            foreach (var line in claim.Lines.Where(l => l.RejectedAmount > 0m))
            {
                if (line.CreditNoteId is not null)
                {
                    if (line.Disposition == RejectionDisposition.RebillPatient) rebilled += line.RejectedAmount;
                    else writtenOff += line.RejectedAmount;
                    continue;
                }

                var groups = await RejectedGroupsAsync(line.PayerInvoiceId, line.RejectedAmount, token);

                var note = CreditNote.CreateDraft(companyId, payer.CustomerId, line.PayerInvoiceId,
                    on, $"رفضُ مطالبة — {line.RejectionReason}");
                foreach (var (group, amount) in groups)
                    note.AddLine($"مرفوضٌ من مطالبة {claim.ClaimNumber}", 1m, amount,
                        vatCategory: group.Category,
                        taxExemptionReasonCode: group.ExemptionReasonCode,
                        taxExemptionReason: group.ExemptionReason);

                await new CreditNotePostingService(db).PostCoreAsync(note, token);
                claim.LinkCreditNote(line.PayerInvoiceId, note.Id);
                creditNoteIds.Add(note.Id);

                if (line.Disposition == RejectionDisposition.RebillPatient)
                {
                    var patientCustomerId = await PatientCustomerAsync(companyId, line.VisitId, token);

                    var invoice = SalesInvoice.CreateDraft(companyId, patientCustomerId, on);
                    foreach (var (group, amount) in groups)
                        invoice.AddLine(
                            $"إعادةُ تحميلٍ بعد رفض التأمين — مطالبة {claim.ClaimNumber}",
                            1m, amount, vatCategory: group.Category,
                            taxExemptionReasonCode: group.ExemptionReasonCode,
                            taxExemptionReason: group.ExemptionReason);

                    await new SalesInvoicePostingService(db).PostCoreAsync(invoice, token);
                    claim.LinkRebillInvoice(line.PayerInvoiceId, invoice.Id);
                    rebillIds.Add(invoice.Id);
                    rebilled += line.RejectedAmount;
                }
                else
                {
                    writtenOff += line.RejectedAmount;
                }
            }

            claim.Close();
            await db.SaveChangesAsync(token);

            return new ClaimSettlementResult(claim.Id, claim.ApprovedTotal, claim.RejectedTotal,
                Math.Round(rebilled, 2, MidpointRounding.AwayFromZero),
                Math.Round(writtenOff, 2, MidpointRounding.AwayFromZero),
                creditNoteIds, rebillIds);
        }, ct);
    }

    /// <summary>
    /// يوزّع المرفوضَ على المعالجات الضريبية <b>لفاتورة المطالبة نفسِها</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>ونسبةُ الضريبة تُشتقّ من الأصل لا تُفترض</b>: عيادةٌ تبيع خدمةً صفريةَ
    /// النسبة وإجراءً تجميلياً أساسياً في زيارةٍ واحدة. <b>وإشعارٌ يعكس المبلغَ كلَّه
    /// بالنسبة الأساسية يعكس ضريبةً لم تُحصَّل</b> — فتصير الهيئةُ مدينةً لنا بما لم ندفعه.</para>
    ///
    /// <para><b>والتوزيعُ بالإجمالي لا بالصافي</b>: المرفوضُ هو ما قالت الشركةُ إنّها لا
    /// تدفعه <b>من الذمّة التي عليها</b>، والذمّةُ إجماليٌّ بالضريبة. فيُوزَّع الإجماليُّ
    /// على الأوعية، <b>ثمّ يُشتقّ صافي كلِّ وعاءٍ ممّا يُنتج إجماليَّه بالضبط</b>.</para>
    ///
    /// <para><b>ولمَ بحثٌ لا قسمة</b>: الضريبةُ تُقرَّب لكلّ وعاء، <b>والقسمةُ على
    /// <c>١٫١٥</c> ثمّ التقريب تُنتج إجمالياً يفترق بفلس</b> — والفلسُ يبقى في ذمّة
    /// الشركة وقد رفضته، <b>وهو عينُ الذمّة الوهمية التي بُني هذا الباب لإغلاقها</b>.
    /// فالبحثُ في جوار القسمة يجد الصافيَ الذي يُطبق الإجماليَّ تماماً، <b>وإن لم يوجد
    /// رُفض بصوتٍ ولم يُبتلع</b>.</para>
    /// </remarks>
    private async Task<IReadOnlyList<(TaxGroup Group, decimal Amount)>> RejectedGroupsAsync(
        long payerInvoiceId, decimal rejected, CancellationToken ct)
    {
        var invoice = await db.SalesInvoices
            .SingleOrDefaultAsync(i => i.Id == payerInvoiceId, ct)
            ?? throw new AccountingException("فاتورةُ المطالبة غير موجودة.");

        // ‏إجماليُّ كلِّ وعاءٍ: صافيه وضريبتُه — والضريبةُ تُقرَّب للوعاء كما يقرّبها المستند.
        var grossGroups = invoice.Lines
            .GroupBy(l => new TaxGroup(l.VatCategory, l.TaxExemptionReasonCode, l.TaxExemptionReason))
            .OrderBy(g => g.Key.Category).ThenBy(g => g.Key.ExemptionReasonCode)
            .Select(g =>
            {
                var net = Math.Round(g.Sum(l => l.Quantity * l.UnitPrice), 2,
                    MidpointRounding.AwayFromZero);
                var rate = g.Key.Category.RateFor(SalesInvoice.SaudiVatRate);
                var vat = Math.Round(net * rate, 2, MidpointRounding.AwayFromZero);
                return (Group: g.Key, Amount: net + vat);
            })
            .ToList();

        var allocated = CoverageCalculator.Allocate(grossGroups, rejected);
        if (allocated.Count == 0)
            throw new AccountingException("المرفوضُ لا يُنتج مبلغاً — ولا إشعارَ بصفر.");

        return [.. allocated.Select(a => (a.Group,
            Amount: NetForGross(a.Amount, a.Group.Category.RateFor(SalesInvoice.SaudiVatRate))))];
    }

    /// <summary>
    /// الصافي الذي ضريبتُه المقرَّبةُ تُبلغه الإجماليَّ المطلوب — <b>أو رفضٌ بصوت</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>والتحقّقُ هو الحارس، لا القسمة</b>: ليس كلُّ مبلغٍ إجماليٍّ يُمثَّل بصافٍ
    /// وضريبةٍ مقرَّبة — <c>345.04</c> مثلاً لا صافيَ له عند <c>١٥٪</c>. <b>وقبولُه يُصدر
    /// إشعاراً بغير ما رفضته الشركة</b>، فيبقى الفرقُ في ذمّتها إلى الأبد: <b>وهو عينُ
    /// الذمّة الوهمية التي بُني هذا الباب لإغلاقها، بمقدارٍ أصغر لا بطبيعةٍ أخرى</b>.</para>
    ///
    /// <para><b>وبحثٌ في جوار القسمة كان هنا فحُذف</b>: طفرةٌ نجت فأخرجت أنّه <b>شيفرةٌ
    /// ميّتة</b> — ومسحٌ شاملٌ على مئتَي ألف قيمةٍ أثبت أنّ كلَّ إجماليٍّ <b>مُمثَّلٍ</b>
    /// عند <c>١٥٪</c> تجده القسمةُ المقرَّبة، وما لا تجده <b>لا صافيَ له أصلاً</b>. فالبحثُ
    /// لم يكن يُنقذ حالةً واحدة — <b>وشيفرةٌ لا تعمل تُوهم قارئَها بأنّ حالةً عولجت</b>.</para>
    /// </remarks>
    private static decimal NetForGross(decimal gross, decimal rate)
    {
        if (rate == 0m) return gross;

        var net = Math.Round(gross / (1m + rate), 2, MidpointRounding.AwayFromZero);
        if (net + Math.Round(net * rate, 2, MidpointRounding.AwayFromZero) == gross)
            return net;

        throw new AccountingException(
            $"المبلغ المرفوض {gross:N2} لا يُمثَّل بصافٍ وضريبةٍ عند النسبة {rate:P0} — "
            + "عدّلْه بفلسٍ ليُطابق ما ترفضه الشركة.");
    }

    private async Task<long> PatientCustomerAsync(long companyId, long visitId, CancellationToken ct)
    {
        var patientId = await db.Visits
            .Where(v => v.Id == visitId && v.CompanyId == companyId)
            .Select(v => v.PatientId)
            .SingleAsync(ct);

        return await db.Patients
            .Where(p => p.Id == patientId)
            .Select(p => p.CustomerId)
            .SingleAsync(ct);
    }
}
