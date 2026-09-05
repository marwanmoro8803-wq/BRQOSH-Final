using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// (CEEF-237 · ت-١٢) <b>رجوعُ ساعة الجهاز يُكشف.</b>
///
/// <para><b>ما كان:</b> صلاحيةُ الترخيص تُقاس بـ<c>DateTime.UtcNow</c> وحدَها، مرّةً عند
/// الإقلاع. <b>فمن أرجع ساعةَ جهازه مدّد ترخيصَه بلا حدّ</b> — وصار هذا حمّالاً بعد أن صار
/// التفعيلُ إلزامياً، أي بعد أن صار الترخيصُ هو ما يفصل بين عميلٍ يدفع وعميلٍ لا يدفع.</para>
/// </summary>
public class ClockRollbackTests : IDisposable
{
    private readonly string _root;

    public ClockRollbackTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "brqosh-clock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _root);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        try { Directory.Delete(_root, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static readonly DateTime Day1 = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheFirstObservation_IsNeverARollback()
    {
        var result = ClockRollbackDetector.Observe(Day1);

        Assert.False(result.RolledBack);
        Assert.Equal(Day1, result.HighWaterUtc);
    }

    [Fact]
    public void TimeMovingForward_IsNormal()
    {
        ClockRollbackDetector.Observe(Day1);

        var result = ClockRollbackDetector.Observe(Day1.AddDays(3));

        Assert.False(result.RolledBack);
    }

    [Fact]
    public void ASmallBackwardStep_IsToleratedAsClockSync()
    {
        ClockRollbackDetector.Observe(Day1);

        var result = ClockRollbackDetector.Observe(Day1.AddHours(-1));

        Assert.False(result.RolledBack);
    }

    [Fact]
    public void AYearBackwards_IsReportedWithTheDistance()
    {
        ClockRollbackDetector.Observe(Day1);

        var result = ClockRollbackDetector.Observe(Day1.AddYears(-1));

        Assert.True(result.RolledBack);
        Assert.True(result.Backwards > TimeSpan.FromDays(300));
    }

    [Fact]
    public void TheMarkIsNotLoweredByARollback_SoASecondAttemptIsStillCaught()
    {
        ClockRollbackDetector.Observe(Day1);
        ClockRollbackDetector.Observe(Day1.AddYears(-1));

        // ‏لو خُفِّضت العلامةُ عند الرجوع لصار الرجوعُ الثاني «طبيعياً».
        var again = ClockRollbackDetector.Observe(Day1.AddYears(-1));

        Assert.True(again.RolledBack);
    }

    [Fact]
    public void TheMarkIsNotPlainTextOnDisk()
    {
        ClockRollbackDetector.Observe(Day1);

        var onDisk = File.ReadAllText(Path.Combine(_root, "clock-mark.dat"));

        // ‏علامةٌ نصّيّةٌ يُحرّرها المستخدمُ بمفكّرةٍ ليست علامة.
        Assert.DoesNotContain("2026", onDisk);
        Assert.Contains("BRQOSH1:", onDisk);
    }

    [Fact]
    public void AnUnreadableMark_IsTreatedAsAbsent_NotAsARollback()
    {
        File.WriteAllText(Path.Combine(_root, "clock-mark.dat"), "not a protected timestamp");

        var result = ClockRollbackDetector.Observe(Day1);

        Assert.False(result.RolledBack);
    }
}
