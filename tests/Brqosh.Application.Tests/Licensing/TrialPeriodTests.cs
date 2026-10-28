using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// الفترةُ التجريبية: تبدأ بأوّل تشغيل، وسبعةُ أيامٍ بالتمام، ولا يعيدها حذفُ
/// مواضعَ ولا رجوعُ ساعة. والسجلُّ مربوطٌ بالجهاز ومختوم — فمنسوخٌ أو محرَّفٌ يُهمَل.
/// كلُّ الاختبارات هنا على مخزنٍ بديلٍ في الذاكرة — لا يُمسّ جهازُ المطوّر.
/// </summary>
public class TrialPeriodTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryTrialStore _store = new();

    public TrialPeriodTests() => TrialPeriod.StoreOverride = _store;

    public void Dispose() => TrialPeriod.StoreOverride = null;

    [Fact]
    public void The_first_run_stamps_today_and_grants_seven_days()
    {
        var state = TrialPeriod.EnsureStarted(Now);

        Assert.False(state.Expired);
        Assert.Equal(7, state.DaysLeft);
        Assert.Equal(Now, state.StartedUtc);
        Assert.Equal(Now.AddDays(7), state.ExpiresUtc);
        Assert.NotNull(_store.Record);
    }

    [Fact]
    public void A_second_run_does_not_move_the_start()
    {
        TrialPeriod.EnsureStarted(Now);
        var again = TrialPeriod.EnsureStarted(Now.AddDays(3));

        Assert.Equal(Now, again.StartedUtc);
        Assert.Equal(4, again.DaysLeft);
    }

    [Fact]
    public void After_seven_full_days_the_trial_is_expired()
    {
        _store.Record = TrialPeriod.Stamp(Now);

        var state = TrialPeriod.Status(Now.AddDays(7))!;

        Assert.True(state.Expired);
        Assert.Equal(0, state.DaysLeft);
    }

    [Fact]
    public void Days_left_round_up_partial_days()
    {
        _store.Record = TrialPeriod.Stamp(Now);

        Assert.Equal(1, TrialPeriod.Status(Now.AddDays(6).AddHours(1))!.DaysLeft);
        Assert.Equal(7, TrialPeriod.Status(Now.AddHours(1))!.DaysLeft);
    }

    [Fact]
    public void A_rolled_back_clock_does_not_extend_the_trial()
    {
        _store.Record = TrialPeriod.Stamp(Now);

        var state = TrialPeriod.Status(Now.AddDays(-2))!;

        Assert.False(state.Expired);
        Assert.Equal(7, state.DaysLeft);
        Assert.Equal(Now, state.StartedUtc);
    }

    [Fact]
    public void Resolve_takes_the_earliest_valid_record()
    {
        var validOld = TrialPeriod.Stamp(Now.AddDays(-9));
        var validNew = TrialPeriod.Stamp(Now.AddDays(-1));
        var otherMachine = validOld with { MachineBind = "000000000000" };
        var tampered = validNew with { Mac = new string('0', 24) };

        var resolved = TrialPeriod.Resolve([otherMachine, tampered, validNew, validOld]);

        Assert.Equal(Now.AddDays(-9), resolved);
    }

    [Fact]
    public void Resolve_ignores_everything_when_nothing_is_valid() =>
        Assert.Null(TrialPeriod.Resolve([TrialPeriod.Stamp(Now) with { MachineBind = "ABCDEFABCDEF" }]));

    private sealed class InMemoryTrialStore : TrialPeriod.ITrialStampStore
    {
        public TrialPeriod.TrialStampRecord? Record { get; set; }

        public TrialPeriod.TrialStampRecord? Read() => Record;

        public void Write(TrialPeriod.TrialStampRecord record) => Record = record;
    }
}
