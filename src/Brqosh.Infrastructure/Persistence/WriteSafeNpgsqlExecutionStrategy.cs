using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// إعادةُ المحاولة التلقائية للقراءات فقط — <b>ومسارُ الكتابة (<see cref="UnitOfWork"/>) يُنفَّذ مرّةً واحدة.</b>
///
/// <para>كانت المعاملةُ كلُّها تُعاد على <b>السياق نفسِه</b>: الكياناتُ المضافةُ في المحاولة الأولى
/// ما تزال متتبَّعة، والمستندُ عُدِّل في الذاكرة (رقمٌ خُصِّص وحالةٌ تغيّرت). فإعادتُها قد تُرحِّل
/// قيداً مكرّراً أو تفشل بحالةٍ بائتة — والترحيلُ ليس متكرّرَ الأثر. فخطأٌ واضحٌ يعيده المستخدمُ
/// أسلمُ في نظامٍ محاسبيّ من إعادةٍ صامتة.</para>
/// </summary>
public sealed class WriteSafeNpgsqlExecutionStrategy(
    ExecutionStrategyDependencies dependencies, int maxRetryCount, TimeSpan maxRetryDelay)
    : NpgsqlRetryingExecutionStrategy(dependencies, maxRetryCount, maxRetryDelay, errorCodesToAdd: null)
{
    private static readonly AsyncLocal<bool> Suspended = new();

    public override bool RetriesOnFailure => !Suspended.Value && base.RetriesOnFailure;

    /// <summary>يُعلِّق إعادة المحاولة لمسار الكتابة الجاري (وما يتفرّع عنه) حتى التخلّص من الناتج.</summary>
    public static IDisposable SuspendForWrite()
    {
        var previous = Suspended.Value;
        Suspended.Value = true;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => Suspended.Value = previous;
    }
}
