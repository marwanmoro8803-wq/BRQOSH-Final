using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٠ · <c>sector-modules#3</c>) <b>إيرادُ عقد المقاولات يُسوّى تراكميّاً بنسبة الإنجاز — وكان كلُّ مستخلصٍ
/// يعترف بحدّه ويؤجّل الزائد، ثمّ لا يعود إليه أحد.</b>
///
/// <para><b>ما كان:</b> <see cref="ProgressBillingIfrs15"/> يحسب إيرادَ المستخلص بنسبة التكلفة <b>ويُقيّده بمبلغ المستخلص</b>،
/// والزائدُ يُؤجَّل إلى <c>2105</c>. فعطلان: <b>المؤجَّلُ لا يُطلَق أبداً</b> — تلحق التكاليفُ النسبةَ ولا مستخلصَ بعدها فيبقى الإيرادُ
/// التزاماً إلى الأبد؛ <b>وما سبقت فيه التكاليفُ الفوترةَ لا يُعترف به</b> — مشروعٌ أنجز ربعَه ولم يُفوتِر شيئاً إيرادُه صفر.</para>
///
/// <para><b>والمعيارُ</b> (الاعترافُ على مدى الزمن بطريقة المدخلات): الإيرادُ حتى تاريخه = قيمةُ العقد × نسبةُ الإنجاز، <b>والفرقُ عن
/// المُعترَف به تسويةٌ تراكميّة</b>؛ وما فُوتر فوق المكتسب التزامُ عقد (<c>2105</c>)، وما اكتُسب فوق المفوتَر أصلُ عقد (<c>1113</c> أعمال
/// تحت التنفيذ — مستخلصات غير مفوترة).</para>
///
/// <para><b>والتسويةُ بعد كلّ حدثٍ يُغيّر النسبةَ أو الفوترة</b>: تسجيلُ تكلفة، وترحيلُ مستخلص، وإلغاؤه، وتعديلُ المشروع (مراجعةُ
/// التقدير لحاقٌ تراكميّ). <b>ولا تمسّ المشروعَ بطريقة الفوترة</b> (بلا تقديرٍ أو بلا تكلفة) — هناك الإيرادُ ما فُوتر.</para>
/// </summary>
public class ContractRevenueTrueUp(BrqoshDbContext db)
{
    private const string Revenue = SaudiChartOfAccounts.SystemCodes.ContractRevenue;
    private const string Deferred = SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers;
    private const string Asset = SaudiChartOfAccounts.SystemCodes.WorkInProgress;

    public static string Key(long projectId) => $"ContractTrueUp:{projectId}";

    /// <summary>حركاتُ التسوية — كلُّها موجبة، والقيدُ يُبنى منها.</summary>
    internal readonly record struct Movement(
        decimal ReleaseDeferred, decimal RaiseAsset, decimal ReduceAsset, decimal Defer, decimal Offset);

    /// <summary>
    /// توزيعُ الفرق: <b>الموجبُ يُطلق المؤجَّلَ أوّلاً</b> ثمّ يرفع أصلَ العقد؛ <b>والسالبُ يُنقص أصلَ العقد أوّلاً</b> ثمّ يؤجّل؛ ثمّ
    /// يُقاصّ ما بقي منهما معاً — فلا يحمل عقدٌ واحدٌ أصلاً والتزاماً في آنٍ.
    /// </summary>
    internal static Movement Allocate(decimal delta, decimal deferred, decimal asset)
    {
        decimal release = 0m, raise = 0m, reduce = 0m, defer = 0m;
        if (delta > 0m)
        {
            release = Math.Min(delta, Math.Max(deferred, 0m));
            raise = delta - release;
        }
        else if (delta < 0m)
        {
            reduce = Math.Min(-delta, Math.Max(asset, 0m));
            defer = -delta - reduce;
        }

        var offset = Math.Min(Math.Max(deferred - release + defer, 0m), Math.Max(asset + raise - reduce, 0m));
        return new Movement(release, raise, reduce, defer, offset);
    }

    public async Task ApplyAsync(long companyId, long projectId, DateOnly on, CancellationToken ct = default)
    {
        var project = await db.ConstructionProjects.SingleAsync(
            p => p.Id == projectId && p.CompanyId == companyId, ct);
        if (project.ContractValue <= 0m || project.EstimatedTotalCost <= 0m)
            return;

        var incurred = await db.ProjectCostEntries
            .Where(c => c.CompanyId == companyId && c.ProjectId == projectId && !c.IsReversed)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        if (incurred <= 0m)
            return;

        // ‏النسبةُ والمكتسبُ بتقريب `ProgressBillingIfrs15` نفسِه — فلا يفترق المستخلصُ والتسويةُ بهللة.
        var percent = Math.Min(100m,
            Math.Round(incurred / project.EstimatedTotalCost * 100m, 4, MidpointRounding.AwayFromZero));
        var earned = Math.Round(project.ContractValue * percent / 100m, 2, MidpointRounding.AwayFromZero);

        var keys = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.ProjectId == projectId && b.BillingNumber != null)
            .Select(b => "ProgressBilling:" + b.BillingNumber)
            .ToListAsync(ct);
        keys.Add(Key(projectId));

        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.Status != JournalEntryStatus.Draft
                        && e.SourceDocument != null && keys.Contains(e.SourceDocument))
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == Revenue || l.AccountCode == Deferred || l.AccountCode == Asset)
            .Select(l => new { l.AccountCode, l.Debit, l.Credit })
            .ToListAsync(ct);

        var recognized = lines.Where(l => l.AccountCode == Revenue).Sum(l => l.Credit - l.Debit);
        var deferred = lines.Where(l => l.AccountCode == Deferred).Sum(l => l.Credit - l.Debit);
        var asset = lines.Where(l => l.AccountCode == Asset).Sum(l => l.Debit - l.Credit);

        var delta = earned - recognized;
        var move = Allocate(delta, deferred, asset);
        if (delta == 0m && move.Offset == 0m)
            return;

        var revenueAccount = await SystemAccountProvisioning.EnsureAsync(db, companyId, Revenue, ct);
        var deferredAccount = await SystemAccountProvisioning.EnsureAsync(db, companyId, Deferred, ct);
        var assetAccount = await SystemAccountProvisioning.EnsureAsync(db, companyId, Asset, ct);

        await FinancialPostingSupport.ValidateAsync(db, companyId, on, PostingDocumentKind.JournalEntry,
            accountIds: [revenueAccount.Id, deferredAccount.Id, assetAccount.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, on,
            $"تسويةُ إيراد العقد {project.Code} — إنجازٌ {percent:0.##}%", Key(projectId));
        if (move.ReleaseDeferred > 0m)
            entry.AddLine(deferredAccount, move.ReleaseDeferred, 0m, "إطلاقُ إيرادٍ مؤجَّلٍ لحقته التكاليف");
        if (move.RaiseAsset > 0m)
            entry.AddLine(assetAccount, move.RaiseAsset, 0m, "أصلُ عقد — مكتسبٌ غيرُ مفوتَر");
        if (delta > 0m)
            entry.AddLine(revenueAccount, 0m, delta, "إيرادٌ مكتسبٌ بنسبة الإنجاز");
        if (delta < 0m)
            entry.AddLine(revenueAccount, -delta, 0m, "ردُّ إيرادٍ زاد على نسبة الإنجاز");
        if (move.ReduceAsset > 0m)
            entry.AddLine(assetAccount, 0m, move.ReduceAsset, "إنقاصُ أصل العقد");
        if (move.Defer > 0m)
            entry.AddLine(deferredAccount, 0m, move.Defer, "تأجيلُ ما فُوتر فوق المكتسب");
        if (move.Offset > 0m)
        {
            entry.AddLine(deferredAccount, move.Offset, 0m, "مقاصّةُ التزام العقد بأصله");
            entry.AddLine(assetAccount, 0m, move.Offset, "مقاصّةُ أصل العقد بالتزامه");
        }

        entry.Post(await new JournalEntryNumberGenerator(db).NextAsync(companyId, on, ct));
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
