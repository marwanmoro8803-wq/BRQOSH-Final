using System.Collections.Concurrent;

namespace Brqosh.Api.Idempotency;

public sealed record IdempotentResponse(int StatusCode, string ContentType, string Body);

/// <summary>ذاكرة مؤقتة لمفاتيح Idempotency-Key — M9 أدنى · Redis لاحقاً في SaaS.</summary>
public static class IdempotencyStore
{
    private static readonly ConcurrentDictionary<string, IdempotentResponse> Cache = new();

    public static bool TryGet(string key, out IdempotentResponse response) =>
        Cache.TryGetValue(key, out response!);

    public static void Save(string key, IdempotentResponse response) =>
        Cache[key] = response;

    public static void ResetForTests() => Cache.Clear();
}
