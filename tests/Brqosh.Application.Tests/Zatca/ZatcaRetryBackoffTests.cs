using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٦ · <c>tax-zatca#8</c>) <b>المحاولةُ الخلفيّةُ تتباعد بعد الفشل وتعود بعد النجاح.</b>
/// </summary>
public class ZatcaRetryBackoffTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AFreshSession_IsDueAtOnce() => Assert.True(new ZatcaRetryBackoff().IsDue(Now));

    /// <summary>القيمُ مكتوبةٌ لا محسوبة — مرجعٌ مستقلٌّ عن الصيغة: ٥ ثمّ ١٠ ثمّ ٢٠ ثمّ ٤٠ ثمّ ساعةٌ سقفاً.</summary>
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(12, 60)]
    [InlineData(1000, 60)]
    public void EachConsecutiveFailure_DoublesTheWait_UpToAnHour(int failures, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), ZatcaRetryBackoff.Delay(failures));

    [Fact]
    public void AFailure_IsNotDueBeforeItsWait_AndIsDueAfterIt()
    {
        var backoff = new ZatcaRetryBackoff(() => 0d);
        backoff.RecordFailure(Now);
        backoff.RecordFailure(Now);

        Assert.Equal(2, backoff.ConsecutiveFailures);
        Assert.False(backoff.IsDue(Now.AddMinutes(9)));
        Assert.True(backoff.IsDue(Now.AddMinutes(10)));
    }

    [Fact]
    public void ASuccess_ResetsTheFailures_AndWaitsTheBaseInterval()
    {
        var backoff = new ZatcaRetryBackoff(() => 0d);
        backoff.RecordFailure(Now);
        backoff.RecordFailure(Now);
        backoff.RecordFailure(Now);

        backoff.RecordSuccess(Now);

        Assert.Equal(0, backoff.ConsecutiveFailures);
        Assert.False(backoff.IsDue(Now.AddMinutes(4)));
        Assert.True(backoff.IsDue(Now.AddMinutes(5)));
    }

    // ‏(جاهزيّةُ الإنتاج · E) ‏التشتيتُ يقع بعد المهلة الحتميّة وحتى خُمسِها: ٢٠ دقيقةً للفشل الثالث ⟵ بين ٢٠ و٢٤.
    [Theory]
    [InlineData(0.0, 20.0)]
    [InlineData(0.5, 22.0)]
    [InlineData(1.0, 24.0)]
    public void TheJitter_IsAddedAfterTheWait_UpToAFifthOfIt(double draw, double expectedMinutes)
    {
        var backoff = new ZatcaRetryBackoff(() => draw);
        backoff.RecordFailure(Now);
        backoff.RecordFailure(Now);
        backoff.RecordFailure(Now);

        Assert.Equal(Now.AddMinutes(expectedMinutes), backoff.NextDueUtc);
    }

    // ‏مصدرٌ معطوبٌ يُعيد خارج [0,1] لا يُبكّر المحاولةَ ولا يمدّها فوق الخُمس.
    [Theory]
    [InlineData(-5.0, 5.0)]
    [InlineData(9.0, 6.0)]
    public void AnOutOfRangeDraw_IsClamped(double draw, double expectedMinutes)
    {
        var backoff = new ZatcaRetryBackoff(() => draw);
        backoff.RecordFailure(Now);

        Assert.Equal(Now.AddMinutes(expectedMinutes), backoff.NextDueUtc);
    }

    // ‏والافتراضيُّ عشوائيٌّ حقّاً: لا يُبكّر أبداً، ولا يتجاوز الخُمس.
    [Fact]
    public void TheDefaultJitter_StaysWithinTheWindow()
    {
        for (var i = 0; i < 200; i++)
        {
            var backoff = new ZatcaRetryBackoff();
            backoff.RecordFailure(Now);
            var due = backoff.NextDueUtc!.Value;
            Assert.InRange(due, Now.AddMinutes(5), Now.AddMinutes(6));
        }
    }
}
