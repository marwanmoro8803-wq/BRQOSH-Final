using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.RealEstate;

/// <summary>
/// <b>الإيجارُ المقدَّمُ يُكتسب شهراً بشهر — وكان يُعترف به كلُّه يومَ
/// الفاتورة.</b>
///
/// <para><b>ما كان:</b> عقدٌ ربعيٌّ أو نصفيٌّ أو سنويٌّ يُفوتَر مقدَّماً عبر <c>RecurringInvoiceService</c>، والفاتورةُ تُرحَّل
/// كأيّ فاتورة مبيعات: <b>الإيرادُ كلُّه في يومها</b>. فعقدٌ سنويٌّ يبدأ في يوليو يُعلن إيرادَ اثني عشر شهراً في سنةٍ لم يمضِ منها
/// إلّا ستّة — <b>ربحٌ مُعلَنٌ مُضخَّمٌ تُدفع عليه زكاة</b>، ومطلوبٌ للمستأجر لا يظهر في المركز الماليّ.</para>
///
/// <para><b>والمعيارُ</b>: دخلُ الإيجار التشغيليّ للمؤجِّر يُعترف به على أساس القسط الثابت على مدّة العقد. <b>والضريبةُ لا
/// تتبعه</b>: نقطتُها الفوترةُ والدفعُ المقدَّم، فتبقى في الفاتورة كما هي.</para>
///
/// <para><b>والكيف</b>: فاتورةُ الدورة تبقى كما هي، ويُرحَّل معها في المعاملة نفسِها <b>قيدُ تأجيل</b> (من الإيراد إلى
/// <c>2117</c> إيرادات إيجار مقبوضة مقدّماً) بما يزيد على الشهر الأوّل، <b>وقيدُ اعترافٍ لكلّ شهرٍ تالٍ مؤرَّخٌ بشهره</b>.
/// والتقاريرُ تُصفّي بالتاريخ فلا يظهر شهرٌ قبل أوانه — <b>ولا يعتمد الاعترافُ على تشغيلٍ يتذكّره أحد</b>.</para>
///
/// <para><b>والأطرافُ</b>: إلغاءُ الفاتورة يعكس التأجيلَ وكلَّ اعترافٍ قائم (الماضي بتاريخ الإلغاء، والمستقبلُ بتاريخه فيصفو
/// شهرُه)؛ <b>وإنهاءُ العقد يعكس اعترافاتِ ما بعده بتواريخها</b>، ويبقى غيرُ المكتسب في <c>2117</c> مستحقّاً للمستأجر.</para>
/// </summary>
public class LeaseRentRecognition(BrqoshDbContext db)
{
    internal const string DeferralPrefix = "RentDeferral:";
    internal const string EarnedPrefix = "RentEarned:";

    public static string DeferralKey(long invoiceId) => $"{DeferralPrefix}{invoiceId}";

    public static string EarnedKey(long invoiceId, int month) => $"{EarnedPrefix}{invoiceId}:{month}";

    /// <summary>كم شهراً تغطّي فاتورةُ دورةٍ واحدة — والأسبوعيّةُ والشهريّةُ لا تأجيلَ فيهما.</summary>
    internal static int MonthsPerPeriod(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Quarterly => 3,
        RecurrenceFrequency.SemiAnnual => 6,
        RecurrenceFrequency.Annual => 12,
        _ => 1,
    };

    /// <summary>شرائحُ متساويةٌ والفرقُ في الأخيرة — فمجموعُها الإيرادُ بالهللة.</summary>
    internal static IReadOnlyList<decimal> Slices(decimal revenue, int months)
    {
        var each = Math.Round(revenue / months, 2, MidpointRounding.AwayFromZero);
        var slices = Enumerable.Repeat(each, months).ToList();
        slices[^1] = revenue - each * (months - 1);
        return slices;
    }

    /// <summary>
    /// يُؤجّل ما زاد على الشهر الأوّل من فاتورة دورةٍ لعقد إيجار، ويُرحّل اعترافَ كلّ شهرٍ تالٍ بتاريخه. <b>وخطّةٌ ليست لعقدٍ لا
    /// تُمسّ</b>، ولا عقدٌ دورتُه شهرٌ، ولا شهرٌ بعد نهاية العقد.
    /// </summary>
    public async Task DeferAsync(RecurringInvoicePlan plan, SalesInvoice invoice, DateOnly occurrence, CancellationToken ct)
    {
        var lease = await db.LeaseContracts.SingleOrDefaultAsync(
            l => l.CompanyId == plan.CompanyId && l.RecurringPlanId == plan.Id, ct);
        if (lease is null || invoice.JournalEntryId is not long invoiceEntryId)
            return;

        var months = Enumerable.Range(0, MonthsPerPeriod(lease.Frequency))
            .Count(i => occurrence.AddMonths(i) <= lease.EndDate);
        if (months <= 1)
            return;

        // ‏الإيرادُ كما رحّلته الفاتورةُ نفسُها — لا كما يُعاد حسابُه: خصمٌ أو تقريبٌ في الترحيل يبقى واحداً في الطرفين.
        var revenueLines = await db.JournalEntries
            .Where(e => e.Id == invoiceEntryId)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.SalesRevenue)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(ct);
        var revenue = revenueLines.Sum(l => l.Credit - l.Debit);
        if (revenue <= 0m)
            return;

        var slices = Slices(revenue, months);
        var revenueAccount = await SystemAccountProvisioning.EnsureAsync(
            db, plan.CompanyId, SaudiChartOfAccounts.SystemCodes.SalesRevenue, ct);
        var unearned = await SystemAccountProvisioning.EnsureAsync(
            db, plan.CompanyId, SaudiChartOfAccounts.SystemCodes.UnearnedRent, ct);

        await PostAsync(plan.CompanyId, occurrence,
            $"تأجيلُ إيجارٍ مقدَّم — فاتورة {invoice.InvoiceNumber}", DeferralKey(invoice.Id),
            debit: revenueAccount, credit: unearned, revenue - slices[0], ct);

        for (var i = 1; i < months; i++)
        {
            await PostAsync(plan.CompanyId, occurrence.AddMonths(i),
                $"إيجارٌ مكتسب — فاتورة {invoice.InvoiceNumber} · الشهر {i + 1} من {months}", EarnedKey(invoice.Id, i),
                debit: unearned, credit: revenueAccount, slices[i], ct);
        }
    }

    /// <summary>
    /// إلغاءُ فاتورةٍ يعكس تأجيلَها وكلَّ اعترافٍ قائمٍ لها — <b>الماضي بتاريخ الإلغاء، والمستقبلُ بتاريخه</b> فيصفو شهرُه.
    /// </summary>
    public async Task WithdrawForCancelledInvoiceAsync(
        long companyId, long invoiceId, DateOnly cancelledOn, string username, CancellationToken ct)
    {
        var deferral = DeferralKey(invoiceId);
        var earned = $"{EarnedPrefix}{invoiceId}:";
        var standing = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.SourceDocument != null
                        && (e.SourceDocument == deferral || e.SourceDocument.StartsWith(earned)))
            .WhereStanding(db.JournalEntries)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.EntryDate })
            .ToListAsync(ct);

        foreach (var entry in standing)
        {
            await new JournalReversalService(db).ReverseAsync(companyId, entry.Id,
                entry.EntryDate > cancelledOn ? entry.EntryDate : cancelledOn, username, fromSourceDocument: true, ct);
        }
    }

    /// <summary>
    /// إنهاءُ العقد يعكس اعترافاتِ ما بعد الإنهاء <b>بتواريخها</b> — فلا يُكتسب إيجارُ شهرٍ لم يُسكن؛ ويبقى غيرُ المكتسب في
    /// <c>2117</c> مستحقّاً للمستأجر يُردّ أو يُسوّى بإشعارٍ دائن.
    /// </summary>
    public async Task WithdrawAfterTerminationAsync(
        LeaseContract lease, DateOnly terminatedOn, string username, CancellationToken ct)
    {
        if (lease.RecurringPlanId is not long planId)
            return;

        var invoiceIds = await db.RecurringInvoiceRuns
            .Where(r => r.CompanyId == lease.CompanyId && r.PlanId == planId)
            .Select(r => r.SalesInvoiceId)
            .ToListAsync(ct);

        foreach (var invoiceId in invoiceIds)
        {
            var earned = $"{EarnedPrefix}{invoiceId}:";
            var later = await db.JournalEntries
                .Where(e => e.CompanyId == lease.CompanyId && e.SourceDocument != null
                            && e.SourceDocument.StartsWith(earned) && e.EntryDate > terminatedOn)
                .WhereStanding(db.JournalEntries)
                .OrderBy(e => e.EntryDate).ThenBy(e => e.Id)
                .Select(e => new { e.Id, e.EntryDate })
                .ToListAsync(ct);

            foreach (var entry in later)
            {
                await new JournalReversalService(db).ReverseAsync(lease.CompanyId, entry.Id,
                    entry.EntryDate, username, fromSourceDocument: true, ct);
            }
        }
    }

    private async Task PostAsync(long companyId, DateOnly date, string description, string key,
        Account debit, Account credit, decimal amount, CancellationToken ct)
    {
        await FinancialPostingSupport.ValidateAsync(db, companyId, date, PostingDocumentKind.JournalEntry,
            accountIds: [debit.Id, credit.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, date, description, key);
        entry.AddLine(debit, amount, 0m, description);
        entry.AddLine(credit, 0m, amount, description);
        entry.Post(await new JournalEntryNumberGenerator(db).NextAsync(companyId, date, ct));
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
