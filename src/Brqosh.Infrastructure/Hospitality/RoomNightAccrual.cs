using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Hospitality;

/// <summary>
/// <b>ليالي الإقامة تبلغ الأستاذَ في شهرها — وكانت تُقيَّد في حساب النزيل
/// وحدَه.</b>
///
/// <para><b>ما كان:</b> <see cref="FolioService.AccrueNightsAsync"/> يُضيف الليلةَ إلى حساب النزيل (الدفتر المساعد) <b>ولا يُرحّل
/// شيئاً</b>، والإيرادُ كلُّه يومَ المغادرة — وتعليقُ الخدمة والزرُّ «تقييد الليالي» يقولان غيرَ ذلك. <b>فنزيلٌ مقيمٌ آخرَ الشهر لا
/// يظهر إيرادُ لياليه الماضية</b>: فندقٌ ممتلئٌ في ديسمبر يُقفل سنتَه ناقصاً إيرادَ كلِّ مقيمٍ لم يغادر.</para>
///
/// <para><b>والكيف</b>: كلُّ ليلةٍ تُقيَّد في حساب النزيل تُرحَّل <b>إيراداً مستحقّاً غيرَ مفوتر</b> (مدين <c>1118</c> / دائن
/// <c>4101</c>) — <b>مجمَّعةً بالشهر ومؤرَّخةً بآخر ليلةٍ منه</b>، فتشغيلةٌ متأخّرةٌ تضع كلَّ ليلةٍ في شهرها. <b>والمغادرةُ تُطلق
/// المستحقّ</b> (مدين <c>4101</c> / دائن <c>1118</c>) لأنّ فاتورتها تحمل الليالي كلَّها — <b>بقيد إطلاقٍ لا بعكس</b>، فلا يحتاج مكتبُ
/// الاستقبال صلاحيّةَ العكس. والضريبةُ في الفاتورة كما هي: نقطتُها الفوترة.</para>
/// </summary>
public class RoomNightAccrual(BrqoshDbContext db)
{
    private const string Accrued = SaudiChartOfAccounts.SystemCodes.AccruedRevenue;
    private const string Revenue = SaudiChartOfAccounts.SystemCodes.SalesRevenue;

    public static string AccrualKey(long folioId) => $"RoomNightAccrual:{folioId}";

    public static string ReleaseKey(long folioId) => $"RoomNightAccrualRelease:{folioId}";

    /// <summary>
    /// يُرحّل الليالي المضافة للتوّ — <b>قيدٌ لكلّ شهرٍ مؤرَّخٌ بآخر ليلةٍ منه</b>، بمجموع أسعارها (والسعرُ صافٍ من الضريبة كما
    /// تحمله الفاتورة).
    /// </summary>
    public async Task AccrueAsync(long companyId, long folioId, string reservationNumber,
        IReadOnlyList<(DateOnly Night, decimal Rate)> nights, CancellationToken ct)
    {
        if (nights.Count == 0)
            return;

        var accrued = await SystemAccountProvisioning.EnsureAsync(db, companyId, Accrued, ct);
        var revenue = await SystemAccountProvisioning.EnsureAsync(db, companyId, Revenue, ct);

        foreach (var month in nights.GroupBy(n => (n.Night.Year, n.Night.Month)).OrderBy(g => g.Key))
        {
            var amount = month.Sum(n => n.Rate);
            if (amount <= 0m)
                continue;

            await PostAsync(companyId, month.Max(n => n.Night),
                $"إيرادُ ليالٍ مستحقّ — حجز {reservationNumber} ({month.Count()} ليلة)", AccrualKey(folioId),
                debit: accrued, credit: revenue, amount, ct);
        }
    }

    /// <summary>
    /// المغادرةُ تُطلق ما استُحقّ لهذا الحساب — ففاتورتُها تحمل الليالي كلَّها، <b>وبدون الإطلاق يُعترف بالمستحقّ مرّتين</b>.
    /// <b>والمبلغُ من الأستاذ لا من الليالي</b>: ليلةٌ لم تُستحقّ لا تُطلَق، واستحقاقٌ عُكس لا يُطلَق.
    /// </summary>
    public async Task ReleaseAsync(long companyId, long folioId, DateOnly on, CancellationToken ct)
    {
        var keys = new[] { AccrualKey(folioId), ReleaseKey(folioId) };
        var lines = await db.JournalEntries
            .WhereReportable(companyId)
            .Where(e => e.SourceDocument != null && keys.Contains(e.SourceDocument))
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == Accrued)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(ct);
        var outstanding = lines.Sum(l => l.Debit - l.Credit);
        if (outstanding <= 0m)
            return;

        var accrued = await SystemAccountProvisioning.EnsureAsync(db, companyId, Accrued, ct);
        var revenue = await SystemAccountProvisioning.EnsureAsync(db, companyId, Revenue, ct);
        await PostAsync(companyId, on, "إطلاقُ إيراد الليالي المستحقّ — فاتورةُ المغادرة تحمله", ReleaseKey(folioId),
            debit: revenue, credit: accrued, outstanding, ct);
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
