using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// إغلاق وإعادة فتح الفترات المالية مع تسجيل التدقيق.
///
/// <para>(م-٢ · ن-٠٣) <b>وكان التابعان يعملان بلا معاملةٍ ولا قفلٍ ولا رمزِ تزامن</b> —
/// ثلاثةُ غياباتٍ كلٌّ منها كافٍ وحدَه:</para>
///
/// <para><b>١ — بلا معاملة:</b> <c>SaveChangesAsync</c> يلتزم حالةَ الفترة، ثمّ يفتح
/// <c>AuditService</c> معاملتَه الخاصّة لسطر التدقيق. <b>فسقوطٌ بينهما يترك فترةً مقفلةً
/// بلا أثرٍ في السجلّ</b> — وإقفالُ فترةٍ صلاحيةٌ إداريّةٌ حسّاسة، وسجلُّها ليس زينة.</para>
///
/// <para><b>٢ — بلا قفل:</b> والحارسُ الوحيد قراءةٌ عاديّة. <b>وقيس:</b> فاتورةٌ تُرحَّل
/// وفترتُها تُقفَل معاً ⟵ التزم الإقفالُ بعد ٤٢٩ م.ث بلا انتظار، والنتيجةُ
/// <c>الفترة=Closed · الفاتورة=Posted</c>. <b>أي فاتورةٌ مرحّلةٌ في فترةٍ مقفلة</b> — وهي
/// الحالةُ التي وُجد كلُّ حارسِ فتراتٍ في النظام ليمنعها.</para>
///
/// <para><b>والعلاجُ لا يقع هنا وحدَه:</b> القفلُ الحصريُّ على دفتر السنة ينتظر كلَّ ترحيلٍ
/// طائرٍ حتى يلتزم <b>ويحجب الجديد</b> — والمرحّلون يأخذون شقَّه المشترَك في
/// <see cref="JournalPostingService.EnsurePeriodOpenAsync"/>، وهو مصبُّ الترحيل كلِّه.
/// انظر <c>LedgerYearLock</c>.</para>
/// </summary>
public class FiscalPeriodService(BrqoshDbContext db)
{
    public Task CloseAsync(long companyId, long periodId, string username, CancellationToken ct = default) =>
        ChangeStateAsync(companyId, periodId, username, close: true, ct);

    public Task ReopenAsync(long companyId, long periodId, string username, CancellationToken ct = default) =>
        ChangeStateAsync(companyId, periodId, username, close: false, ct);

    /// <summary>
    /// <b>والتابعان صارا واحداً</b> — لا اختصاراً بل لأنّ الفارقَ بينهما سطرٌ واحد، وكلُّ ما
    /// عداه (المعاملة · القفل · إعادةُ القراءة · التدقيق) شرطٌ مشترَك. <b>ونسختان من شرطٍ
    /// أمانٍ تفترقان في أوّل تعديل</b> — وهو ما وقع فعلاً: <c>ReopenAsync</c> يفتح فترةً
    /// أُقفلت بإقفالٍ سنويّ، فلو نُسي فيه القفلُ لعاد العطلُ من بابه.
    /// </summary>
    private async Task ChangeStateAsync(
        long companyId, long periodId, string username, bool close, CancellationToken ct)
    {
        SecurityContext.EnsureAdministrator();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var period = await db.FiscalPeriods.SingleAsync(
                p => p.CompanyId == companyId && p.Id == periodId, token);

            // ‏**القفلُ أوّلاً، والسنةُ تُعرف من القراءة أعلاه** — وسنةُ الفترة لا تتغيّر،
            // ‏فقراءةٌ بائتةٌ تكفي لاشتقاق المفتاح وحدَه ولا تكفي لقرار الإقفال.
            await LedgerYearLock.AcquireForClosingAsync(db.Database, companyId,
                await FiscalYearResolver.StartYearForAsync(db, companyId, period.StartDate, token), token);

            // ‏**ثمّ تُعاد القراءة بعد القفل** — وهذا السطرُ هو الفرقُ بين حارسٍ وزينة.
            // ‏فالصفُّ الذي قُرئ قبل القفل قد يكون قد تغيّر بينما ننتظره، والمتعقّبُ يُعيد
            // ‏المثيلَ القديمَ من خريطة الهويّة بلا أن يسأل القاعدة. **فيُقفَل ما أُقفل، أو
            // ‏يُبنى القرارُ على حالةٍ ماتت أثناء الانتظار.**
            await db.Entry(period).ReloadAsync(token);

            if (close) period.Close(); else period.Reopen();
            await db.SaveChangesAsync(token);

            // ‏وسطرُ التدقيق **داخل المعاملة نفسِها** — فإمّا يقع التغييرُ وأثرُه معاً أو
            // ‏لا يقع أيٌّ منهما. و`AuditService` ينضمّ إلى المعاملة القائمة ولا يفتح ثانية.
            await new AuditService(db).LogAsync(
                companyId, username,
                close ? "إغلاق فترة مالية" : "إعادة فتح فترة مالية",
                "FiscalPeriod", period.Code,
                $"من {period.StartDate:yyyy-MM-dd} إلى {period.EndDate:yyyy-MM-dd}");

            return true;
        }, ct);
    }
}
