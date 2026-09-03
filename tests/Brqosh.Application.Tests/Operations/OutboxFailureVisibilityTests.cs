using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-231 · O-36) <b>فشلُ تفريغ طابور الأحداث يُسجَّل — والتسامحُ يبقى.</b>
///
/// <para><b>ما كان:</b> <c>OutboxDispatchHook.OnCommittedAsync</c> يبتلع كلَّ استثناءٍ
/// في <c>catch { }</c> فارغ. <b>والتسامحُ مقصودٌ وصحيح</b>: الإرسالُ الخارجيّ لا يُفشل
/// عمليةً محاسبيةً التُزمت بالفعل، والأحداثُ تبقى معلَّقةً لإعادة المحاولة.</para>
///
/// <para><b>لكنّ الصمتَ كان يُخفي عطلاً دائماً</b>: نقطةُ استقبالٍ ماتت، أو شهادةٌ
/// انتهت، أو مفتاحٌ بُدِّل — <b>فيتراكم الطابورُ شهوراً ولا سطرَ واحدٌ يقول لماذا</b>.
/// والتقليمُ لا يمسّ المعلَّق (<c>ط5-١١</c>)، <b>فالطابورُ ينمو بلا حدٍّ وبلا خبر</b>.</para>
///
/// <para><b>وهذا الحارسُ يقيس ثلاثةً:</b> أنّ الفشلَ لا يُرمى، <b>وأنّه يُسجَّل باسمه</b>،
/// <b>وأنّ النجاحَ يمحو الأثر</b> — فعطلٌ أُصلح يبقى معروضاً هو إنذارٌ كاذبٌ يُدرَّب
/// المشغّلُ على تجاهله.</para>
/// </summary>
public class OutboxFailureVisibilityTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public OutboxFailureVisibilityTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        OutboxDispatchHook.Reset();
        OutboxDispatchHook.ResetFailureForTests();
    }

    public void Dispose()
    {
        OutboxDispatchHook.Reset();
        OutboxDispatchHook.ResetFailureForTests();
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: يفشل الإرسالُ فلا يُرمى — ويُسجَّل سببُه باسمه.</b>
    /// </summary>
    [Fact]
    public async Task AFailingDispatch_DoesNotThrow_AndIsRecorded()
    {
        OutboxDispatchHook.Wire((_, _) =>
            throw new InvalidOperationException("نقطةُ الاستقبال لا تستجيب"));

        // ‏**ولا يُرمى** — فعمليةٌ محاسبيةٌ التُزمت لا تُرتَدّ لأنّ webhook سقط.
        await OutboxDispatchHook.OnCommittedAsync(_db, CancellationToken.None);

        Assert.NotNull(OutboxDispatchHook.LastFailure);
        Assert.Contains("لا تستجيب", OutboxDispatchHook.LastFailure!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والنجاحُ يمحو الأثر — فإنذارٌ باقٍ بعد الإصلاح يُدرَّب المشغّلُ على تجاهله.</b>
    /// </summary>
    [Fact]
    public async Task ASuccessfulDispatch_ClearsThePreviousFailure()
    {
        OutboxDispatchHook.Wire((_, _) => throw new InvalidOperationException("عطلٌ مؤقّت"));
        await OutboxDispatchHook.OnCommittedAsync(_db, CancellationToken.None);
        Assert.NotNull(OutboxDispatchHook.LastFailure);

        OutboxDispatchHook.Wire((_, _) => Task.CompletedTask);
        await OutboxDispatchHook.OnCommittedAsync(_db, CancellationToken.None);

        Assert.Null(OutboxDispatchHook.LastFailure);
    }

    /// <summary>
    /// <b>وغيابُ المرسِل ليس فشلاً — فاختبارُ وحدةٍ بلا حقنٍ ليس عطلاً في الإنتاج.</b>
    /// </summary>
    [Fact]
    public async Task NoHandler_RecordsNothing()
    {
        await OutboxDispatchHook.OnCommittedAsync(_db, CancellationToken.None);

        Assert.Null(OutboxDispatchHook.LastFailure);
    }
}
