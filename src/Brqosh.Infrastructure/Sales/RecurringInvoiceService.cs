using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>حصيلةُ تشغيلةٍ واحدة — وما تعذّر يُسمّى ولا يُبتلع.</summary>
public record RecurringGenerationResult(
    int InvoicesCreated,
    int PlansAdvanced,
    IReadOnlyList<string> Refusals);

/// <summary>
/// (CEEF-230 · ط1-٥) <b>يُولّد فواتيرَ الخطط المستحقّة حتى تاريخٍ معلوم.</b>
///
/// <para><b>دفعةٌ يُطلقها المستخدم لا خدمةٌ خلفية</b> — ولا مُجدوِلَ عملٍ في هذا التطبيق
/// إطلاقاً. وهو أصدقُ لتطبيقٍ مكتبيّ: خدمةٌ خلفيةٌ في تطبيقٍ مغلقٍ لا تعمل، وخطةٌ تعتمد
/// عليها تتوقّف بصمتٍ شهراً كاملاً. والمثالُ القائم <c>DepreciationService.RunMonthlyAsync</c>.</para>
///
/// <para><b>و<paramref name="asOf"/> وسيطٌ لا <c>DateTime.Today</c></b>: تشغيلةٌ تقرأ
/// ساعةَ الجهاز لا تُعاد بنتيجتها، ولا تُختبر إلا بتغيير الساعة. <b>وأداةٌ لا تُعاد
/// نتيجتُها لا تُصدَّق</b>.</para>
/// </summary>
public class RecurringInvoiceService(BrqoshDbContext db)
{
    /// <summary>
    /// يُولّد كلَّ ما استحقّ حتى <paramref name="asOf"/>.
    ///
    /// <para><b>وكلَّ المستحقّات لا آخرَها</b>: مَن أغلق البرنامج ثلاثة أشهر له ثلاثُ
    /// فواتير. <b>وفاتورةٌ واحدةٌ تُسقط إيرادَ شهرين بلا أثر</b> — والدفاترُ تخرج ناقصةً
    /// بلا ما يدلّ على النقص.</para>
    ///
    /// <para><b>وكلُّ خطةٍ في معاملتها</b>: سقوطُ واحدةٍ (عميلٌ محذوف، فترةٌ مقفلة) لا
    /// يُلغي ما وُلِّد لغيرها — <b>ومعاملةٌ واحدةٌ للدفعة كلِّها تجعل عطلَ خطةٍ يمحو عملَ
    /// عشرين</b>. وما سقط يُسمّى في الحصيلة.</para>
    /// </summary>
    public async Task<RecurringGenerationResult> GenerateDueAsync(
        long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        // ‏**حارسان للإيقاف، وأيُّهما وحده يكفي — وقِيس ذلك.**
        //
        // ‏نزعُ هذا الترشيح وحده لا يُغيّر نتيجةً، ونزعُ فحصِ الحالة في `NextOccurrence()`
        // ‏وحدَه كذلك. **ونزعُهما معاً يُسقط الاختبار** — فالسلوكُ مُثبَت، ولا يُدّعى أنّ
        // ‏أحدهما بعينه هو العامل.
        //
        // ‏والاثنان مقصودان: هذا يمنع **تحميلَ** خططٍ لا عمل لها (أداء)، وذاك يمنع
        // ‏**توليدَها** أيّاً كان مَن استدعاه (صحّة) — فالنطاقُ لا يثق بمُرشِّح مستدعيه.
        var plans = await db.RecurringInvoicePlans
            .Where(p => p.CompanyId == companyId && p.Status == RecurringPlanStatus.Active)
            .ToListAsync(ct);

        var created = 0;
        var advanced = 0;
        var refusals = new List<string>();

        // (CEEF-230 · ط6-٧) ‏**ما وُلِّد يُجمع ليُبلَّغ بعد التشغيلة كلِّها** — لا داخل
        // ‏معاملة الخطة: النداءُ الشبكيُّ لا يجري إلّا بعد الـcommit، **ولا داخل الـ`try`
        // ‏الذي يُسمّي المتعذِّر**: تعثُّرُ إبلاغٍ ليس تعذُّرَ توليد، وخلطُهما يُخبر
        // ‏المستخدمَ أنّ فاتورةً لم تُولَّد وهي في الدفاتر.
        var issued = new List<long>();

        foreach (var plan in plans)
        {
            var madeForThisPlan = 0;

            // ‏سقفٌ صلبٌ على التكرارات في التشغيلة الواحدة — **حارسُ حلقةٍ لا سياسةُ عمل**.
            // ‏فخطةٌ أسبوعيةٌ بتاريخِ بدايةٍ قديمٍ خطأً قد تُولّد آلافاً، وحلقةٌ بلا سقفٍ
            // ‏تُعلّق البرنامج. والسقفُ يُبلَّغ في الحصيلة فلا يمرّ صمتاً.
            const int MaxPerRun = 240;

            while (madeForThisPlan < MaxPerRun)
            {
                var due = plan.NextOccurrence();
                if (due is not DateOnly occurrence || occurrence > asOf) break;

                try
                {
                    issued.Add(await GenerateOneAsync(plan, occurrence, ct));
                    created++;
                    madeForThisPlan++;
                }
                catch (Exception ex)
                {
                    refusals.Add($"«{plan.Code}» — {occurrence:yyyy-MM-dd}: {FirstLine(ex)}");
                    break;   // ‏لا تُحاوَل التكراراتُ التالية لخطةٍ عثرت: الترتيبُ معنى.
                }
            }

            if (madeForThisPlan > 0) advanced++;
            if (madeForThisPlan == MaxPerRun)
                refusals.Add($"«{plan.Code}» بلغت سقفَ {MaxPerRun} تكراراً في التشغيلة — راجعْ تاريخَ بدايتها.");
        }

        foreach (var invoiceId in issued)
            await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
                db, companyId, ZatcaDocumentType.SalesInvoice, invoiceId, ct);

        return new RecurringGenerationResult(created, advanced, refusals);
    }

    /// <summary>
    /// يُولّد فاتورةَ تكرارٍ واحد ويُثبّت أثرَه — <b>في معاملةٍ واحدة</b>.
    ///
    /// <para><b>والأثرُ يُكتب قبل تقدُّم العدّاد وبعد الترحيل</b>: فقيدُ التفرّد على
    /// <c>(PlanId, OccurrenceDate)</c> هو ما يمنع الازدواجَ فعلاً — لا فحصٌ في الذاكرة
    /// يسبقه تشغيلٌ آخر. <b>وفوترةُ العميل مرّتين عن شهرٍ واحد عطلٌ يراه العميلُ قبلنا.</b></para>
    /// </summary>
    private async Task<long> GenerateOneAsync(
        RecurringInvoicePlan plan, DateOnly occurrence, CancellationToken ct) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            if (plan.Lines.Count == 0)
                throw new AccountingException("الخطة بلا سطور.");

            var invoice = SalesInvoice.CreateDraft(plan.CompanyId, plan.CustomerId, occurrence);
            foreach (var line in plan.Lines)
                invoice.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                    line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason,
                    discountPercent: line.DiscountPercent);

            await new SalesInvoicePostingService(db).PostCoreAsync(invoice, token);

            db.RecurringInvoiceRuns.Add(RecurringInvoiceRun.Create(
                plan.CompanyId, plan.Id, occurrence, invoice.Id));

            plan.MarkGenerated();
            await db.SaveChangesAsync(token);

            return invoice.Id;
        }, ct);

    private static string FirstLine(Exception ex)
    {
        var deepest = ex;
        while (deepest.InnerException is not null) deepest = deepest.InnerException;
        var i = deepest.Message.IndexOf('\n');
        return (i < 0 ? deepest.Message : deepest.Message[..i]).TrimEnd('\r');
    }
}
