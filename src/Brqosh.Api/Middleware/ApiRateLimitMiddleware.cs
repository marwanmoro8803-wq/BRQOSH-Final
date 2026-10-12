using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Api.Middleware;

/// <summary>
/// G-47 / Shura P1 — حد معدّل طلبات أساسي لكل مفتاح API أو عنوان IP.
///
/// <para>(CEEF-237 · ت-١٠) <b>والقاموسُ يُقلَّم — وكان ينمو بلا حدّ.</b> مفتاحُه
/// <c>X-Api-Key</c> أو عنوانُ المُتّصل، <b>وكلاهما يختاره المُتّصل</b>: فمن أرسل مليونَ طلبٍ
/// بمليون مفتاحٍ مختلف زرع مليونَ مدخلٍ لا يُحذف أبداً. <b>فحدُّ المعدّل نفسُه يصير طريقَ
/// استنزافِ ذاكرة</b> — ومن يُحاول ذلك هو بالضبط من بُني الحدُّ لصدّه.</para>
///
/// <para>والتقليمُ رخيصٌ لأنّ المداخلَ تموت بانقضاء دقيقتها: يُمسح ما تجاوزت دقيقتُه
/// دقيقتين، ولا يقع إلا حين يتجاوز الحجمُ سقفاً — فلا يُدفع ثمنُه في المسار الساخن.</para>
///
/// <para><b>والدلوُ بالمفتاح لا يُمنح إلا لمفتاحٍ صحيح.</b> كان أيُّ <c>X-Api-Key</c> يُقدَّم
/// يفتح دلواً جديداً بمئةٍ وعشرين طلباً — فمن يُخمّن المفاتيح يُرسل كلَّ تخمينٍ بمفتاحٍ مختلف
/// <b>فلا يبلغ الحدَّ أبداً</b>، وهو بعينه ما نُقل الحدُّ قبل فحص المفتاح لصدّه (CEEF-210).
/// فالآن: مفتاحٌ يطابق المُعرَّف يُحسب على بصمته (لا نصِّه)، وكلُّ ما سواه يُحسب على عنوان المُتّصل.</para>
/// </summary>
public sealed class ApiRateLimitMiddleware(RequestDelegate next, IConfiguration? configuration = null)
{
    private const int MaxRequestsPerMinute = 120;

    /// <summary>حجمٌ يبدأ عنده التقليم — أعلى بكثيرٍ من أيّ استعمالٍ مشروع.</summary>
    internal const int PruneThreshold = 10_000;

    private static readonly ConcurrentDictionary<string, (long Bucket, int Count)> Windows = new();

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        var key = ResolveBucketKey(ctx, ResolveConfiguredKey(), ResolveTenantKeys());

        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        PruneIfCrowded(bucket);

        var entry = Windows.AddOrUpdate(
            key,
            _ => (bucket, 1),
            (_, old) => old.Bucket == bucket ? (bucket, old.Count + 1) : (bucket, 1));

        if (entry.Count > MaxRequestsPerMinute)
        {
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.Response.WriteAsJsonAsync(new { error = "rate_limit_exceeded" });
            return;
        }

        await next(ctx);
    }

    private TenantApiKeys? _tenantKeys;

    /// <summary>‏(S-4) تُقرأ مرّةً — وصحّتُها حكمَ عليه الإقلاع (<c>Program</c>)، فلا يُعاد تحليلُها لكلّ طلب.</summary>
    private TenantApiKeys ResolveTenantKeys() =>
        _tenantKeys ??= TenantApiKeys.Parse(configuration?[TenantApiKeys.Env]
                                            ?? Environment.GetEnvironmentVariable(TenantApiKeys.Env));

    private string? ResolveConfiguredKey() =>
        configuration?[ApiSecuritySettings.ApiKeyEnv]
        ?? Environment.GetEnvironmentVariable(ApiSecuritySettings.ApiKeyEnv);

    /// <summary>
    /// مفتاحُ الدلو: بصمةُ المفتاح إن طابق المُعرَّفَ، وإلّا عنوانُ المُتّصل.
    /// والبصمةُ لا النصّ — فلا يبقى المفتاحُ السرّيُّ نصّاً في ذاكرةٍ طويلة العمر.
    /// </summary>
    internal static string ResolveBucketKey(HttpContext ctx, string? configuredKey, TenantApiKeys? tenantKeys = null)
    {
        // ‏(S-4) ومفتاحٌ مربوطٌ بمستأجرٍ مفتاحٌ صحيحٌ كالمشترك — له دلوُه، لا دلوُ عنوانه.
        if (ctx.Request.Headers.TryGetValue("X-Api-Key", out var presented)
            && (SecretComparer.Equals(configuredKey, presented.ToString())
                || tenantKeys?.TenantFor(presented.ToString()) is not null))
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(presented.ToString()));
            return $"key:{Convert.ToHexString(digest, 0, 8)}";
        }

        return $"ip:{ctx.Connection.RemoteIpAddress}";
    }

    /// <summary>يُسقط المداخلَ المنتهية حين يتضخّم القاموس. عديمُ الأثر على مدخلٍ حيّ.</summary>
    internal static void PruneIfCrowded(long currentBucket)
    {
        if (Windows.Count < PruneThreshold) return;

        foreach (var pair in Windows)
        {
            // ‏دقيقتان هامشٌ يمنع حذفَ مدخلٍ لطلبٍ في الطريق.
            if (currentBucket - pair.Value.Bucket >= 2)
                Windows.TryRemove(pair.Key, out _);
        }
    }

    /// <summary>للاختبار وحدَه: يُفرغ الحالةَ المشتركة بين الحالات.</summary>
    internal static void ResetForTests() => Windows.Clear();

    internal static int TrackedKeys => Windows.Count;

    /// <summary>للاختبار وحدَه: يزرع مداخلَ بدقيقةٍ معلومة.</summary>
    internal static void SeedForTests(int count, long bucket, string prefix = "k")
    {
        for (var i = 0; i < count; i++) Windows[$"{prefix}:{i}"] = (bucket, 1);
    }
}
