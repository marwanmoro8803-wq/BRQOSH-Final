using Brqosh.Api.Middleware;

namespace Brqosh.Api.Tests;

/// <summary>
/// (CEEF-237 · ت-١٠) <b>قاموسُ حدّ المعدّل يُقلَّم — وكان ينمو بلا حدّ.</b>
///
/// <para><b>ما كان:</b> المفتاحُ إمّا <c>X-Api-Key</c> وإمّا عنوانُ المُتّصل، <b>وكلاهما
/// يختاره المُتّصل</b>. فمن أرسل مليونَ طلبٍ بمليون مفتاحٍ مختلف زرع مليونَ مدخلٍ لا يُحذف
/// أبداً. <b>فحدُّ المعدّل نفسُه يصير طريقَ استنزافِ ذاكرة</b> — ومن يُحاول ذلك هو بالضبط
/// من بُني الحدُّ لصدّه.</para>
/// </summary>
public class ApiRateLimitPruningTests
{
    [Fact]
    public void ExpiredEntriesAreDropped_WhenTheDictionaryGrowsBeyondTheThreshold()
    {
        ApiRateLimitMiddleware.ResetForTests();
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;

        ApiRateLimitMiddleware.SeedForTests(
            count: ApiRateLimitMiddleware.PruneThreshold + 10, bucket: bucket - 5);
        Assert.True(ApiRateLimitMiddleware.TrackedKeys > ApiRateLimitMiddleware.PruneThreshold);

        ApiRateLimitMiddleware.PruneIfCrowded(bucket);

        Assert.Equal(0, ApiRateLimitMiddleware.TrackedKeys);
    }

    [Fact]
    public void LiveEntriesSurvivePruning()
    {
        ApiRateLimitMiddleware.ResetForTests();
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;

        // ‏نصفُها منتهية ونصفُها في الدقيقة الجارية.
        ApiRateLimitMiddleware.SeedForTests(ApiRateLimitMiddleware.PruneThreshold, bucket - 5, prefix: "old");
        ApiRateLimitMiddleware.SeedForTests(50, bucket, prefix: "live");

        ApiRateLimitMiddleware.PruneIfCrowded(bucket);

        // ‏تقليمُ مدخلٍ حيٍّ يمنح المُتّصل حدّاً جديداً — فيصير التقليمُ ثغرةَ التفاف.
        Assert.Equal(50, ApiRateLimitMiddleware.TrackedKeys);
    }

    [Fact]
    public void BelowTheThreshold_NothingIsTouched()
    {
        ApiRateLimitMiddleware.ResetForTests();
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        ApiRateLimitMiddleware.SeedForTests(100, bucket - 99);

        ApiRateLimitMiddleware.PruneIfCrowded(bucket);

        // ‏لا يُدفع ثمنُ المسح في المسار الساخن ما دام الحجمُ معقولاً.
        Assert.Equal(100, ApiRateLimitMiddleware.TrackedKeys);
    }
}
