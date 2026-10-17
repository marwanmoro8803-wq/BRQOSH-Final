using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Npgsql;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٦ · <c>tax-zatca#8</c>) <b>محطّةٌ واحدةٌ ترسل الدفعةَ في آن</b> — على PostgreSQL حقيقيّ.
///
/// <para>مؤقّتا محطّتين ينبضان معاً على قاعدةٍ واحدة: بلا قفلٍ يُرسَل المستندُ نفسُه إلى الهيئة مرّتين. فالقفلُ لا ينتظر —
/// من وجده محجوزاً تخطّى دورتَه — ويُحرَّر بعد الدفعة فتأخذه المحطّةُ التالية.</para>
/// </summary>
public class OneWorkstationResubmitsAtATimeTests
{
    [PostgresFact]
    public async Task WhileAnotherWorkstationHoldsTheLock_TheBatchIsSkipped_AndAfterwardsItRuns()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("zatcalock");
        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة القفل", "310123456700156", BusinessProfile.General);

        // ‏«محطّةٌ أخرى» — اتّصالٌ مستقلٌّ يحجز القفلَ نفسَه بالمفتاح نفسِه.
        await using (var other = new NpgsqlConnection(scope.ConnectionString))
        {
            await other.OpenAsync();
            await using (var take = new NpgsqlCommand("SELECT pg_advisory_lock(@c::int, @k::int)", other))
            {
                take.Parameters.AddWithValue("c", (int)company.Id);
                take.Parameters.AddWithValue("k", ZatcaSubmissionService.ResubmitLockKey);
                await take.ExecuteNonQueryAsync();
            }

            var whileHeld = await new ZatcaSubmissionService(scope.Db).ResubmitPendingInBackgroundAsync(company.Id);
            Assert.True(whileHeld.Skipped);

            // ‏**وإغلاقُ الاتّصال لا يحرّره**: Npgsql يُعيده إلى المجمَّع وجلسةُ PostgreSQL حيّةٌ بقفلها — قِيس هنا أوّلاً.
            // ‏ولذلك تحرّره الخدمةُ صراحةً في `finally` ولا تتّكل على الإغلاق.
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@c::int, @k::int)", other);
            release.Parameters.AddWithValue("c", (int)company.Id);
            release.Parameters.AddWithValue("k", ZatcaSubmissionService.ResubmitLockKey);
            Assert.True((bool)(await release.ExecuteScalarAsync())!);
        }

        // ‏تحرّر القفل — والدورةُ التالية تجري (لا معلَّقَ فلا إرسال، ولا تخطّي).
        var afterwards = await new ZatcaSubmissionService(scope.Db).ResubmitPendingInBackgroundAsync(company.Id);
        Assert.False(afterwards.Skipped);

        // ‏وقفلُ الدورة نفسِها حُرِّر: محطّةٌ أخرى تأخذه الآن.
        await using var next = new NpgsqlConnection(scope.ConnectionString);
        await next.OpenAsync();
        await using var probe = new NpgsqlCommand("SELECT pg_try_advisory_lock(@c::int, @k::int)", next);
        probe.Parameters.AddWithValue("c", (int)company.Id);
        probe.Parameters.AddWithValue("k", ZatcaSubmissionService.ResubmitLockKey);
        Assert.True((bool)(await probe.ExecuteScalarAsync())!);
    }
}
