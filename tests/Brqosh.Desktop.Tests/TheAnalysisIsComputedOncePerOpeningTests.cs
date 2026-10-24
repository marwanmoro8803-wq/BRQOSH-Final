using Brqosh.Application.Intelligence;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل في تباطؤ؟») التحليلُ الماليُّ لا يُحسب مرّتين لفتحةٍ واحدة (اللوحةُ ولوحةُ المساعد معاً)، ولا يُخزَّن
/// فشلُه، ولا يتجاوز عمرُه المدّةَ القصيرة، ولا يُقدَّم تحليلُ منشأةٍ لمنشأةٍ أخرى.
/// </summary>
public class TheAnalysisIsComputedOncePerOpeningTests : IDisposable
{
    public TheAnalysisIsComputedOncePerOpeningTests() => IntelligenceCache.Invalidate();

    public void Dispose() => IntelligenceCache.Invalidate();

    private static FinancialIntelligenceReport Report(long companyId) =>
        new(companyId, DateTime.UtcNow, "test", []);

    [Fact]
    public async Task TwoCallersAtTheSameMoment_ShareOneComputation()
    {
        var runs = 0;
        var gate = new TaskCompletionSource<FinancialIntelligenceReport>();
        Task<FinancialIntelligenceReport> Factory() { runs++; return gate.Task; }

        var first = IntelligenceCache.GetAsync(7, Factory);
        var second = IntelligenceCache.GetAsync(7, Factory);
        gate.SetResult(Report(7));

        Assert.Same(await first, await second);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task AnotherCompany_NeverGetsTheCachedAnalysis()
    {
        var runs = 0;
        Task<FinancialIntelligenceReport> Factory(long id) { runs++; return Task.FromResult(Report(id)); }

        var a = await IntelligenceCache.GetAsync(1, () => Factory(1));
        var b = await IntelligenceCache.GetAsync(2, () => Factory(2));

        Assert.Equal(1, a.CompanyId);
        Assert.Equal(2, b.CompanyId);
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task AfterTheTtl_ItIsComputedAgain()
    {
        var runs = 0;
        Task<FinancialIntelligenceReport> Factory() { runs++; return Task.FromResult(Report(5)); }
        var t0 = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

        await IntelligenceCache.GetAsync(5, Factory, t0);
        await IntelligenceCache.GetAsync(5, Factory, t0 + IntelligenceCache.Ttl - TimeSpan.FromSeconds(1));
        Assert.Equal(1, runs);

        await IntelligenceCache.GetAsync(5, Factory, t0 + IntelligenceCache.Ttl + TimeSpan.FromSeconds(1));
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task AFailure_IsNotKeptAsTheAnswer()
    {
        var runs = 0;
        Task<FinancialIntelligenceReport> Factory()
        {
            runs++;
            return runs == 1
                ? Task.FromException<FinancialIntelligenceReport>(new InvalidOperationException("boom"))
                : Task.FromResult(Report(9));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => IntelligenceCache.GetAsync(9, Factory));
        var again = await IntelligenceCache.GetAsync(9, Factory);

        Assert.Equal(9, again.CompanyId);
        Assert.Equal(2, runs);
    }
}
