using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-229) <b>قفلُ سلسلة التدقيق من نوع <c>xact</c> — وميّتٌ خارج معاملة.</b>
///
/// <para><c>AuditService.LogAsync</c> يأخذ <c>pg_advisory_xact_lock</c> ثمّ يقرأ آخرَ إدخالٍ
/// في السلسلة ليبني عليه. <b>والقفلُ من نوع «معاملة»: خارجَها يُحرَّر فورَ انتهاء الأمر</b> —
/// فلا يحمي القراءةَ التي تليه.</para>
///
/// <para><b>وهذا بعينه العطلُ الذي أُغلق مرّتين في هذه الحملة:</b> في <c>م1-٦ / Z-4</c> حيث
/// كان قفلُ العدّاد يعود مبكّراً، وفي <c>م1-١٤</c> حيث صار <c>DocumentSequenceLock</c>
/// <b>يرمي</b> إن استُدعي خارج معاملة. <b>وبقي هذا الثالثُ بلا حارس</b> — مُسجَّلاً بنداً
/// مفتوحاً منذ م2-١١.</para>
///
/// <para><b>وستّون مستدعياً</b> لـ<c>LogAsync</c> في المستودع، منهم من يُنادي من شاشةٍ
/// مباشرةً بلا معاملةٍ إطلاقاً.</para>
///
/// <para><b>ولا يظهر على SQLite:</b> القفلُ يعود مبكّراً هناك بلا أثر، والكتبةُ يتسلسلون
/// بحكم المحرّك. فيُقاس على PostgreSQL حصراً — <b>درسُ Z-4</b>.</para>
/// </summary>
public class AuditChainConcurrencyTests
{
    private const int Writers = 24;

    /// <summary>
    /// <b>القياس المركزيّ.</b> ستّةُ كتبةٍ متزامنين على سلسلة منشأةٍ واحدة، <b>بلا معاملة</b> ⟵
    /// تُكتب الستّةُ كلُّها والسلسلةُ سليمة.
    ///
    /// <para><b>ولماذا «كلُّها» لا «واحدةٌ تنجح»؟</b> لأنّ سطرَ التدقيق ليس عمليةً تنافسية
    /// كسندِ قبض: كلُّ حدثٍ وقع <b>يجب</b> أن يُسجَّل. فسقوطُ أحدها يعني <b>حدثاً محاسبياً
    /// وقع ولا أثرَ له</b> — وهو ما بُنيت السلسلةُ لمنعه.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentWritesWithoutATransaction_AllLandAndTheChainHolds()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("auditchain");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة السلسلة", "310123456700033", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            // ‏بلا معاملة — وهو حالُ المستدعي من شاشةٍ مباشرة.
            await new AuditService(db).LogAsync(
                company.Id, "admin", "ترحيل", "TestEntity", $"K-{i}", $"تفاصيل {i}");
            return i;
        });

        await using var reader = scope.OpenIndependentContext();
        var chain = await new AuditChainVerifier(reader).VerifyAsync(company.Id);

        Assert.True(outcome.Successes.Count == Writers,
            $"سقط {Writers - outcome.Successes.Count} من {Writers} سطرِ تدقيق — "
            + $"فحدثٌ وقع ولا أثرَ له. {outcome.Describe()}");

        Assert.True(chain.IsIntact,
            $"كُسرت سلسلةُ التدقيق بكتابةٍ متزامنة — {chain.Breaks.Count} انقطاعاً. "
            + $"{string.Join(" · ", chain.Breaks.Select(b => b.Kind))}");

        Assert.Equal(Writers, chain.EntriesChecked);
    }
}
