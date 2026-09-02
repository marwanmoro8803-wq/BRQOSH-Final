using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Healthcare;

/// <summary>حصيلةُ فوترةِ زيارة — والقسمةُ مُعلَنةٌ لا مطويّة.</summary>
public record VisitBillingResult(
    long VisitId,
    decimal Total,
    decimal PatientShare,
    decimal PayerShare,
    CoverageBasis Basis,
    long? PatientInvoiceId,
    long? PayerInvoiceId);

/// <summary>
/// (CEEF-230 · ط3-٢) <b>فوترةُ الزيارة — فاتورةٌ للمريض وأخرى لشركة التأمين.</b>
///
/// <para><b>ولماذا فاتورتان لا قيدُ تحويل:</b> الدفترُ المساعد للعملاء في هذا النظام
/// <b>يُبنى من المستندات لا من قيود الأستاذ</b> — فاتورةٌ وإشعارٌ وسندُ قبضٍ وشيك. فقيدٌ
/// يدويّ ينقل الذمّةَ من المريض إلى الشركة <b>يُغيّر الأستاذَ ولا يراه الدفترُ المساعد</b>،
/// فتنكسر هويّةُ <c>هـ-١</c> وتخرج أعمارُ الديون بأرقامٍ لا تُفسَّر. <b>وفاتورةٌ لكلّ ذمّةٍ
/// تجعل كلَّ شيءٍ يعمل بلا استثناء</b>: التحصيلُ والأعمار وكشفُ الحساب والفوترة الإلكترونية.</para>
///
/// <para><b>والمجموعُ محفوظ</b>: فاتورةُ المريض + فاتورةُ الشركة = إجماليُّ الزيارة، هللةً
/// بهللة. وهي هويّةُ <c>هـ-١٠</c>.</para>
/// </summary>
public class VisitBillingService(BrqoshDbContext db)
{
    /// <summary>
    /// يقسم الزيارةَ ويُصدر فاتورتَها (أو فاتورتيها).
    ///
    /// <para><b>وكلُّه في معاملةٍ واحدة</b>: فاتورةُ مريضٍ تُرحَّل ثمّ تعثر مطالبةُ الشركة
    /// تترك <b>ثمانين بالمئة من إيراد الزيارة بلا مستند</b>، والزيارةُ مفتوحةٌ تُفوتَر
    /// ثانيةً فتُنشئ للمريض فاتورتين.</para>
    /// </summary>
    public async Task<VisitBillingResult> BillAsync(long companyId, long visitId,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        var result = await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var visit = await db.Visits
                .SingleOrDefaultAsync(v => v.Id == visitId && v.CompanyId == companyId, token)
                ?? throw new AccountingException("الزيارة غير موجودة.");

            if (visit.Status != VisitStatus.Open)
                throw new AccountingException("لا تُفوتَر إلا زيارةٌ مفتوحة — ولا تُفوتَر مرّتين.");
            if (visit.Lines.Count == 0)
                throw new AccountingException("الزيارة بلا خدمات.");

            var patient = await db.Patients
                .SingleOrDefaultAsync(p => p.Id == visit.PatientId, token)
                ?? throw new AccountingException("المريض غير موجود.");

            var (policy, payer) = await ResolveCoverageAsync(visit, token);

            var remaining = policy is null
                ? null
                : await RemainingLimitAsync(policy, visit.VisitDate, token);

            var split = CoverageCalculator.Split(
                coveredTotal: policy is null ? 0m : visit.CoveredTotal,
                uncoveredTotal: policy is null ? visit.Total : visit.UncoveredTotal,
                coveragePercent: policy?.CoveragePercent ?? 0m,
                copayFixed: policy?.CopayFixed ?? 0m,
                remainingLimit: remaining);

            long? payerInvoiceId = null;
            if (split.PayerShare > 0m && payer is not null)
            {
                var covered = GroupByCategory(visit, coveredOnly: true);
                payerInvoiceId = await IssueAsync(companyId, payer.CustomerId, visit.VisitDate,
                    CoverageCalculator.Allocate(covered, split.PayerShare),
                    $"مطالبة تأمين — زيارة {visit.VisitNumber}", token);
            }

            long? patientInvoiceId = null;
            if (split.PatientShare > 0m)
            {
                // ‏**وحصّةُ المريض ما بقي بعد نصيب الشركة، فئةً بفئة** — لا حسابٌ ثانٍ:
                // ‏الطرحُ يضمن أنّ الفاتورتين تُطبقان الزيارةَ تماماً، **وأنّ كلَّ فئةٍ
                // ‏ضريبيةٍ تُطبق نفسَها** فلا يُخضع الدواءُ الصفريُّ للضريبة.
                var all = GroupByCategory(visit, coveredOnly: false);
                var claimed = split.PayerShare > 0m && payer is not null
                    ? CoverageCalculator.Allocate(GroupByCategory(visit, coveredOnly: true), split.PayerShare)
                    : [];

                var patientGroups = all
                    .Select(g => (g.Group,
                        Amount: g.Amount - claimed.Where(c => c.Group == g.Group).Sum(c => c.Amount)))
                    .Where(g => g.Amount != 0m)
                    .ToList();

                patientInvoiceId = await IssueAsync(companyId, patient.CustomerId, visit.VisitDate,
                    patientGroups, $"حصّة المريض — زيارة {visit.VisitNumber}", token);
            }

            // (CEEF-231 · ط3-٢ج) ‏**وما صُرف من الصيدلية يخرج من المستودع هنا.**
            //
            // ‏والموضعُ قرارٌ: **الزيارةُ لا الفاتورة**. فالمبلغُ يُقسَم على فاتورتين
            // ‏بنسبة التغطية **والكمّيةُ لا تُقسَم** — فصرفٌ من كلٍّ منهما يُخرج الدواءَ
            // ‏مرّتين، **ووثيقةٌ تُغطّي كلَّ شيءٍ لا تُنشئ فاتورةَ مريضٍ أصلاً** فيضيع
            // ‏الصرفُ كلُّه لو عُلِّق بها.
            //
            // ‏**وداخل المعاملة**: صرفٌ بعدها يترك زيارةً مفوترةً ودواءً لم يخرج إن تعثّر.
            await new PharmacyDispenseService(db).DispenseForVisitAsync(
                companyId, visit, $"Visit:{visit.VisitNumber}", token);

            visit.MarkBilled(patientInvoiceId, payerInvoiceId);
            await db.SaveChangesAsync(token);

            return new VisitBillingResult(visit.Id, visit.Total, split.PatientShare,
                split.PayerShare, split.Basis, patientInvoiceId, payerInvoiceId);
        }, ct);

        // (CEEF-230 · ط6-٧) ‏**والمستندُ وُلِّد داخل المعاملة، والإبلاغُ من هنا** — بعد
        // ‏الـcommit وحدَه، فالنداءُ الشبكيُّ لا يُحبس معاملةً مفتوحة.
        //
        // ‏**والفاتورتان تُبلَّغان كلتاهما**: حصّةُ المريض ومطالبةُ الشركة **فاتورتا ضريبةٍ
        // ‏لا واحدة** — وإبلاغُ إحداهما يترك الأخرى ديناً معلوماً للهيئة بلا مستند.
        foreach (var invoiceId in new[] { result.PatientInvoiceId, result.PayerInvoiceId })
        {
            if (invoiceId is long id)
                await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
                    db, companyId, ZatcaDocumentType.SalesInvoice, id, ct);
        }

        return result;
    }

    /// <summary>
    /// يُحضر الوثيقةَ والشركةَ — <b>ووثيقةٌ منتهيةٌ تُهمَل ولا تُرفَض</b>.
    ///
    /// <para>فالمريضُ عُولج فعلاً، ورفضُ الفوترة يترك الخدمةَ بلا إيراد. <b>والصوابُ أن
    /// تصير الزيارةُ على الحساب الخاصّ</b> — يراها المحاسبُ في ذمّة المريض ويُراجعها.</para>
    /// </summary>
    private async Task<(InsurancePolicy?, InsurancePayer?)> ResolveCoverageAsync(
        Visit visit, CancellationToken ct)
    {
        if (visit.PolicyId is not long policyId) return (null, null);

        var policy = await db.InsurancePolicies
            .SingleOrDefaultAsync(p => p.Id == policyId && p.CompanyId == visit.CompanyId, ct);

        if (policy is null || !policy.IsValidOn(visit.VisitDate)) return (null, null);

        // ‏**ووثيقةُ مريضٍ آخر لا تُغطّي هذا** — والخطأُ يقع حين يُنسخ رقمُ وثيقةٍ من
        // ‏زيارةٍ سابقة. وقبولُها يُطالِب الشركةَ باسمِ عضوٍ لم يُعالَج.
        if (policy.PatientId != visit.PatientId)
            throw new AccountingException("الوثيقة ليست لهذا المريض.");

        var payer = await db.InsurancePayers
            .SingleOrDefaultAsync(p => p.Id == policy.PayerId && p.IsActive, ct);

        return payer is null ? (null, null) : (policy, payer);
    }

    /// <summary>
    /// ما بقي من سقف <b>سنة الوثيقة</b> — لا السنةِ الميلادية.
    ///
    /// <para>ويُحسب المستهلَكُ من <b>فواتير الشركة المرحّلة</b> عن زياراتِ هذا المريض في
    /// تلك السنة. <b>وعدّادٌ مخزَّنٌ في الوثيقة كان سيَنحرف</b> عند أوّل إشعارِ دائنٍ أو
    /// فاتورةٍ تُلغى، والاشتقاقُ من المستندات لا ينحرف.</para>
    /// </summary>
    private async Task<decimal?> RemainingLimitAsync(InsurancePolicy policy, DateOnly on,
        CancellationToken ct)
    {
        if (policy.AnnualLimit is not decimal limit) return null;

        var yearStart = policy.PolicyYearStart(on);
        var yearEnd = yearStart.AddYears(1);

        var billedInvoiceIds = await db.Visits
            .Where(v => v.CompanyId == policy.CompanyId
                        && v.PolicyId == policy.Id
                        && v.Status == VisitStatus.Billed
                        && v.VisitDate >= yearStart && v.VisitDate < yearEnd
                        && v.PayerInvoiceId != null)
            .Select(v => v.PayerInvoiceId!.Value)
            .ToListAsync(ct);

        var consumed = billedInvoiceIds.Count == 0
            ? 0m
            : await db.SalesInvoices
                .Where(i => billedInvoiceIds.Contains(i.Id) && i.Status == SalesInvoiceStatus.Posted)
                .SumAsync(i => i.Subtotal, ct);

        return limit - consumed;
    }

    /// <summary>
    /// يجمع سطورَ الزيارة <b>بمعالجتها الضريبية الكاملة</b> — الفئةُ وسببُ الإعفاء معاً.
    ///
    /// <para><b>والسببُ جزءٌ من المفتاح لا زينة</b>: دواءان صفريّان بسببين مختلفين لا
    /// يُجمعان، <b>وإلا خرج المستندُ الإلكترونيُّ بسببٍ لا يخصّ نصفَ مبلغه</b>.</para>
    /// </summary>
    private static List<(TaxGroup Group, decimal Amount)> GroupByCategory(
        Visit visit, bool coveredOnly) =>
        [.. visit.Lines
            .Where(l => !coveredOnly || l.IsCovered)
            .GroupBy(l => new TaxGroup(l.VatCategory, l.TaxExemptionReasonCode, l.TaxExemptionReason))
            .OrderBy(g => g.Key.Category).ThenBy(g => g.Key.ExemptionReasonCode)
            .Select(g => (Group: g.Key, Amount: Math.Round(g.Sum(l => l.LineTotal), 2,
                MidpointRounding.AwayFromZero)))];

    private async Task<long> IssueAsync(long companyId, long customerId, DateOnly date,
        IReadOnlyList<(TaxGroup Group, decimal Amount)> groups, string description,
        CancellationToken ct)
    {
        if (groups.Count == 0)
            throw new AccountingException($"لا سطورَ لفاتورة «{description}».");

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, date);
        foreach (var (group, amount) in groups)
            invoice.AddLine(description, 1m, amount, vatCategory: group.Category,
                taxExemptionReasonCode: group.ExemptionReasonCode,
                taxExemptionReason: group.ExemptionReason);

        await new SalesInvoicePostingService(db).PostCoreAsync(invoice, ct);
        return invoice.Id;
    }
}
